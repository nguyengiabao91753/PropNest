using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Persistence;

namespace PropNest.Infrastructure.Consumers;

public sealed partial class AmeModerationConsumer : IConsumer<ListingSubmitted>
{
    private const string AmeSource = "AME";
    private const string AmeModelInfo = "PropNest-AME-v2.1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] AdminPrefixes =
    [
        "thanh pho ", "tp. ", "tp ", "tinh ", "quan ", "q. ",
        "huyen ", "thi xa ", "tx. ", "tx ", "phuong ", "p. ", "xa ", "thi tran "
    ];

    private readonly PropNestDbContext _dbContext;
    private readonly IListingRepository _listingRepository;
    private readonly IAmeModerationClient _ameModerationClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AmeModerationConsumer> _logger;

    public AmeModerationConsumer(
        PropNestDbContext dbContext,
        IListingRepository listingRepository,
        IAmeModerationClient ameModerationClient,
        IUnitOfWork unitOfWork,
        ILogger<AmeModerationConsumer> logger)
    {
        _dbContext = dbContext;
        _listingRepository = listingRepository;
        _ameModerationClient = ameModerationClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ListingSubmitted> context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var message = context.Message;
        LogConsuming(_logger, message.ListingId, message.CorrelationId);

        // 0. Kiểm tra Idempotency theo CorrelationId trên bảng AmeModerationResults
        var alreadyModerated = await _dbContext.AmeModerationResults
            .AsNoTracking()
            .AnyAsync(r => r.CorrelationId == message.CorrelationId, context.CancellationToken);

        if (alreadyModerated)
        {
            LogAlreadyProcessed(_logger, message.ListingId, message.CorrelationId);
            return;
        }

        // 1. Tải Listing và kiểm tra trạng thái PendingModeration
        var listing = await _listingRepository.GetByIdAsync(message.ListingId, context.CancellationToken);
        if (listing is null)
        {
            LogListingNotFound(_logger, message.ListingId, message.CorrelationId);
            return;
        }

        if (listing.Status != ListingStatus.PendingModeration)
        {
            LogListingNotPending(_logger, listing.ListingId, listing.Status, message.CorrelationId);
            return;
        }

        // 2. Lấy ContentRules active và MarketPriceBenchmark phù hợp nhất (chuẩn hóa tên địa danh trước & sau sáp nhập)
        var activeRuleEntities = await _dbContext.ContentRules
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.RuleId)
            .ToListAsync(context.CancellationToken);

        var activeRules = activeRuleEntities
            .Select(r => new AmeContentRuleDto(
                RuleCode: $"RULE_{r.RuleId}",
                Category: r.RuleType,
                KeywordOrPattern: r.Pattern,
                IsRegex: IsRegexRuleType(r.RuleType),
                Severity: r.Severity,
                ScoreWeight: MapSeverityWeight(r.Severity)))
            .ToList();

        var benchmarkDto = await ResolveRegionalPriceBenchmarkAsync(listing, context.CancellationToken);

        var imageUrls = (listing.Images is { Count: > 0 }
                ? listing.Images
                    .OrderBy(img => img.IsCover ? 0 : 1)
                    .ThenBy(img => img.SortOrder)
                    .Select(img => img.Url)
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .ToList()
                : message.Images?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList()) ?? [];

        // 3. Gọi sang dịch vụ Python AME (có cơ chế Fallback an toàn sang ManualReview cho Admin)
        var ameRequest = new AmeModerationRequest(
            ListingId: listing.ListingId,
            CorrelationId: message.CorrelationId,
            Title: listing.Title,
            Description: listing.Description,
            PropertyType: listing.PropertyType,
            ListingType: listing.ListingType,
            Price: listing.Price,
            Area: listing.Area,
            City: listing.City,
            District: listing.District,
            Ward: listing.Ward,
            Address: listing.Address,
            ActiveRules: activeRules,
            PriceBenchmark: benchmarkDto,
            LegacyAddress: listing.LegacyAddress,
            AdministrativeMode: listing.AdministrativeMode,
            ProvinceCode: listing.ProvinceCode,
            DistrictCode: listing.DistrictCode,
            WardCode: listing.WardCode,
            Floors: listing.Floors,
            Bedrooms: listing.Bedrooms,
            Bathrooms: listing.Bathrooms,
            BalconyDirection: listing.BalconyDirection,
            Images: imageUrls);

