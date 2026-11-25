using PropNest.Domain.Common;
using PropNest.Domain.Listings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Application.Listings;

public sealed record ListingSubmitted(
    Guid CorrelationId,
    long ListingId,
    long OwnerUserId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal Price,
    decimal Area,
    string City,
    string District,
    string? Ward,
    string Address,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<string>? Images = null,
    string? LegacyAddress = null,
    string AdministrativeMode = "PostMerger",
    int? ProvinceCode = null,
    int? Floors = null,
    int? Bedrooms = null,
    int? Bathrooms = null);

public sealed record ListingPublishedEvent(
    long ListingId,
    long OwnerUserId,
    string PackageCode,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset? PackageEndDate,
    string? PriceWarningMessage = null);

public sealed record ListingApprovedPendingPaymentEvent(
    long ListingId,
    long OwnerUserId,
    string PackageCode,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset? PackageEndDate,
    string? PriceWarningMessage = null);

public sealed record ListingRejectedEvent(
    long ListingId,
    long OwnerUserId,
    string ReasonsJson);

public sealed record ListingExpiredEvent(
    long ListingId,
    long OwnerUserId,
    string PackageCode,
    DateTimeOffset? EndDate);

public sealed record AmeContentRuleDto(
    string RuleCode,
    string Category,
    string KeywordOrPattern,
    bool IsRegex,
    string Severity,
    decimal ScoreWeight);

public sealed record AmePriceBenchmarkDto(
    string PropertyType,
    string ListingType,
    string City,
    string District,
    decimal MinPricePerSqm,
    decimal AvgPricePerSqm,
    decimal MaxPricePerSqm);

public sealed record AmeRuleViolationDto(
    string Rule,
    string Severity,
    string Message);

public sealed record AmeModerationRequest(
    long ListingId,
    Guid CorrelationId,
    string Title,
    string? Description,
    string PropertyType,
    string ListingType,
    decimal Price,
    decimal Area,
    string City,
    string District,
    string? Ward,
    string Address,
    IReadOnlyList<AmeContentRuleDto> ActiveRules,
    AmePriceBenchmarkDto? PriceBenchmark,
    string? LegacyAddress = null,
    string AdministrativeMode = "PostMerger",
    int? ProvinceCode = null,
    int? DistrictCode = null,
    int? WardCode = null,
    int? Floors = null,
    int? Bedrooms = null,
    int? Bathrooms = null,
    string? BalconyDirection = null,
    IReadOnlyList<string>? Images = null);

public sealed record AmeModerationResponse(
    ModerationDecision Decision,
    decimal TotalRiskScore,
    decimal KeywordRiskScore,
    decimal PriceAnomalyScore,
    decimal CompletenessScore,
    decimal DuplicateScore,
    decimal? AiSemanticScore,
    string Summary,
    int ProcessingTimeMs,
    IReadOnlyList<string> ReasonCodes,
    string RawResponseJson,
    IReadOnlyList<AmeRuleViolationDto>? Violations = null,
    string? PriceWarningMessage = null)
{
    public static AmeModerationResponse FallbackManualReview(string reasonCode, string? detail = null)
    {
        // Không đưa thông điệp lỗi kỹ thuật (HTTP/exception/timeout) vào Summary hiển thị cho người dùng.
        _ = detail;
        var summary = ModerationTextFormatter.MapCodeToFriendlyMessage(reasonCode);
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = ModerationTextFormatter.ManualFallbackSummary;
        }
        return new AmeModerationResponse(
            Decision: ModerationDecision.ManualReview,
            TotalRiskScore: 50m,
            KeywordRiskScore: 0m,
            PriceAnomalyScore: 0m,
            CompletenessScore: 0m,
            DuplicateScore: 0m,
            AiSemanticScore: null,
            Summary: summary,
            ProcessingTimeMs: 0,
            ReasonCodes: [reasonCode],
            RawResponseJson: "{}",
            Violations: null,
            PriceWarningMessage: null);
    }
}
public interface IAmeModerationClient
{
    Task<AmeModerationResponse> ModerateAsync(
        AmeModerationRequest request,
        CancellationToken cancellationToken = default);
}