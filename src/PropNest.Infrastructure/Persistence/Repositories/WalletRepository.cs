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

        public async Task<IEnumerable<WalletTransaction>> GetTransactionsByUserIdAsync(long userId, int? page = 1, WalletTransactionType? type = null, CancellationToken cancellationToken = default)
        {
            var wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
            if(wallet == null)
            {
                return Enumerable.Empty<WalletTransaction>();
            }
            const int pageSize = 10;
            var currentPage =Math.Max(1, page ?? 1);

            var transactions = dbContext.WalletTransactions
                .AsNoTracking()
                .Where(t => t.WalletId == wallet.WalletId);
            if (type.HasValue)
            {
                transactions = transactions.Where(t => t.TransactionType == type.Value);
            }

            return await transactions
                .OrderByDescending(t => t.CreatedAt)
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }
    }
}
