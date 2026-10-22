using PropNest.Web.Seller.Models.ViewModels;

namespace PropNest.Web.Seller.Services.IServices
{
    public interface ISellerWalletService
    {
        Task<WalletViewModel> GetWalletAsync(int page = 1, string? type = null, string? searchTerm = null, CancellationToken cancellationToken = default);
        Task<bool> TopUpWalletAsync(decimal amount, string? description = null, CancellationToken cancellationToken = default);
    }
}
