using System.ComponentModel.DataAnnotations;

namespace PropNest.Web.Seller.Models.InputModels;

public sealed class CreatePropertyInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập tiêu đề tin đăng.")]
    [StringLength(150, MinimumLength = 10, ErrorMessage = "Tiêu đề cần có từ 10 đến 150 ký tự.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chi tiết bất động sản.")]
    [MinLength(20, ErrorMessage = "Mô tả cần có ít nhất 20 ký tự để khách hàng nắm rõ thông tin.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại bất động sản.")]
    public string PropertyType { get; set; } = "Apartment";

    [Required(ErrorMessage = "Vui lòng chọn hình thức giao dịch.")]
    public string ListingType { get; set; } = "sale"; // 'sale' | 'rent'

    [Required(ErrorMessage = "Vui lòng nhập giá bất động sản.")]
    [Range(100_000, 1_000_000_000_000, ErrorMessage = "Giá không hợp lệ.")]
    public decimal Price { get; set; } = 5_000_000_000m;

    [Required(ErrorMessage = "Vui lòng nhập diện tích.")]
    [Range(10, 10_000, ErrorMessage = "Diện tích phải từ 10m² đến 10.000m².")]
    public decimal Area { get; set; } = 85m;

    public int Beds { get; set; } = 2;
    public int Baths { get; set; } = 2;
    public int Carports { get; set; } = 1;

    [Required(ErrorMessage = "Vui lòng chọn Tỉnh / Thành phố.")]
    public string City { get; set; } = "TP. Hồ Chí Minh";

    [Required(ErrorMessage = "Vui lòng chọn Quận / Huyện.")]
    public string District { get; set; } = "Quận 2 (Thủ Đức)";

    public string? Ward { get; set; } = "Thảo Điền";

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ cụ thể.")]
    public string Address { get; set; } = "Đường Nguyễn Văn Hưởng";

    public string PackageCode { get; set; } = "VIP"; // 'Standard' | 'VIP' | 'Boost'

    public bool IsDraft { get; set; }

    public string ImageUrl { get; set; } = "/images/p1.png";

    public string? Banner { get; set; } = "VIP Nổi Bật";

    public bool BannerOn { get; set; } = true;

    public bool ShowAddress { get; set; } = true;
}
