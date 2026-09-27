using System.ComponentModel.DataAnnotations;

namespace PropNest.Web.Client.Models.ViewModels;

public sealed class SignupViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn loại tài khoản.")]
    public string Role { get; set; } = "customer"; // 'customer' | 'seller'

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ và tên không hợp lệ.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ email.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
    public string Password { get; set; } = string.Empty;
}
