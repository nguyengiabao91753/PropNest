using PropNest.Domain.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Wallets
{
    public interface IWalletService
    {
        Task<WalletDto> CreateWalletAsync(long userId, CancellationToken cancellationToken = default);
        Task<WalletDto> GetBalanceByUserIdAsync(long userId, CancellationToken cancellationToken = default);
        Task<WalletDto> TopUpAsync(TopUpWalletCommand command, CancellationToken cancellationToken = default);

        Task<IEnumerable<TransactionDto>> GetTransactionsByUserIdAsync(long userId, int? page = 1, WalletTransactionType? type = null, CancellationToken cancellationToken = default);


    }

    public interface IWalletRepository
    {
        Task<Wallet?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);
        Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);
        Task<bool> ExistsByUserIdAsync(long userId, CancellationToken cancellationToken = default);
        Task AddTransactionAsync(WalletTransaction transaction, CancellationToken cancellationToken = default);

        Task<IEnumerable<WalletTransaction>> GetTransactionsByUserIdAsync(long userId, int? page = 1, WalletTransactionType? type = null, CancellationToken cancellationToken = default);
    }

    public sealed record WalletDto
    (
        long WalletId,
        long UserId,
        decimal MainBalance,
        decimal PromoBalance

    )
    {
        public decimal TotalBalance => MainBalance + PromoBalance;
    };

    public sealed record TransactionDto
    (
        long TransactionId,
        Guid CorrelationId,
        WalletTransactionType Type,
        decimal Amount,
        decimal BalanceBefore,
        decimal BalanceAfter,
        string Description,
        DateTime CreatedAt
    );

    public sealed record TopUpWalletCommand(
        long UserId,
        decimal Amount,
        string? Description,
        Guid? CorrelationId
    );
}
