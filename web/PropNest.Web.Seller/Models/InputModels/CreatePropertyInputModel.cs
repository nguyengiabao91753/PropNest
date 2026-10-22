using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PropNest.Web.Seller.Models.InputModels;

public sealed class CreatePropertyInputModel
{
    /// <summary>
    /// ID tin đăng (null khi Tạo mới, có giá trị khi Chỉnh sửa)
    /// </summary>
    public long? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề tin đăng.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Tiêu đề phải từ 10 đến 500 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả chi tiết bất động sản.")]
    [MinLength(20, ErrorMessage = "Mô tả cần có ít nhất 20 ký tự để khách hàng nắm rõ thông tin.")]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại bất động sản.")]
    [StringLength(100)]
    [Display(Name = "Loại bất động sản")]
    public string PropertyType { get; set; } = "Apartment";

    [Required(ErrorMessage = "Vui lòng chọn hình thức giao dịch.")]
    [StringLength(50)]
    [Display(Name = "Hình thức giao dịch")]
    public string ListingType { get; set; } = "sale"; // 'sale' | 'rent'

    [Required(ErrorMessage = "Vui lòng nhập giá bất động sản.")]
    [Range(0, 1_000_000_000_000, ErrorMessage = "Mức giá phải lớn hơn hoặc bằng 0.")]
    [Display(Name = "Mức giá (VNĐ)")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập diện tích.")]
    [Range(0.01, 100_000, ErrorMessage = "Diện tích phải từ 0.01 m² trở lên.")]
    [Display(Name = "Diện tích (m²)")]
    public decimal? Area { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập Tỉnh / Thành phố.")]
    [StringLength(100)]
    [Display(Name = "Tỉnh / Thành phố")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập Quận / Huyện.")]
    [StringLength(100)]
    [Display(Name = "Quận / Huyện")]
    public string District { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Phường / Xã")]
    public string? Ward { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ cụ thể.")]
    [StringLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
    [Display(Name = "Địa chỉ chi tiết")]
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Base64 string của byte[] RowVersion - Bắt buộc khi Update để kiểm tra Optimistic Concurrency (409 Conflict)
    /// </summary>
    public string? RowVersion { get; set; }

    /// <summary>
    /// Danh sách file ảnh mới tải lên từ form
    /// </summary>
    [Display(Name = "Hình ảnh tải lên")]
    public List<IFormFile>? ImageFiles { get; set; } = new();

    /// <summary>
    /// Danh sách URL ảnh hiện có (được Backend trả về khi tải form Sửa để xem preview)
    /// </summary>
    public List<string> ExistingImages { get; set; } = new();
}