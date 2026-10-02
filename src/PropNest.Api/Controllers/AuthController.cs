using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropNest.Api.Contracts.Auth;
using PropNest.Application.Abstractions;
using PropNest.Application.Wallets;
using PropNest.Domain.Users;
using PropNest.Infrastructure.Persistence;

namespace PropNest.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<long>> roleManager,
    IWalletService walletService,
    IJwtTokenService jwtTokenService,
    PropNestDbContext dbContext) : ControllerBase
{
    private const string SellerRole = "Seller";

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var roleResult = await EnsureSellerRoleAsync();
        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(ToErrorResponse(roleResult.Errors));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser(email, request.FullName, request.PhoneNumber, SellerRole);
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(ToErrorResponse(createResult.Errors));
        }

        var roleAssignmentResult = await userManager.AddToRoleAsync(user, SellerRole);
        if (!roleAssignmentResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(ToErrorResponse(roleAssignmentResult.Errors));
        }

        await walletService.CreateWalletAsync(user.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new RegisterResponse(user.Id, user.Email!));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.GenerateAccessToken(user, roles);

        return Ok(new LoginResponse(accessToken));
    }

    private async Task<IdentityResult> EnsureSellerRoleAsync()
    {
        if (await roleManager.RoleExistsAsync(SellerRole))
        {
            return IdentityResult.Success;
        }

        return await roleManager.CreateAsync(new IdentityRole<long>(SellerRole));
    }

    private static object ToErrorResponse(IEnumerable<IdentityError> errors)
    {
        return new
        {
            message = "Registration failed.",
            errors = errors.Select(error => new
            {
                code = error.Code,
                description = error.Description
            })
        };
    }
}