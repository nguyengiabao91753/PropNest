using PropNest.Application.Abstractions;
using PropNest.Domain.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Wallets
{
    public class WalletService(IWalletRepository walletRepository, IUnitOfWork unitOfWork) : IWalletService
    {
        public async Task<WalletDto> CreateWalletAsync(long userId, CancellationToken cancellationToken = default)
        {
            if (await walletRepository.ExistsByUserIdAsync(userId, cancellationToken))
            {
                throw new InvalidOperationException($"User {userId} already has a wallet.");
            }

            var wallet = new Wallet(userId);
            await walletRepository.AddAsync(wallet, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return WalletMappingExtensions.ToDto(wallet);
        }

        public async Task<WalletDto> GetBalanceByUserIdAsync(long userId, CancellationToken cancellationToken = default)
        {
            if (await walletRepository.ExistsByUserIdAsync(userId, cancellationToken) == false)
            {
                throw new InvalidOperationException($"User {userId} do not have wallet");
            }

            var wallet = await walletRepository.GetByUserIdAsync(userId, cancellationToken) ?? new Wallet(userId);
            return WalletMappingExtensions.ToDto(wallet);
        }

        public async Task<IEnumerable<TransactionDto>> GetTransactionsByUserIdAsync(long userId, int? page = 1, WalletTransactionType? type = null, CancellationToken cancellationToken = default)
        {
            var transactions = await walletRepository.GetTransactionsByUserIdAsync(userId, page, type, cancellationToken);
            return transactions.Select(WalletMappingExtensions.ToDto);
        }

        public async Task<WalletDto> TopUpAsync(TopUpWalletCommand command, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.Amount);

            var wallet = await walletRepository.GetByUserIdAsync(command.UserId, cancellationToken)
                ?? throw new KeyNotFoundException($"Wallet for user {command.UserId} not found.");

            // Idempotency: nếu giao dịch TopUp cùng CorrelationId đã tồn tại -> không cộng tiền lần 2
            if (command.CorrelationId.HasValue &&
                await walletRepository.TransactionExistsAsync(wallet.WalletId, command.CorrelationId.Value, WalletTransactionType.TopUp, cancellationToken))
            {
                return WalletMappingExtensions.ToDto(wallet);
            }

            // Thống nhất hạch toán sổ cái theo tổng số dư (Main + Promo) giống Charge và Refund
            var totalBalanceBefore = wallet.MainBalance + wallet.PromoBalance;
            wallet.CreditMain(command.Amount);
            var totalBalanceAfter = wallet.MainBalance + wallet.PromoBalance;

            var transaction = new WalletTransaction(
                walletId: wallet.WalletId,
                correlationId: command.CorrelationId ?? Guid.NewGuid(),
                type: WalletTransactionType.TopUp,
                amount: command.Amount,
                balanceBefore: totalBalanceBefore,
                balanceAfter: totalBalanceAfter,
                description: command.Description ?? "Nạp tiền vào ví"
                );

            await walletRepository.AddTransactionAsync(transaction, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return WalletMappingExtensions.ToDto(wallet);
        }
    }
}
