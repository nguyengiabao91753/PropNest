namespace PropNest.Web.Seller.Models.ViewModels;

public sealed record SellerProvinceItem(
    int Code,
    string Name,
    string DivisionType,
    string Codename);

public sealed record SellerDistrictItem(
    int Code,
    string Name,
    string DivisionType,
    string Codename,
    int ProvinceCode);

public sealed record SellerWardItem(
    int Code,
    string Name,
    string DivisionType,
    string Codename,
    int ParentCode);

public sealed record SellerLegacyWardMapping(
    int SourceCode,
    SellerWardItem NewWard,
    SellerProvinceItem? NewProvince,
    IReadOnlyList<string> MergedFromLegacyNames);

public sealed record SellerParsedAddressResult(
    bool Matched,
    string DetectedMode,
    string? StreetAddress,
    int? SuggestedProvinceCode,
    string? SuggestedProvinceName,
    int? SuggestedDistrictCode,
    string? SuggestedDistrictName,
    int? SuggestedWardCode,
    string? SuggestedWardName,
    string? LegacyAddress,
    string? Explanation);

public sealed record SellerSuggestDescriptionInput(
    string? Title,
    string PropertyType,
    string ListingType,
    decimal? Price,
    decimal? Area,
    string? City,
    string? District,
    string? Ward,
    string? Address,
    string? LegacyAddress,
    string? AdministrativeMode,
    int? Floors,
    int? Bedrooms,
    int? Bathrooms,
    string? BalconyDirection);

public sealed record SellerDescriptionSuggestionResult(
    string SuggestedTitle,
    string SuggestedDescription,
    string Source,
    string? ModelName);
