using System.ComponentModel.DataAnnotations;

namespace PropNest.Web.Client.Models.InputModels;

public sealed class InquiryInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên không hợp lệ.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại hoặc email liên hệ.")]
    public string Contact { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập nội dung lời nhắn.")]
    [MinLength(10, ErrorMessage = "Lời nhắn cần ít nhất 10 ký tự.")]
    public string Message { get; set; } = string.Empty;

    public string ListingId { get; set; } = string.Empty;
}
