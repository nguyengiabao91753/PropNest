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
    }
}
