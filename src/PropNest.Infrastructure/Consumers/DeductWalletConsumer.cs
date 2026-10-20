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
    public partial class DeductWalletConsumer : IConsumer<DeductWalletCommand>
    {
        private readonly PropNestDbContext _dbContext;
        private readonly IWalletRepository _walletRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeductWalletConsumer> _logger;
        public DeductWalletConsumer(
            PropNestDbContext dbContext,
            IWalletRepository walletRepository,
            IUnitOfWork unitOfWork,
            ILogger<DeductWalletConsumer> logger)
        {
            _dbContext = dbContext;
            _walletRepository = walletRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;



        }
        public async Task Consume(ConsumeContext<DeductWalletCommand> context)
        {
            var commnad = context.Message;
            LogConsuming(_logger, commnad.CorrelationId, commnad.UserId, commnad.Amount);

            // Check Idempotency 
            var alreadyProcessed = await _dbContext.WalletTransactions.AnyAsync(t => t.CorrelationId == commnad.CorrelationId && t.TransactionType == WalletTransactionType.Charge, context.CancellationToken);

            if (alreadyProcessed)
            {
                LogAlreadyProcessed(_logger, commnad.CorrelationId);
                await context.Publish(new WalletDeducted(
                    commnad.CorrelationId,
                    commnad.UserId,
                    commnad.Amount
                ));
                return;
            }

            //lấy thông tin ví
            var wallet = await _walletRepository.GetByUserIdAsync( commnad.UserId, context.CancellationToken );
            if(wallet is null)
            {
                LogFailed(_logger, commnad.CorrelationId, "Ví của người dùng không tồn tại");
                await context.Publish(new WalletDeductionFailed(
                    commnad.CorrelationId,
                    commnad.UserId,
                    "Ví của người dùng không tồn tại"
                ));
                return;
            }

            //trừ tiền
            var totalBalanceBefore = wallet.MainBalance + wallet.PromoBalance;
            try
            {
                wallet.Charge(commnad.Amount);
            }
            catch(InvalidOperationException ex)
            {
                LogFailed(_logger, commnad.CorrelationId, ex.Message);
                await context.Publish(new WalletDeductionFailed(
                    commnad.CorrelationId,
                    commnad.UserId,
                    ex.Message
                ));
                return;
            }

            var totalBalanceAfter = wallet.MainBalance + wallet.PromoBalance;
            var transaction = new WalletTransaction(
                walletId: wallet.WalletId,
                correlationId: commnad.CorrelationId,
                type: WalletTransactionType.Charge,
                amount: commnad.Amount,
                balanceBefore: totalBalanceBefore,
                balanceAfter: totalBalanceAfter,
                description: $"Thanh toán gói dịch vụ tin đăng ({commnad.Amount:N0}đ)"
            );

            await _walletRepository.AddTransactionAsync(transaction, context.CancellationToken);
            await _unitOfWork.SaveChangesAsync(context.CancellationToken);

            LogSuccess(_logger, commnad.CorrelationId, commnad.UserId, commnad.Amount);

            await context.Publish(new WalletDeducted(
                commnad.CorrelationId,
                commnad.UserId,
                commnad.Amount
            ));

        }

        [LoggerMessage(Level = LogLevel.Information, Message = "DeductWalletConsumer: Processing DeductWalletCommand for CorrelationId: {CorrelationId}, UserId: {UserId}, Amount: {Amount}")]
        private static partial void LogConsuming(ILogger logger, Guid correlationId, long userId, decimal amount);

        [LoggerMessage(Level = LogLevel.Warning, Message = "DeductWalletConsumer: CorrelationId {CorrelationId} has already been charged. Skipping duplicate.")]
        private static partial void LogAlreadyProcessed(ILogger logger, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "DeductWalletConsumer: Failed to deduct wallet for CorrelationId: {CorrelationId}. Reason: {Reason}")]
        private static partial void LogFailed(ILogger logger, Guid correlationId, string reason);
        [LoggerMessage(Level = LogLevel.Information, Message = "DeductWalletConsumer: Successfully deducted {Amount} for UserId: {UserId}, CorrelationId: {CorrelationId}")]
        private static partial void LogSuccess(ILogger logger, Guid correlationId, long userId, decimal amount);
    }
}
