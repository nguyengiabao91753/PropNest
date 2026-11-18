namespace PropNest.Application.Listings;

public sealed record ProvinceDto(
    int Code,
    string Name,
    string DivisionType,
    string Codename);

public sealed record DistrictDto(
    int Code,
    string Name,
    string DivisionType,
    string Codename,
    int ProvinceCode);

public sealed record WardDto(
    int Code,
    string Name,
    string DivisionType,
    string Codename,
    int ParentCode);

public sealed record LegacyWardMappingDto(
    int SourceCode,
    WardDto NewWard,
    ProvinceDto? NewProvince,
    IReadOnlyList<string> MergedFromLegacyNames);

public sealed record ParsedAddressResultDto(
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

public interface IVietnamAddressService
{
    Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync(
        string mode = "post",
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DistrictDto>> GetDistrictsByProvinceAsync(
        int provinceCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WardDto>> GetWardsByProvinceAsync(
        int provinceCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WardDto>> GetWardsByDistrictAsync(
        int districtCode,
        CancellationToken cancellationToken = default);

    Task<LegacyWardMappingDto?> ConvertLegacyWardAsync(
        int? legacyWardCode,
        string? legacyWardName,
        int? legacyProvinceCode = null,
        CancellationToken cancellationToken = default);

    Task<ParsedAddressResultDto> ParseAndSuggestAddressAsync(
        string rawAddress,
        CancellationToken cancellationToken = default);
}
