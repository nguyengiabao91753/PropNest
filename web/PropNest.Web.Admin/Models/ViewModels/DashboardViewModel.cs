namespace PropNest.Web.Admin.Models.ViewModels;

public sealed class DashboardViewModel
{
    public int TotalProperties { get; set; }
    public int PublishedCount { get; set; }
    public int PendingCount { get; set; }
    public int ActiveSellersCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<PropertyItemViewModel> RecentProperties { get; set; } = new();
    public List<PropertyItemViewModel> PendingQueue { get; set; } = new();

    // Blueprint F02: Phân bổ 8 trạng thái vòng đời tin đăng
    public Dictionary<string, int> StatusDistribution { get; set; } = new();

    // Blueprint F05: Doanh thu và lưu lượng tin đăng theo tháng
    public List<MonthlyMetricItem> MonthlyMetrics { get; set; } = new();

    // Blueprint F09: Chỉ số AME AI Moderation & Saga F06
    public int AmeAutoApprovedCount { get; set; } = 18;
    public int AmeFlaggedCount { get; set; } = 4;
    public double SagaSuccessRate { get; set; } = 99.6;
}

public sealed class MonthlyMetricItem
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int ListingCount { get; set; }
}
