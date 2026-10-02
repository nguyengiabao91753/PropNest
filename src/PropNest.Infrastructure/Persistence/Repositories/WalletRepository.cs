using Microsoft.EntityFrameworkCore;
using PropNest.Application.Wallets;
using PropNest.Domain.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Persistence.Repositories
{
    public sealed class WalletRepository(PropNestDbContext dbContext) : IWalletRepository
    {
        public Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
        {
            return  dbContext.Wallets.AddAsync(wallet, cancellationToken).AsTask();
        }

        public Task AddTransactionAsync(WalletTransaction transaction, CancellationToken cancellationToken = default)
        {
            return dbContext.WalletTransactions.AddAsync(transaction, cancellationToken).AsTask();
        }

        public Task<bool> ExistsByUserIdAsync(long userId, CancellationToken cancellationToken = default)
        {
            return dbContext.Wallets.AnyAsync(x => x.UserId == userId, cancellationToken);
        }

        public Task<Wallet?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default)
        {
            return dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
        }
    }
}
