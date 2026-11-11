#pragma warning disable CA1707 // Naming Styles: Identifiers should not contain underscores (Task requires exact test names)

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using PropNest.Api.Contracts.Auth;
using PropNest.Api.Controllers;
using PropNest.Application.Abstractions;
using PropNest.Application.Wallets;
using PropNest.Domain.Users;
using PropNest.Infrastructure.Persistence;

namespace PropNest.Domain.Tests;

public sealed class AuthFlowTests : IDisposable
{
    private readonly SqliteConnection _sqliteConnection;
    private readonly PropNestDbContext _dbContext;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<RoleManager<IdentityRole<long>>> _mockRoleManager;
    private readonly Mock<IWalletService> _mockWalletService;
    private readonly Mock<IJwtTokenService> _mockJwtTokenService;
    private readonly AuthController _controller;

    public AuthFlowTests()
    {
        _sqliteConnection = new SqliteConnection("DataSource=:memory:");
        _sqliteConnection.Open();

        var options = new DbContextOptionsBuilder<PropNestDbContext>()
            .UseSqlite(_sqliteConnection)
            .Options;

        _dbContext = new PropNestDbContext(options);

        _mockUserManager = CreateMockUserManager();
        _mockRoleManager = CreateMockRoleManager();
        _mockWalletService = new Mock<IWalletService>();
        _mockJwtTokenService = new Mock<IJwtTokenService>();

        _controller = new AuthController(
            _mockUserManager.Object,
            _mockRoleManager.Object,
            _mockWalletService.Object,
            _mockJwtTokenService.Object,
            _dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _sqliteConnection.Dispose();
    }

    [Fact]
    public async Task register_success()
    {
        // Arrange
        var request = new RegisterRequest(
            "seller@example.com",
            "StrongPassword123!",
            "Nguyen Van A",
            "0901234567");

        _mockRoleManager
            .Setup(r => r.RoleExistsAsync("Seller"))
            .ReturnsAsync(true);

        _mockUserManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager
            .Setup(u => u.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Seller"))
            .ReturnsAsync(IdentityResult.Success);

        _mockWalletService
            .Setup(w => w.CreateWalletAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, CancellationToken _) => new WalletDto(1L, userId, 0m, 0m));

        // Act
        var actionResult = await _controller.Register(request, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var response = Assert.IsType<RegisterResponse>(objectResult.Value);
        Assert.Equal("seller@example.com", response.Email);

        _mockUserManager.Verify(u => u.CreateAsync(
            It.Is<ApplicationUser>(u => u.Email == "seller@example.com" && u.FullName == "Nguyen Van A"),
            request.Password), Times.Once);

        _mockUserManager.Verify(u => u.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), "Seller"), Times.Once);

        _mockWalletService.Verify(w => w.CreateWalletAsync(
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task register_duplicate_email()
    {
        // Arrange
        var request = new RegisterRequest(
            "duplicate@example.com",
            "Password123!",
            "Nguyen Van B",
            "0909999999");

        _mockRoleManager
            .Setup(r => r.RoleExistsAsync("Seller"))
            .ReturnsAsync(true);

        var duplicateError = new IdentityError
        {
            Code = "DuplicateEmail",
            Description = "Email 'duplicate@example.com' is already taken."
        };

        _mockUserManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(duplicateError));

        // Act
        var actionResult = await _controller.Register(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        _mockUserManager.Verify(u => u.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);

        _mockWalletService.Verify(w => w.CreateWalletAsync(
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task register_weak_password()
    {
        // Arrange
        var request = new RegisterRequest(
            "weakpass@example.com",
            "123",
            "Nguyen Van C",
            "0908888888");

        _mockRoleManager
            .Setup(r => r.RoleExistsAsync("Seller"))
            .ReturnsAsync(true);

        var passwordErrors = new[]
        {
            new IdentityError
            {
                Code = "PasswordTooShort",
                Description = "Passwords must be at least 6 characters."
            },
            new IdentityError
            {
                Code = "PasswordRequiresNonAlphanumeric",
                Description = "Passwords must have at least one non alphanumeric character."
            }
        };

        _mockUserManager
            .Setup(u => u.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(passwordErrors));

        // Act
        var actionResult = await _controller.Register(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequestResult.StatusCode);

        _mockUserManager.Verify(u => u.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);

        _mockWalletService.Verify(w => w.CreateWalletAsync(
            It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task login_wrong_password()
    {
        // Arrange
        var request = new LoginRequest("seller@example.com", "WrongPassword!");
        var user = new ApplicationUser(request.Email, "Seller Name", "0901234567", "Seller");

        _mockUserManager
            .Setup(u => u.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(u => u.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(false);

        // Act
        var actionResult = await _controller.Login(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);

        _mockJwtTokenService.Verify(j => j.GenerateAccessToken(
            It.IsAny<ApplicationUser>(), It.IsAny<IList<string>>()), Times.Never);
    }

    [Fact]
    public async Task login_success_returns_token()
    {
        // Arrange
        var request = new LoginRequest("seller@example.com", "CorrectPassword123!");
        var user = new ApplicationUser(request.Email, "Seller Name", "0901234567", "Seller");
        var roles = new List<string> { "Seller" };
        const string expectedAccessToken = "valid-mocked-jwt-token-xyz123";

        _mockUserManager
            .Setup(u => u.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockUserManager
            .Setup(u => u.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        _mockUserManager
            .Setup(u => u.GetRolesAsync(user))
            .ReturnsAsync(roles);

        _mockJwtTokenService
            .Setup(j => j.GenerateAccessToken(user, roles))
            .Returns(expectedAccessToken);

        // Act
        var actionResult = await _controller.Login(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var loginResponse = Assert.IsType<LoginResponse>(okResult.Value);
        Assert.Equal(expectedAccessToken, loginResponse.AccessToken);

        _mockJwtTokenService.Verify(j => j.GenerateAccessToken(user, roles), Times.Once);
    }

    private static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<RoleManager<IdentityRole<long>>> CreateMockRoleManager()
    {
        var store = new Mock<IRoleStore<IdentityRole<long>>>();
        return new Mock<RoleManager<IdentityRole<long>>>(
            store.Object,
            null!, null!, null!, null!);
    }
}
