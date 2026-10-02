using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Contracts.Wallets;
using PropNest.Application.Abstractions;
using PropNest.Application.Wallets;

namespace PropNest.Api.Controllers
{
    [Route("api/v1/wallets")]
    [ApiController]
    public  class WalletsController(ICurrentUser currentUser, IWalletService walletService) : ControllerBase
    {
        [HttpPost("top-up")]
        public async Task<ActionResult<WalletDto>> TopUp(
            TopUpWalletRequest request, 
            [FromHeader(Name = "X-Correlation-Id")] string? correlationHeader, 
            CancellationToken cancellationToken)

        {
            var uerId = currentUser.UserId ?? throw new UnauthorizedAccessException("A valid user identifier claim is required."); ;

            Guid? correlationId = Guid.TryParse(correlationHeader, out var parsedCorrelationId) ? parsedCorrelationId : null;

            var command = new TopUpWalletCommand(uerId, request.Amount, request.Description, correlationId);
            try
            {
                var walletDto = await walletService.TopUpAsync(command, cancellationToken);
                return Ok(walletDto);
            }
            catch (Exception)
            {
                return BadRequest("Failed to top up wallet.");
            }
        }
    }
}
