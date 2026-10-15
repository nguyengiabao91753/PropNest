using PropNest.Domain.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Wallets
{
    public static class WalletMappingExtensions
    {
        // Entity -> DTO
        public static WalletDto ToDto(this Wallet wallet)
        {
            return new WalletDto(
                wallet.WalletId,
                wallet.UserId,
                wallet.MainBalance,
                wallet.PromoBalance
            );
        }

        public static TransactionDto ToDto(this WalletTransaction transaction)
        {
            return new TransactionDto(
                transaction.TransactionId,
                transaction.CorrelationId,
                transaction.TransactionType,
                transaction.Amount,
                transaction.BalanceBefore,
                transaction.BalanceAfter,
                transaction.Description,
                transaction.CreatedAt.UtcDateTime
            );
        }
    }
}
