using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;

namespace PropNest.Infrastructure.Services;

public sealed partial class VietnamAddressService : IVietnamAddressService
{
    private const string PostCacheKey = "vn_address_v2_tree";
    private const string PreCacheKey = "vn_address_v1_tree";
    private static readonly SemaphoreSlim PostTreeLock = new(1, 1);
    private static readonly SemaphoreSlim PreTreeLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] AdminPrefixes =
    [
        "thanh pho truc thuoc trung uong ",
        "thanh pho ",
        "tinh ",
        "thi xa ",
        "thi tran ",
        "quan ",
        "huyen ",
        "phuong ",
        "xa ",
        "tp. ",
        "tp ",
        "tx. ",
        "tx ",
        "tt. ",
        "tt ",
        "q. ",
        "q ",
        "h. ",
        "h ",
        "p. ",
        "p ",
        "x. ",
        "x "
    ];

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly VietnamAddressOptions _options;
    private readonly ILogger<VietnamAddressService> _logger;

    public VietnamAddressService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<VietnamAddressOptions> options,
        ILogger<VietnamAddressService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProvinceDto>> GetProvincesAsync(
        string mode = "post",
        CancellationToken cancellationToken = default)
    {
        if (IsPreMergerMode(mode))
        {
            var tree = await GetPreMergerTreeAsync(cancellationToken);
            return tree.Select(p => new ProvinceDto(p.Code, p.Name, p.DivisionType, p.Codename)).ToList();
        }

        var postTree = await GetPostMergerTreeAsync(cancellationToken);
        return postTree.Select(p => new ProvinceDto(p.Code, p.Name, p.DivisionType, p.Codename)).ToList();
    }

    public async Task<IReadOnlyList<DistrictDto>> GetDistrictsByProvinceAsync(
        int provinceCode,
        CancellationToken cancellationToken = default)
    {
        var tree = await GetPreMergerTreeAsync(cancellationToken);
        var province = tree.FirstOrDefault(p => p.Code == provinceCode);
        if (province?.Districts is null)
        {
            return [];
        }

        return province.Districts
            .Select(d => new DistrictDto(d.Code, d.Name, d.DivisionType, d.Codename, d.ProvinceCode))
            .ToList();
    }

    public async Task<IReadOnlyList<WardDto>> GetWardsByProvinceAsync(
        int provinceCode,
        CancellationToken cancellationToken = default)
    {
        var tree = await GetPostMergerTreeAsync(cancellationToken);
        var province = tree.FirstOrDefault(p => p.Code == provinceCode);
        if (province?.Wards is null)
        {
            return [];
        }

        return province.Wards
            .Select(w => new WardDto(w.Code, w.Name, w.DivisionType, w.Codename, w.ProvinceCode))
            .ToList();
    }

    public async Task<IReadOnlyList<WardDto>> GetWardsByDistrictAsync(
        int districtCode,
        CancellationToken cancellationToken = default)
    {
        var tree = await GetPreMergerTreeAsync(cancellationToken);
        var district = tree
            .SelectMany(p => p.Districts ?? [])
            .FirstOrDefault(d => d.Code == districtCode);

        if (district?.Wards is null)
        {
            return [];
        }

        return district.Wards
            .Select(w => new WardDto(w.Code, w.Name, w.DivisionType, w.Codename, w.DistrictCode))
            .ToList();
    }

    public async Task<LegacyWardMappingDto?> ConvertLegacyWardAsync(
        int? legacyWardCode,
        string? legacyWardName,
        int? legacyProvinceCode = null,
        CancellationToken cancellationToken = default)
    {
        if ((!legacyWardCode.HasValue || legacyWardCode <= 0) && string.IsNullOrWhiteSpace(legacyWardName))
        {
            return null;
        }

        var cacheKey = $"vn_legacy_conv:{legacyWardCode}:{legacyWardName?.Trim().ToLowerInvariant()}:{legacyProvinceCode}";
        if (_cache.TryGetValue(cacheKey, out LegacyWardMappingDto? cachedMapping))
        {
            return cachedMapping;
        }

        var query = legacyWardCode.HasValue && legacyWardCode > 0
            ? $"/api/v2/w/from-legacy/?legacy_code={legacyWardCode.Value}"
            : $"/api/v2/w/from-legacy/?legacy_name={Uri.EscapeDataString(legacyWardName!.Trim())}";

        List<ApiFromLegacyResult>? mappings;
        try
        {
            mappings = await _httpClient.GetFromJsonAsync<List<ApiFromLegacyResult>>(query, JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogExternalApiError(_logger, ex, query);
            return null;
        }

        if (mappings is null || mappings.Count == 0)
        {
            return null;
        }

        var chosen = mappings[0];
        if (legacyProvinceCode.HasValue && mappings.Count > 1)
        {
            chosen = mappings.FirstOrDefault(m => m.Ward?.ProvinceCode == legacyProvinceCode.Value) ?? mappings[0];
        }

        if (chosen.Ward is null)
        {
            return null;
        }

        var postTree = await GetPostMergerTreeAsync(cancellationToken);
        var newProvince = postTree.FirstOrDefault(p => p.Code == chosen.Ward.ProvinceCode);
        var mergedLegacyNames = await FetchLegacySourcesForWardAsync(chosen.Ward.Code, cancellationToken);

        var mappingDto = new LegacyWardMappingDto(
            SourceCode: chosen.SourceCode,
            NewWard: new WardDto(
                chosen.Ward.Code,
                chosen.Ward.Name,
                chosen.Ward.DivisionType,
                chosen.Ward.Codename,
                chosen.Ward.ProvinceCode),
            NewProvince: newProvince is null
                ? null
                : new ProvinceDto(newProvince.Code, newProvince.Name, newProvince.DivisionType, newProvince.Codename),
            MergedFromLegacyNames: mergedLegacyNames);

        _cache.Set(cacheKey, mappingDto, TimeSpan.FromHours(Math.Max(1, _options.CacheHours)));
        return mappingDto;
    }

    public async Task<ParsedAddressResultDto> ParseAndSuggestAddressAsync(
        string rawAddress,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawAddress))
        {
            return new ParsedAddressResultDto(
                Matched: false,
                DetectedMode: "Unknown",
                StreetAddress: null,
                SuggestedProvinceCode: null,
                SuggestedProvinceName: null,
                SuggestedDistrictCode: null,
                SuggestedDistrictName: null,
                SuggestedWardCode: null,
                SuggestedWardName: null,
                LegacyAddress: null,
                Explanation: "Vui lòng nhập hoặc dán địa chỉ cần nhận dạng.");
        }

        var preTree = await GetPreMergerTreeAsync(cancellationToken);
        var postTree = await GetPostMergerTreeAsync(cancellationToken);

        var segments = rawAddress
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var normalizedFull = NormalizeText(rawAddress);

        // 1. Kiểm tra khớp theo chuẩn Trước sáp nhập (3 cấp: Tỉnh - Quận/Huyện - Phường/Xã)
        var preMatch = TryMatchPreMerger(preTree, segments, normalizedFull);
        if (preMatch.Province is not null && preMatch.Ward is not null)
        {
            var mapping = await ConvertLegacyWardAsync(
                preMatch.Ward.Code,
                preMatch.Ward.Name,
                preMatch.Province.Code,
                cancellationToken);

            var legacyText = string.Join(", ",
                new[] { preMatch.Ward.Name, preMatch.District?.Name, preMatch.Province.Name }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

            var street = BuildRemainingStreet(segments, preMatch.MatchedSegmentIndices, rawAddress);

            if (mapping is not null && mapping.NewProvince is not null)
            {
                return new ParsedAddressResultDto(
                    Matched: true,
                    DetectedMode: "PreMerger",
                    StreetAddress: street,
                    SuggestedProvinceCode: mapping.NewProvince.Code,
                    SuggestedProvinceName: mapping.NewProvince.Name,
                    SuggestedDistrictCode: preMatch.District?.Code,
                    SuggestedDistrictName: preMatch.District?.Name,
                    SuggestedWardCode: mapping.NewWard.Code,
                    SuggestedWardName: mapping.NewWard.Name,
                    LegacyAddress: legacyText,
                    Explanation: $"Đã nhận diện địa chỉ trước sáp nhập ({legacyText}) và quy đổi sang địa chỉ sau sáp nhập: {mapping.NewWard.Name}, {mapping.NewProvince.Name}.");
            }
        }

        // 2. Kiểm tra khớp theo chuẩn Sau sáp nhập (2 cấp: Tỉnh/TP - Phường/Xã)
        var postMatch = TryMatchPostMerger(postTree, segments, normalizedFull);
        if (postMatch.Province is not null)
        {
            var street = BuildRemainingStreet(segments, postMatch.MatchedSegmentIndices, rawAddress);

            // Nếu người dùng nhập địa chỉ cũ có tên Quận/Huyện nhưng không khớp phường mới, vẫn giữ LegacyAddress
            var legacyNote = preMatch.District is not null && preMatch.Province is not null
                ? string.Join(", ", new[] { postMatch.Ward?.Name, preMatch.District.Name, preMatch.Province.Name }.Where(s => !string.IsNullOrWhiteSpace(s)))
                : null;

            var summaryText = postMatch.Ward is not null
                ? $"{postMatch.Ward.Name}, {postMatch.Province.Name}"
                : postMatch.Province.Name;

            return new ParsedAddressResultDto(
                Matched: true,
                DetectedMode: "PostMerger",
                StreetAddress: street,
                SuggestedProvinceCode: postMatch.Province.Code,
                SuggestedProvinceName: postMatch.Province.Name,
                SuggestedDistrictCode: preMatch.District?.Code,
                SuggestedDistrictName: preMatch.District?.Name,
                SuggestedWardCode: postMatch.Ward?.Code,
                SuggestedWardName: postMatch.Ward?.Name,
                LegacyAddress: legacyNote,
                Explanation: postMatch.Ward is not null
                    ? $"Đã nhận diện địa chỉ theo đơn vị hành chính sau sáp nhập: {summaryText}."
                    : $"Đã nhận diện được Tỉnh/Thành phố ({summaryText}). Vui lòng chọn thêm Phường/Xã.");
        }

        // 3. Nếu chỉ khớp Tỉnh + Quận trước sáp nhập -> gợi ý Tỉnh sau sáp nhập (nếu cùng mã) hoặc trả kết quả một phần
        if (preMatch.Province is not null)
        {
            var street = BuildRemainingStreet(segments, preMatch.MatchedSegmentIndices, rawAddress);
            var postProv = postTree.FirstOrDefault(p => p.Code == preMatch.Province.Code)
                           ?? postTree.FirstOrDefault(p => NormalizeAdminName(p.Name) == NormalizeAdminName(preMatch.Province.Name));

            var legacyPartial = string.Join(", ",
                new[] { preMatch.District?.Name, preMatch.Province.Name }.Where(s => !string.IsNullOrWhiteSpace(s)));

            return new ParsedAddressResultDto(
                Matched: postProv is not null,
                DetectedMode: "PreMerger",
                StreetAddress: street,
                SuggestedProvinceCode: postProv?.Code ?? preMatch.Province.Code,
                SuggestedProvinceName: postProv?.Name ?? preMatch.Province.Name,
                SuggestedDistrictCode: preMatch.District?.Code,
                SuggestedDistrictName: preMatch.District?.Name,
                SuggestedWardCode: null,
                SuggestedWardName: null,
                LegacyAddress: legacyPartial,
                Explanation: $"Đã nhận diện khu vực trước sáp nhập ({legacyPartial}). Vui lòng chọn chính xác Phường/Xã mới.");
        }

        return new ParsedAddressResultDto(
            Matched: false,
            DetectedMode: "Unknown",
            StreetAddress: rawAddress.Trim(),
            SuggestedProvinceCode: null,
            SuggestedProvinceName: null,
            SuggestedDistrictCode: null,
            SuggestedDistrictName: null,
            SuggestedWardCode: null,
            SuggestedWardName: null,
            LegacyAddress: null,
            Explanation: "Không thể tự động khớp chính xác Tỉnh/Phường từ chuỗi vừa nhập. Vui lòng chọn thủ công từ danh sách bên dưới.");
    }

    private async Task<IReadOnlyList<ApiPostProvince>> GetPostMergerTreeAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(PostCacheKey, out List<ApiPostProvince>? cached) && cached is { Count: > 0 })
        {
            return cached;
        }

        await PostTreeLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(PostCacheKey, out cached) && cached is { Count: > 0 })
            {
                return cached;
            }

            var data = await _httpClient.GetFromJsonAsync<List<ApiPostProvince>>("/api/v2/?depth=2", JsonOptions, cancellationToken)
                       ?? [];
            if (data.Count > 0)
            {
                _cache.Set(PostCacheKey, data, TimeSpan.FromHours(Math.Max(1, _options.CacheHours)));
            }
            return data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogExternalApiError(_logger, ex, "/api/v2/?depth=2");
            return [];
        }
        finally
        {
            PostTreeLock.Release();
        }
    }

    private async Task<IReadOnlyList<ApiPreProvince>> GetPreMergerTreeAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(PreCacheKey, out List<ApiPreProvince>? cached) && cached is { Count: > 0 })
        {
            return cached;
        }

        await PreTreeLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(PreCacheKey, out cached) && cached is { Count: > 0 })
            {
                return cached;
            }

            var data = await _httpClient.GetFromJsonAsync<List<ApiPreProvince>>("/api/v1/?depth=3", JsonOptions, cancellationToken)
                       ?? [];
            if (data.Count > 0)
            {
                _cache.Set(PreCacheKey, data, TimeSpan.FromHours(Math.Max(1, _options.CacheHours)));
            }
            return data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogExternalApiError(_logger, ex, "/api/v1/?depth=3");
            return [];
        }
        finally
        {
            PreTreeLock.Release();
        }
    }

    private async Task<IReadOnlyList<string>> FetchLegacySourcesForWardAsync(int wardCode, CancellationToken cancellationToken)
    {
        var cacheKey = $"vn_ward_legacies:{wardCode}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cachedSources) && cachedSources is not null)
        {
            return cachedSources;
        }

        try
        {
            var list = await _httpClient.GetFromJsonAsync<List<ApiLegacyWardItem>>(
                $"/api/v2/w/{wardCode}/to-legacies/",
                JsonOptions,
                cancellationToken);
            if (list is null || list.Count == 0)
            {
                return [];
            }

            var names = list.Select(x => x.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            _cache.Set(cacheKey, names, TimeSpan.FromHours(Math.Max(1, _options.CacheHours)));
            return names;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            LogExternalApiError(_logger, ex, $"/api/v2/w/{wardCode}/to-legacies/");
            return [];
        }
    }

    private static (ApiPreProvince? Province, ApiPreDistrict? District, ApiPreWard? Ward, HashSet<int> MatchedSegmentIndices)
        TryMatchPreMerger(IReadOnlyList<ApiPreProvince> tree, List<string> segments, string normalizedFull)
    {
        var matchedIndices = new HashSet<int>();
        ApiPreProvince? matchedProvince = null;
        ApiPreDistrict? matchedDistrict = null;
        ApiPreWard? matchedWard = null;

        // Duyệt từ cuối lên đầu (thường địa chỉ VN kết thúc bằng Tỉnh/TP)
        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var candidate = FindBestProvince(tree, segments[i], p => p.Name);
            if (candidate is not null)
            {
                matchedProvince = candidate;
                matchedIndices.Add(i);
                break;
            }
        }

        if (matchedProvince is null)
        {
            matchedProvince = tree
                .OrderByDescending(p => NormalizeAdminName(p.Name).Length)
                .FirstOrDefault(p => normalizedFull.Contains(NormalizeAdminName(p.Name), StringComparison.Ordinal));
        }

        if (matchedProvince?.Districts is null)
        {
            return (matchedProvince, null, null, matchedIndices);
        }

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (matchedIndices.Contains(i)) continue;
            var candidate = FindBestItem(matchedProvince.Districts, segments[i], d => d.Name);
            if (candidate is not null)
            {
                matchedDistrict = candidate;
                matchedIndices.Add(i);
                break;
            }
        }

        if (matchedDistrict is null)
        {
            matchedDistrict = matchedProvince.Districts
                .OrderByDescending(d => NormalizeAdminName(d.Name).Length)
                .FirstOrDefault(d => normalizedFull.Contains(NormalizeAdminName(d.Name), StringComparison.Ordinal));
        }

        var wardPool = matchedDistrict?.Wards
                       ?? matchedProvince.Districts.SelectMany(d => d.Wards ?? []).ToList();

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (matchedIndices.Contains(i)) continue;
            var candidate = FindBestItem(wardPool, segments[i], w => w.Name);
            if (candidate is not null)
            {
                matchedWard = candidate;
                matchedIndices.Add(i);
                matchedDistrict ??= matchedProvince.Districts.FirstOrDefault(d => d.Code == candidate.DistrictCode);
                break;
            }
        }

        return (matchedProvince, matchedDistrict, matchedWard, matchedIndices);
    }

    private static (ApiPostProvince? Province, ApiPostWard? Ward, HashSet<int> MatchedSegmentIndices)
        TryMatchPostMerger(IReadOnlyList<ApiPostProvince> tree, List<string> segments, string normalizedFull)
    {
        var matchedIndices = new HashSet<int>();
        ApiPostProvince? matchedProvince = null;
        ApiPostWard? matchedWard = null;

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var candidate = FindBestProvince(tree, segments[i], p => p.Name);
            if (candidate is not null)
            {
                matchedProvince = candidate;
                matchedIndices.Add(i);
                break;
            }
        }

        if (matchedProvince is null)
        {
            matchedProvince = tree
                .OrderByDescending(p => NormalizeAdminName(p.Name).Length)
                .FirstOrDefault(p => normalizedFull.Contains(NormalizeAdminName(p.Name), StringComparison.Ordinal));
        }

        if (matchedProvince?.Wards is null)
        {
            return (matchedProvince, null, matchedIndices);
        }

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (matchedIndices.Contains(i)) continue;
            var candidate = FindBestItem(matchedProvince.Wards, segments[i], w => w.Name);
            if (candidate is not null)
            {
                matchedWard = candidate;
                matchedIndices.Add(i);
                break;
            }
        }

        return (matchedProvince, matchedWard, matchedIndices);
    }

    private static T? FindBestProvince<T>(IEnumerable<T> items, string rawSegment, Func<T, string> nameSelector) where T : class
    {
        var expanded = ExpandProvinceAlias(rawSegment);
        return FindBestItem(items, expanded, nameSelector);
    }

    private static T? FindBestItem<T>(IEnumerable<T> items, string rawSegment, Func<T, string> nameSelector) where T : class
    {
        var normSeg = NormalizeAdminName(rawSegment);
        var normFullSeg = NormalizeText(rawSegment);

        if (string.IsNullOrWhiteSpace(normSeg))
        {
            return null;
        }

        T? exactFull = null;
        T? exactCore = null;
        T? containsCore = null;

        foreach (var item in items)
        {
            var itemName = nameSelector(item);
            var normItemFull = NormalizeText(itemName);
            var normItemCore = NormalizeAdminName(itemName);

            if (string.Equals(normItemFull, normFullSeg, StringComparison.Ordinal))
            {
                exactFull = item;
                break;
            }

            if (string.Equals(normItemCore, normSeg, StringComparison.Ordinal))
            {
                exactCore ??= item;
            }
            else if (normSeg.Length >= 3 && normItemCore.Length >= 3 &&
                     (normSeg.Contains(normItemCore, StringComparison.Ordinal) || normItemCore.Contains(normSeg, StringComparison.Ordinal)))
            {
                // Tránh nhầm "quan 1" với "quan 10", "quan 11", "quan 12"
                if (IsNumericAdminMismatch(normSeg, normItemCore))
                {
                    continue;
                }
                containsCore ??= item;
            }
        }

        return exactFull ?? exactCore ?? containsCore;
    }

    private static bool IsNumericAdminMismatch(string a, string b)
    {
        var aDigits = new string(a.Where(char.IsDigit).ToArray());
        var bDigits = new string(b.Where(char.IsDigit).ToArray());
        return aDigits.Length > 0 && bDigits.Length > 0 && !string.Equals(aDigits, bDigits, StringComparison.Ordinal);
    }

    private static string BuildRemainingStreet(List<string> segments, HashSet<int> matchedIndices, string rawAddress)
    {
        if (segments.Count == 0)
        {
            return rawAddress.Trim();
        }

        var remaining = segments
            .Where((_, index) => !matchedIndices.Contains(index))
            .ToList();

        return remaining.Count > 0 ? string.Join(", ", remaining) : rawAddress.Trim();
    }

    private static string ExpandProvinceAlias(string rawSegment)
    {
        var normalized = NormalizeText(rawSegment);
        return normalized switch
        {
            "hcm" or "tp hcm" or "tphcm" or "sai gon" or "tp sai gon" => "ho chi minh",
            "hn" or "tp hn" => "ha noi",
            "dn" or "tp dn" => "da nang",
            "hp" or "tp hp" => "hai phong",
            "ct" or "tp ct" => "can tho",
            "bd" => "binh duong",
            "brvt" or "vung tau" => "ba ria - vung tau",
            _ => rawSegment
        };
    }

    private static string NormalizeAdminName(string input)
    {
        var normalized = NormalizeText(input);
        foreach (var prefix in AdminPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.Ordinal))
            {
                normalized = normalized[prefix.Length..].Trim();
                break;
            }
        }

        return normalized;
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(ch switch
            {
                'đ' or 'Đ' => 'd',
                '-' or '_' or '.' => ' ',
                _ => char.ToLowerInvariant(ch)
            });
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsPreMergerMode(string? mode) =>
        string.Equals(mode, "pre", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(mode, "PreMerger", StringComparison.OrdinalIgnoreCase);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "VietnamAddressService failed calling endpoint {Endpoint}.")]
    private static partial void LogExternalApiError(ILogger logger, Exception exception, string endpoint);

    private sealed class ApiPostProvince
    {
        public int Code { get; init; }
        public string Name { get; init; } = string.Empty;
        [JsonPropertyName("division_type")]
        public string DivisionType { get; init; } = string.Empty;
        public string Codename { get; init; } = string.Empty;
        public List<ApiPostWard>? Wards { get; init; }
    }

    private sealed class ApiPostWard
    {
        public int Code { get; init; }
        public string Name { get; init; } = string.Empty;
        [JsonPropertyName("division_type")]
        public string DivisionType { get; init; } = string.Empty;
        public string Codename { get; init; } = string.Empty;
        [JsonPropertyName("province_code")]
        public int ProvinceCode { get; init; }
    }

    private sealed class ApiPreProvince
    {
        public int Code { get; init; }
        public string Name { get; init; } = string.Empty;
        [JsonPropertyName("division_type")]
        public string DivisionType { get; init; } = string.Empty;
        public string Codename { get; init; } = string.Empty;
        public List<ApiPreDistrict>? Districts { get; init; }
    }

    private sealed class ApiPreDistrict
    {
        public int Code { get; init; }
        public string Name { get; init; } = string.Empty;
        [JsonPropertyName("division_type")]
        public string DivisionType { get; init; } = string.Empty;
        public string Codename { get; init; } = string.Empty;
        [JsonPropertyName("province_code")]
        public int ProvinceCode { get; init; }
        public List<ApiPreWard>? Wards { get; init; }
    }

    private sealed class ApiPreWard
    {
        public int Code { get; init; }
        public string Name { get; init; } = string.Empty;
        [JsonPropertyName("division_type")]
        public string DivisionType { get; init; } = string.Empty;
        public string Codename { get; init; } = string.Empty;
        [JsonPropertyName("district_code")]
        public int DistrictCode { get; init; }
    }

    private sealed class ApiFromLegacyResult
    {
        [JsonPropertyName("source_code")]
        public int SourceCode { get; init; }
        public ApiPostWard? Ward { get; init; }
    }

    private sealed class ApiLegacyWardItem
    {
        public string Name { get; init; } = string.Empty;
    }
}
