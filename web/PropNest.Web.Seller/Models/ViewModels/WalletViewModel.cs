namespace PropNest.Web.Seller.Models.ViewModels;

public sealed class WalletTransactionCountsViewModel
{
    public int All { get; set; }
    public int TopUp { get; set; }
    public int Charge { get; set; }
    public int Refund { get; set; }
    public int PromoReward { get; set; }
}

public sealed class WalletTransactionViewModel
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; set; }
}

public sealed class WalletViewModel
{
    public decimal MainBalance { get; set; }
    public decimal PromoBalance { get; set; }
    public decimal TotalBalance { get; set; }
    public string? RowVersion { get; set; }

    // Phân trang & Lọc đồng bộ với Properties
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10; // Chuẩn 10 giao dịch / trang
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / (PageSize > 0 ? PageSize : 10));
    public string CurrentType { get; set; } = "all";
    public string? SearchTerm { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public WalletTransactionCountsViewModel Counts { get; set; } = new();
    public List<WalletTransactionViewModel> Transactions { get; set; } = new();
}
