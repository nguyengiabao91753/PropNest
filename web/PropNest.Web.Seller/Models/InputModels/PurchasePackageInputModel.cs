namespace PropNest.Web.Seller.Models.InputModels
{
    public sealed class PurchasePackageInputModel
    {
        public long ListingId { get; set; }
        public string PackageCode {  get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? IdempotencyKey { get; set; }
    }
}
