using System.ComponentModel.DataAnnotations;

namespace PropNest.Web.Admin.Models.InputModels;

public sealed class AdminLoginInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập Email.")]
    [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
    public string Email { get; set; } = "admin@propnest.vn";

    [Required(ErrorMessage = "Vui lòng nhập Mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "Admin@123456";

    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }
}
