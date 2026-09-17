using System.ComponentModel.DataAnnotations;

namespace PropNest.Api.Contracts.Listings;

public class CreateListingRequest
{
    [Required, StringLength(500)]
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    [Required, StringLength(100)]
    public string PropertyType { get; init; } = string.Empty;

    [Required, StringLength(50)]
    public string ListingType { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Price { get; init; }

    [Range(0.01, double.MaxValue)]
    public decimal Area { get; init; }

    [Required, StringLength(100)]
    public string City { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string District { get; init; } = string.Empty;

    [StringLength(100)]
    public string? Ward { get; init; }

    [Required, StringLength(500)]
    public string Address { get; init; } = string.Empty;
}

public sealed class UpdateListingRequest : CreateListingRequest
{
    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class PurchasePackageRequest
{
    [Required, StringLength(50)]
    public string PackageCode { get; init; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal ChargeAmount { get; init; }
}
