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

    [Range(1, 200)]
    public int? Floors { get; init; }

    [Range(0, 100)]
    public int? Bedrooms { get; init; }

    [Range(0, 100)]
    public int? Bathrooms { get; init; }

    [StringLength(50)]
    public string? BalconyDirection { get; init; }

    [Required, StringLength(100)]
    public string City { get; init; } = string.Empty;

    [StringLength(100)]
    public string? District { get; init; }

    [StringLength(100)]
    public string? Ward { get; init; }

    [Required, StringLength(500)]
    public string Address { get; init; } = string.Empty;

    [StringLength(20)]
    public string AdministrativeMode { get; init; } = "PostMerger";

    public int? ProvinceCode { get; init; }

    public int? DistrictCode { get; init; }

    public int? WardCode { get; init; }

    [StringLength(500)]
    public string? LegacyAddress { get; init; }
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

public sealed class SuggestListingDescriptionRequest
{
    [StringLength(500)]
    public string? Title { get; init; }

    [StringLength(100)]
    public string PropertyType { get; init; } = "Apartment";

    [StringLength(50)]
    public string ListingType { get; init; } = "sale";

    public decimal? Price { get; init; }

    public decimal? Area { get; init; }

    [StringLength(100)]
    public string? City { get; init; }

    [StringLength(100)]
    public string? District { get; init; }

    [StringLength(100)]
    public string? Ward { get; init; }

    [StringLength(500)]
    public string? Address { get; init; }

    [StringLength(500)]
    public string? LegacyAddress { get; init; }

    [StringLength(20)]
    public string? AdministrativeMode { get; init; }

    public int? Floors { get; init; }

    public int? Bedrooms { get; init; }

    public int? Bathrooms { get; init; }

    [StringLength(50)]
    public string? BalconyDirection { get; init; }
}

public sealed class ParseAddressRequest
{
    [Required, StringLength(500)]
    public string RawAddress { get; init; } = string.Empty;
}
