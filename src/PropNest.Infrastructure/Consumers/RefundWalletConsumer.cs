using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropNest.Application.Abstractions;
using PropNest.Application.Wallets;
using PropNest.Application.Workflows;
using PropNest.Domain.Wallets;
using PropNest.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Consumers
{
    public partial class RefundWalletConsumer : IConsumer<RefundWalletCommand>
    {
        private readonly PropNestDbContext _dbContext;
        private readonly IWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RefundWalletConsumer> _logger;
        public RefundWalletConsumer(
            PropNestDbContext dbContext,
            IWalletRepository walletRepository,
            IUnitOfWork unitOfWork,
            ILogger<RefundWalletConsumer> logger)
        {
            _dbContext = dbContext;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        public async Task Consume(ConsumeContext<RefundWalletCommand> context)
        {
            var command = context.Message;
            LogConsuming(_logger, command.CorrelationId, command.UserId, command.Amount);

            // check idempotency
            var alreadyProcessed = await _dbContext.WalletTransactions.AnyAsync(
                w=>w.CorrelationId == command.CorrelationId && w.TransactionType == WalletTransactionType.Refund,
                context.CancellationToken
                );
            if (alreadyProcessed)
            {
                LogAlreadyProcessed(_logger, command.CorrelationId);
                await context.Publish(new WalletRefunded(
                    command.CorrelationId,
                    command.UserId,
                    command.Amount));
                return;
            }

            //lấy thông tin ví
            var wallet = await _walletRepository.GetByUserIdAsync(command.UserId, context.CancellationToken);
            if (wallet is null)
            {
                LogWalletNotFound(_logger, command.CorrelationId, command.UserId);
                throw new InvalidOperationException($"Không tìm thấy ví của người dùng có ID {command.UserId} để hoàn tiền.");
            }

            //hoàn tièn
            var totalBalanceBefore = wallet.MainBalance + wallet.PromoBalance;
            wallet.Refund(command.Amount);
            var totalBalanceAfter = wallet.MainBalance + wallet.PromoBalance;

            var transaction = new WalletTransaction(
              walletId: wallet.WalletId,
              correlationId: command.CorrelationId,
              type: WalletTransactionType.Refund,
              amount: command.Amount,
              balanceBefore: totalBalanceBefore,
              balanceAfter: totalBalanceAfter,
              description: $"Hoàn tiền giao dịch mua gói tin đăng thất bại ({command.Amount:N0}đ)"
          );
            await _walletRepository.AddTransactionAsync(transaction, context.CancellationToken);
            await _unitOfWork.SaveChangesAsync(context.CancellationToken);
            LogSuccess(_logger, command.CorrelationId, command.UserId, command.Amount);

            //publish event
            await context.Publish(new WalletRefunded(
               command.CorrelationId,
               command.UserId,
               command.Amount));

        }

        [LoggerMessage(Level = LogLevel.Information, Message = "RefundWalletConsumer: Processing RefundWalletCommand for CorrelationId: {CorrelationId}, UserId: {UserId}, Amount: {Amount}")]
        private static partial void LogConsuming(ILogger logger, Guid correlationId, long userId, decimal amount);
        [LoggerMessage(Level = LogLevel.Warning, Message = "RefundWalletConsumer: CorrelationId {CorrelationId} has already been refunded. Skipping duplicate.")]
        private static partial void LogAlreadyProcessed(ILogger logger, Guid correlationId);
        [LoggerMessage(Level = LogLevel.Error, Message = "RefundWalletConsumer: Wallet not found for UserId: {UserId}, CorrelationId: {CorrelationId}")]
        private static partial void LogWalletNotFound(ILogger logger, Guid correlationId, long userId);
        [LoggerMessage(Level = LogLevel.Information, Message = "RefundWalletConsumer: Successfully refunded {Amount} for UserId: {UserId}, CorrelationId: {CorrelationId}")]
        private static partial void LogSuccess(ILogger logger, Guid correlationId, long userId, decimal amount);
    }
}
