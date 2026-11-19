using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropNest.Api.Contracts.Wallets;
using PropNest.Api.Extensions;
using PropNest.Application.Abstractions;
using PropNest.Application.Wallets;
using PropNest.Domain.Wallets;

namespace PropNest.Api.Controllers
{
    [Route("api/v1/wallets")]
    [ApiController]
    [Authorize]
    public sealed partial class WalletsController(
        ICurrentUser currentUser,
        IWalletService walletService,
        IHostEnvironment hostEnvironment,
        ILogger<WalletsController> logger) : ControllerBase
    {
        [HttpPost("top-up")]
        public async Task<ActionResult<WalletDto>> TopUp(
            TopUpWalletRequest request,
            [FromHeader(Name = "X-Correlation-Id")] string? correlationHeader,
            CancellationToken cancellationToken)
        {
            if (!hostEnvironment.IsDevelopment() && !currentUser.IsInRole("Admin"))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Nạp tiền trực tiếp chỉ được phép trong môi trường Development hoặc bởi Quản trị viên." });
            }

            var userId = currentUser.UserId
                ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");

            Guid? correlationId = Guid.TryParse(correlationHeader, out var parsedCorrelationId)
                ? parsedCorrelationId
                : null;

            var command = new TopUpWalletCommand(userId, request.Amount, request.Description, correlationId);
            try
            {
                var walletDto = await walletService.TopUpAsync(command, cancellationToken);
                return Ok(walletDto);
            }
            catch (Exception ex)
            {
                LogWalletError(logger, ex, "TopUp", userId);
                return this.HandleException(ex);
            }
        }

        [HttpGet("me")]
        public async Task<ActionResult<WalletDto>> GetMyWallet(CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId
                ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
            try
            {
                var walletDto = await walletService.GetBalanceByUserIdAsync(userId, cancellationToken);
                return Ok(walletDto);
            }
            catch (Exception ex)
            {
                LogWalletError(logger, ex, "GetMyWallet", userId);
                return this.HandleException(ex);
            }
        }

        [HttpGet("me/transactions")]
        public async Task<ActionResult<IEnumerable<TransactionDto>>> GetMyTransactions(
            [FromQuery] int? page,
            [FromQuery] WalletTransactionType? type,
            CancellationToken cancellationToken)
        {
            var userId = currentUser.UserId
                ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
            try
            {
                var transactions = await walletService.GetTransactionsByUserIdAsync(userId, page, type, cancellationToken);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                LogWalletError(logger, ex, "GetMyTransactions", userId);
                return this.HandleException(ex);
            }
        }

        [LoggerMessage(
            EventId = 1,
            Level = LogLevel.Warning,
            Message = "WalletsController.{Action} failed for UserId={UserId}.")]
        private static partial void LogWalletError(ILogger logger, Exception ex, string action, long userId);
    }
}