        AmeModerationResponse ameResponse;
        try
        {
            ameResponse = await _ameModerationClient.ModerateAsync(ameRequest, context.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUnexpectedClientError(_logger, ex, listing.ListingId, message.CorrelationId);
            ameResponse = AmeModerationResponse.FallbackManualReview("AME_CONSUMER_ERROR_FALLBACK_MANUAL", ex.Message);
        }

        // 4. Lưu kết quả phân tích chi tiết vào bảng AmeModerationResults
        var scoreJson = JsonSerializer.Serialize(new
        {
            ameResponse.TotalRiskScore,
            ameResponse.KeywordRiskScore,
            ameResponse.PriceAnomalyScore,
            ameResponse.CompletenessScore,
            ameResponse.DuplicateScore,
            ameResponse.AiSemanticScore,
            ameResponse.ProcessingTimeMs
        }, JsonOptions);

        var priceWarningMessage = !string.IsNullOrWhiteSpace(ameResponse.PriceWarningMessage)
            ? ameResponse.PriceWarningMessage
            : ameResponse.Violations?.FirstOrDefault(v =>
                v.Rule.Contains("PRICE", StringComparison.OrdinalIgnoreCase))?.Message;

        var reasonsJson = JsonSerializer.Serialize(new
        {
            ameResponse.Summary,
            ameResponse.ReasonCodes,
            PriceWarningMessage = priceWarningMessage,
            ameResponse.Violations
        }, JsonOptions);

        var warningsJson = ameResponse.ReasonCodes.Count > 0
            ? JsonSerializer.Serialize(ameResponse.ReasonCodes, JsonOptions)
            : null;

        var moderationResult = new AmeModerationResult(
            listingId: listing.ListingId,
            correlationId: message.CorrelationId,
            decision: ameResponse.Decision,
            scoreJson: scoreJson,
            reasonsJson: reasonsJson,
            warningsJson: warningsJson,
            modelInfo: AmeModelInfo);

        _dbContext.AmeModerationResults.Add(moderationResult);

        // 5. Áp dụng quyết định vào Domain Listing, ghi Review, History và Outbox
        switch (ameResponse.Decision)
        {
            case ModerationDecision.Approved:
                ApplyAutoApprove(listing, reasonsJson, ameResponse.Summary, priceWarningMessage);
                break;

            case ModerationDecision.Rejected:
                ApplyAutoReject(listing, reasonsJson);
                break;

            default:
                ApplyManualReviewFallback(listing, reasonsJson);
                break;
        }

        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
        LogDecisionApplied(_logger, listing.ListingId, ameResponse.Decision, ameResponse.TotalRiskScore, message.CorrelationId);
    }

