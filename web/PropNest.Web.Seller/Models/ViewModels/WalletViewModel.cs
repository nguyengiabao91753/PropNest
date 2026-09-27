namespace PropNest.Web.Seller.Models.ViewModels;

public sealed class WalletTransactionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "Charge"; // 'TopUp' | 'Charge' | 'Refund'
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ListingId { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class WalletViewModel
{
    public decimal MainBalance { get; set; } = 4_500_000m;
    public decimal PromoBalance { get; set; } = 750_000m;
    public decimal TotalBalance => MainBalance + PromoBalance;
    public string RowVersion { get; set; } = "0x00000000000007D4";
    public List<WalletTransactionViewModel> Transactions { get; set; } = new();
}
