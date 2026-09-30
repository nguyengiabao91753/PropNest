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
    }
}
