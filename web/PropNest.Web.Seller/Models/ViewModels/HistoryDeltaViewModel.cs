namespace PropNest.Web.Seller.Models.ViewModels;

public sealed class DeltaItemViewModel
{
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

public sealed class HistoryDeltaViewModel
{
    public long HistoryId { get; set; }
    public long ListingId { get; set; }
    public string ActionType { get; set; } = "Cập nhật";
    public string RawActionType { get; set; } = string.Empty;
    public string Actor { get; set; } = "Hệ thống";
    public string? ActorRole { get; set; }
    public DateTimeOffset ActionDate { get; set; } = DateTimeOffset.UtcNow;
    public string? Note { get; set; }
    public List<string> RejectReasons { get; set; } = new();
    public List<DeltaItemViewModel> DeltaChanges { get; set; } = new();

    public bool IsReject =>
        string.Equals(RawActionType, "Reject", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Từ chối", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Reject", StringComparison.OrdinalIgnoreCase) ||
        RejectReasons.Count > 0;

    public bool IsUpdate =>
        string.Equals(RawActionType, "UpdateContent", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(RawActionType, "Update", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Cập nhật", StringComparison.OrdinalIgnoreCase) ||
        DeltaChanges.Count > 0;

    public bool IsCreate =>
        string.Equals(RawActionType, "Create", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Tạo", StringComparison.OrdinalIgnoreCase);

    public bool IsSubmit =>
        string.Equals(RawActionType, "Submit", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Gửi", StringComparison.OrdinalIgnoreCase);

    public bool IsApprove =>
        (string.Equals(RawActionType, "Approve", StringComparison.OrdinalIgnoreCase) ||
         ActionType.Contains("Duyệt", StringComparison.OrdinalIgnoreCase)) && !IsReject;

    public bool IsExpire =>
        string.Equals(RawActionType, "Expire", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Hết hạn", StringComparison.OrdinalIgnoreCase);

    public bool IsHide =>
        string.Equals(RawActionType, "Hide", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Ẩn", StringComparison.OrdinalIgnoreCase);

    public bool IsUpgrade =>
        string.Equals(RawActionType, "UpgradePackage", StringComparison.OrdinalIgnoreCase) ||
        ActionType.Contains("Gói", StringComparison.OrdinalIgnoreCase);
}