    private async Task<AmePriceBenchmarkDto?> ResolveRegionalPriceBenchmarkAsync(
        Listing listing,
        CancellationToken cancellationToken)
    {
        var benchmarks = await _dbContext.MarketPriceBenchmarks
            .AsNoTracking()
            .Where(b => b.PropertyType == listing.PropertyType && b.ListingType == listing.ListingType)
            .ToListAsync(cancellationToken);

        var cityNorm = NormalizeAdminUnit(listing.City);
        var districtTokens = ExtractDistrictTokens(listing.District, listing.Ward, listing.LegacyAddress);

        var matched = benchmarks
            .OrderByDescending(b => b.EffectiveFrom)
            .ThenByDescending(b => b.BenchmarkId)
            .FirstOrDefault(b =>
                NormalizeAdminUnit(b.CityCode) == cityNorm &&
                districtTokens.Contains(NormalizeAdminUnit(b.DistrictCode)));

        if (matched is not null)
        {
            return new AmePriceBenchmarkDto(
                matched.PropertyType,
                matched.ListingType,
                matched.CityCode,
                matched.DistrictCode,
                matched.MinPricePerSqm,
                matched.AvgPricePerSqm,
                matched.MaxPricePerSqm);
        }

        // Fallback theo tin Published thực tế trong cùng Tỉnh/Thành phố (yêu cầu tối thiểu 3 tin để có bình quân tin cậy)
        var comparableUnitPrices = await _dbContext.Listings
            .AsNoTracking()
            .Where(x => x.ListingId != listing.ListingId
                        && x.Status == ListingStatus.Published
                        && x.PropertyType == listing.PropertyType
                        && x.ListingType == listing.ListingType
                        && x.Area > 0
                        && x.Price > 0
                        && (listing.ProvinceCode.HasValue
                            ? x.ProvinceCode == listing.ProvinceCode
                            : x.City == listing.City))
            .Select(x => new { x.Price, x.Area, x.District, x.LegacyAddress })
            .Take(200)
            .ToListAsync(cancellationToken);

        var sameDistrictPrices = comparableUnitPrices
            .Where(x => districtTokens.Count > 0 &&
                        (districtTokens.Contains(NormalizeAdminUnit(x.District)) ||
                         (!string.IsNullOrWhiteSpace(x.LegacyAddress) &&
                          ExtractDistrictTokens(null, null, x.LegacyAddress).Overlaps(districtTokens))))
            .Select(x => x.Price / x.Area)
            .ToList();

        var sample = sameDistrictPrices.Count >= 3
            ? sameDistrictPrices
            : comparableUnitPrices.Select(x => x.Price / x.Area).ToList();

        if (sample.Count >= 3)
        {
            sample.Sort();
            var avg = sample.Average();
            var min = sample[Math.Max(0, (int)(sample.Count * 0.15))];
            var max = sample[Math.Min(sample.Count - 1, (int)(sample.Count * 0.85))];
            return new AmePriceBenchmarkDto(
                listing.PropertyType,
                listing.ListingType,
                listing.City,
                string.IsNullOrWhiteSpace(listing.District) ? "*" : listing.District,
                Math.Round(min, 0),
                Math.Round(avg, 0),
                Math.Round(max, 0));
        }

        return null;
    }

    private static HashSet<string> ExtractDistrictTokens(string? district, string? ward, string? legacyAddress)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddNormalized(string? raw)
        {
            var norm = NormalizeAdminUnit(raw);
            if (!string.IsNullOrWhiteSpace(norm))
            {
                set.Add(norm);
            }
        }

        AddNormalized(district);
        AddNormalized(ward);

