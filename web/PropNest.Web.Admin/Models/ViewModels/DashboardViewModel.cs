namespace PropNest.Web.Admin.Models.ViewModels;

public sealed class DashboardViewModel
{
    // 4 Core Required Metric Cards (TASK 217)
    public int PublishedCount { get; set; }
    public int PendingCount { get; set; }
    public int TotalUsers { get; set; }
    public int TotalWalletTransactions { get; set; }
    public decimal TotalWalletVolume { get; set; }

    // Additional System Overview Metrics
    public int TotalProperties { get; set; }
    public int ActiveSellersCount { get; set; }
    public decimal TotalRevenue { get; set; }

    // Lists for Dashboard Panels
    public List<PropertyItemViewModel> RecentProperties { get; set; } = new();
    public List<PropertyItemViewModel> PendingQueue { get; set; } = new();
    public List<WalletTransactionItemViewModel> RecentTransactions { get; set; } = new();

    // Chart Data Models
    public Dictionary<string, int> StatusDistribution { get; set; } = new();
    public List<MonthlyMetricItem> MonthlyMetrics { get; set; } = new();
    public WalletDistributionViewModel WalletStats { get; set; } = new();

    // AI & Saga Engine Metrics
    public int AmeAutoApprovedCount { get; set; }
    public int AmeFlaggedCount { get; set; }
    public double SagaSuccessRate { get; set; } = 100.0;
}

public sealed class MonthlyMetricItem
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int ListingCount { get; set; }
    public int WalletTransactionCount { get; set; }
}

public sealed class WalletTransactionItemViewModel
{
    public string TransactionId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "TopUp"; // 'TopUp' | 'Charge' | 'Refund'
    public decimal Amount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "Thành công";
    public string Description { get; set; } = string.Empty;
}

public sealed class WalletDistributionViewModel
{
    public int TopUpCount { get; set; }
    public int PackagePurchaseCount { get; set; }
    public int RefundCount { get; set; }
    public decimal TotalTopUpAmount { get; set; }
    public decimal TotalSpentAmount { get; set; }
}

