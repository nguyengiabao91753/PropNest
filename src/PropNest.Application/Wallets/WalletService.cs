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

        public async Task<WalletDto> TopUpAsync(TopUpWalletCommand command, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.Amount);

            var wallet = await walletRepository.GetByUserIdAsync(command.UserId, cancellationToken) ?? throw new InvalidOperationException($"Wallet for user {command.UserId} not found.");

            var balanceBefore = wallet.MainBalance;
            wallet.CreditMain(command.Amount);
            var balanceAfter = wallet.MainBalance;

            var transaction = new WalletTransaction(
                walletId: wallet.WalletId,
                correlationId: command.CorrelationId ?? Guid.NewGuid(),
                type: WalletTransactionType.TopUp,
                amount: command.Amount,
                balanceBefore: balanceBefore,
                balanceAfter: balanceAfter,
                description: command.Description ?? "Nạp tiền vào ví"
                );

            await walletRepository.AddTransactionAsync(transaction, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return WalletMappingExtensions.ToDto(wallet);
        }
    }
}
