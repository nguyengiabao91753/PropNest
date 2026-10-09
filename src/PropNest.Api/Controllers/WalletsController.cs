using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Contracts.Wallets;
using PropNest.Application.Abstractions;
using PropNest.Application.Wallets;
using PropNest.Domain.Wallets;

namespace PropNest.Api.Controllers
{
    [Route("api/v1/wallets")]
    [ApiController]
    public class WalletsController(ICurrentUser currentUser, IWalletService walletService) : ControllerBase
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


        [HttpGet("me")]
        public async Task<ActionResult<WalletDto>> GetMyWallet(CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
            try
            {
                var walletDto = await walletService.GetBalanceByUserIdAsync(userId, cancellationToken);
                return Ok(walletDto);
            }
            catch (Exception)
            {
                return NotFound("Wallet not found for the current user.");
            }
        }

        [HttpGet("me/transactions")]
        public async Task<ActionResult<IEnumerable<TransactionDto>>> GetMyTransactions([FromQuery] int? page,[FromQuery] WalletTransactionType? type, CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
            try
            {
                var transactions = await walletService.GetTransactionsByUserIdAsync(userId, page, type, cancellationToken);
                return Ok(transactions);
            }
            catch (Exception)
            {
                return NotFound("No transactions found for the current user.");
            }
        }
    }
}
