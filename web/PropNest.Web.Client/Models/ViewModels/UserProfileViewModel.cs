namespace PropNest.Web.Client.Models.ViewModels;

public sealed class UserProfileViewModel
{
    public long? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
    public string Status { get; set; } = "Active";
    public DateTimeOffset? CreatedAt { get; set; }

    public string RoleDisplay => Role switch
    {
        "Admin" => "Quản trị viên (Admin)",
        "Moderator" => "Kiểm duyệt viên (Moderator)",
        "Seller" => "Nhà môi giới / Người bán (Seller)",
        _ => "Khách hàng (Customer)"
    };

    public string StatusDisplay => Status switch
    {
        "Active" => "Đang hoạt động",
        "Suspended" => "Tạm khóa",
        _ => Status
    };

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                if (!string.IsNullOrWhiteSpace(Email))
                {
                    return Email[..Math.Min(2, Email.Length)].ToUpperInvariant();
                }
                return "PN";
            }

            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
            }
            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }
}