        if (!string.IsNullOrWhiteSpace(legacyAddress))
        {
            foreach (var part in legacyAddress.Split([',', ';', '/', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                AddNormalized(part);
            }
        }

        return set;
    }

    private static string NormalizeAdminUnit(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var replaced = raw.Trim().Replace('đ', 'd').Replace('Đ', 'D');
        var decomposed = replaced.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
        }

        var cleaned = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        foreach (var prefix in AdminPrefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.Ordinal))
            {
                cleaned = cleaned[prefix.Length..].Trim();
            }
        }

        return cleaned switch
        {
            "tphcm" or "tp hcm" or "hcm" or "sai gon" or "saigon" => "ho chi minh",
            "hn" or "thu do ha noi" => "ha noi",
            _ => cleaned
        };
    }

    private static bool IsRegexRuleType(string ruleType) =>
        string.Equals(ruleType, "PhonePattern", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ruleType, "BadWord", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ruleType, "REGEX", StringComparison.OrdinalIgnoreCase);

    private static decimal MapSeverityWeight(string severity)
    {
        if (string.Equals(severity, "Reject", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(severity, "BLOCK", StringComparison.OrdinalIgnoreCase))
        {
            return 100m;
        }

        if (string.Equals(severity, "ManualReview", StringComparison.OrdinalIgnoreCase))
        {
            return 50m;
        }

        return 25m;
    }

    private void ApplyAutoApprove(
        Listing listing,
        string reasonsJson,
        string summary,
        string? priceWarningMessage)
    {
        listing.Approve(DateTimeOffset.UtcNow);

        _listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            AmeSource,
            ModerationDecision.Approved,
            reasonsJson,
            reviewerUserId: null));

        var historyNote = !string.IsNullOrWhiteSpace(priceWarningMessage)
            ? $"[Cảnh báo giá ảo] {priceWarningMessage}"
            : string.IsNullOrWhiteSpace(summary)
                ? (listing.Status == ListingStatus.Published
                    ? "AME duyệt tự động nội dung cập nhật - tin tiếp tục hiển thị theo thời hạn gói đã thanh toán."
                    : "AME duyệt tự động thành công - chờ người bán thanh toán gói để hiển thị tin.")
                : summary;

        _listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            actorUserId: null,
            "AutoApprove",
            historyNote));

        var outboxType = listing.Status == ListingStatus.Published
            ? "ListingPublished"
            : "ListingApprovedPendingPayment";

        var approvedPayload = JsonSerializer.Serialize(new
        {
            listing.ListingId,
            listing.OwnerUserId,
            listing.PackageCode,
            Status = listing.Status.ToString(),
            listing.StartDate,
            listing.EndDate,
            listing.PackageEndDate,
            PriceWarningMessage = priceWarningMessage
        }, JsonOptions);
        _listingRepository.AddOutboxMessage(new OutboxMessage(outboxType, approvedPayload));
    }

    private void ApplyAutoReject(Listing listing, string reasonsJson)
    {
        listing.Reject();

        _listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            AmeSource,
            ModerationDecision.Rejected,
            reasonsJson,
            reviewerUserId: null));

        _listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            actorUserId: null,
            "AutoReject",
            reasonsJson));

        var rejectedPayload = JsonSerializer.Serialize(new
        {
            listing.ListingId,
            listing.OwnerUserId,
            ReasonsJson = reasonsJson
        }, JsonOptions);
        _listingRepository.AddOutboxMessage(new OutboxMessage("ListingRejected", rejectedPayload));
    }

    private void ApplyManualReviewFallback(Listing listing, string reasonsJson)
    {
        listing.MarkForManualReview();

        _listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            AmeSource,
            ModerationDecision.ManualReview,
            reasonsJson,
            reviewerUserId: null));

        _listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            actorUserId: null,
            "RouteManualReview",
            reasonsJson));
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "AmeModerationConsumer: Starting moderation for ListingId={ListingId}, CorrelationId={CorrelationId}.")]
    private static partial void LogConsuming(ILogger logger, long listingId, Guid correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "AmeModerationConsumer: CorrelationId={CorrelationId} for ListingId={ListingId} was already processed. Skipping.")]
    private static partial void LogAlreadyProcessed(ILogger logger, long listingId, Guid correlationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "AmeModerationConsumer: ListingId={ListingId} was not found (CorrelationId={CorrelationId}). Skipping.")]
    private static partial void LogListingNotFound(ILogger logger, long listingId, Guid correlationId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "AmeModerationConsumer: ListingId={ListingId} is in status {Status} (not PendingModeration). Skipping (CorrelationId={CorrelationId}).")]
    private static partial void LogListingNotPending(ILogger logger, long listingId, ListingStatus status, Guid correlationId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "AmeModerationConsumer: Unexpected error calling IAmeModerationClient for ListingId={ListingId}, CorrelationId={CorrelationId}. Falling back to ManualReview.")]
    private static partial void LogUnexpectedClientError(ILogger logger, Exception exception, long listingId, Guid correlationId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "AmeModerationConsumer: Completed ListingId={ListingId} with Decision={Decision}, TotalRiskScore={TotalRiskScore}, CorrelationId={CorrelationId}.")]
    private static partial void LogDecisionApplied(ILogger logger, long listingId, ModerationDecision decision, decimal totalRiskScore, Guid correlationId);
}
