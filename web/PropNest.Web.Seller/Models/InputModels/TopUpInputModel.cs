using System.ComponentModel.DataAnnotations;

namespace PropNest.Web.Seller.Models.InputModels;

public sealed class TopUpInputModel
{
    [Required(ErrorMessage = "Vui lòng nhập số tiền cần nạp.")]
    [Range(50_000, 100_000_000, ErrorMessage = "Số tiền nạp tối thiểu là 50.000 ₫ và tối đa là 100.000.000 ₫.")]
    public decimal Amount { get; set; } = 1_000_000m;
}
