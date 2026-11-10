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
    DateTimeOffset SubmittedAt);

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
    AmePriceBenchmarkDto? PriceBenchmark);
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
    string RawResponseJson)
{
    public static AmeModerationResponse FallbackManualReview(string reasonCode, string? detail = null)
    {
        var summary = string.IsNullOrWhiteSpace(detail)
            ? "Hệ thống kiểm duyệt tự động tạm thời không khả dụng. Tin đăng được chuyển sang duyệt thủ công."
            : $"Hệ thống kiểm duyệt tự động chuyển sang duyệt thủ công: {detail}";
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
            RawResponseJson: "{}");
    }
}
public interface IAmeModerationClient
{
    Task<AmeModerationResponse> ModerateAsync(
        AmeModerationRequest request,
        CancellationToken cancellationToken = default);
}