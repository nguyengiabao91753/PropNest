using System.ComponentModel.DataAnnotations;

namespace PropNest.Web.Admin.Models.InputModels;

public sealed class RejectPropertyInputModel
{
    [Required(ErrorMessage = "Mã tin đăng không được để trống.")]
    public string ListingId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lý do từ chối tin đăng theo quy định Blueprint F03.")]
    [MinLength(5, ErrorMessage = "Lý do từ chối cần có ít nhất 5 ký tự để người bán hiểu rõ.")]
    public string Reason { get; set; } = string.Empty;
}
