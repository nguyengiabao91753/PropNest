using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;

namespace PropNest.Infrastructure.Services;

public partial class AmeModerationClient : IAmeModerationClient
{
    private const string ModerateEndpoint = "/api/v1/moderate";
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;
    private readonly AmeOptions _options;
    private readonly ILogger<AmeModerationClient> _logger;

    public AmeModerationClient(
        HttpClient httpClient,
        IOptions<AmeOptions> options,
        ILogger<AmeModerationClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AmeModerationResponse> ModerateAsync(
        AmeModerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_options.Enabled)
        {
            LogAmeDisabled(request.ListingId, request.CorrelationId);
            return AmeModerationResponse.FallbackManualReview("AME_DISABLED_FALLBACK_MANUAL");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ModerateEndpoint)
            {
                Content = JsonContent.Create(request, options: SerializerOptions)
            };
            httpRequest.Headers.TryAddWithoutValidation(CorrelationIdHeader, request.CorrelationId.ToString());
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                LogAmeHttpError(request.ListingId, (int)response.StatusCode, request.CorrelationId);
                return AmeModerationResponse.FallbackManualReview(
                    "AME_HTTP_ERROR_FALLBACK_MANUAL",
                    $"HTTP {(int)response.StatusCode}");
            }

            var payload = JsonSerializer.Deserialize<FastApiModerationPayload>(rawJson, SerializerOptions);
            if (payload is null)
            {
                LogAmeEmptyPayload(request.ListingId, request.CorrelationId);
                return AmeModerationResponse.FallbackManualReview("AME_EMPTY_PAYLOAD_FALLBACK_MANUAL");
            }

            var violations = payload.Violations?
                .Select(v => new AmeRuleViolationDto(
                    v.Rule ?? "RULE_VIOLATION",
                    v.Severity ?? "info",
                    v.Message ?? string.Empty))
                .ToList() ?? [];

            var reasonCodes = payload.ReasonCodes is { Count: > 0 }
                ? payload.ReasonCodes
                : violations.Select(v => v.Rule).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var priceWarningMsg = !string.IsNullOrWhiteSpace(payload.PriceWarningMessage)
                ? payload.PriceWarningMessage
                : violations.FirstOrDefault(v =>
                    v.Rule.Contains("PRICE", StringComparison.OrdinalIgnoreCase))?.Message;

            var totalRiskScore = payload.TotalRiskScore > 0m
                ? payload.TotalRiskScore
                : payload.Score.HasValue
                    ? Math.Clamp(Math.Round((1m - payload.Score.Value) * 100m, 1), 0m, 100m)
                    : 0m;

            var summary = !string.IsNullOrWhiteSpace(payload.Summary)
                ? payload.Summary
                : !string.IsNullOrWhiteSpace(priceWarningMsg)
                    ? priceWarningMsg
                    : violations.Count > 0
                        ? string.Join("; ", violations.Select(v => v.Message).Where(m => !string.IsNullOrWhiteSpace(m)))
                        : "Đã hoàn tất chấm điểm tự động.";

            return new AmeModerationResponse(
                Decision: ParseDecision(payload.Decision),
                TotalRiskScore: totalRiskScore,
                KeywordRiskScore: payload.KeywordRiskScore,
                PriceAnomalyScore: payload.PriceAnomalyScore,
                CompletenessScore: payload.CompletenessScore,
                DuplicateScore: payload.DuplicateScore,
                AiSemanticScore: payload.AiSemanticScore,
                Summary: summary,
                ProcessingTimeMs: payload.ProcessingTimeMs,
                ReasonCodes: reasonCodes,
                RawResponseJson: rawJson,
                Violations: violations,
                PriceWarningMessage: priceWarningMsg);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            LogAmeException(ex, request.ListingId, request.CorrelationId);
            return AmeModerationResponse.FallbackManualReview("AME_UNAVAILABLE_FALLBACK_MANUAL", ex.Message);
        }
    }

    private static ModerationDecision ParseDecision(string? decision)
    {
        if (string.Equals(decision, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            return ModerationDecision.Approved;
        }

        if (string.Equals(decision, "Rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "REJECT", StringComparison.OrdinalIgnoreCase))
        {
            return ModerationDecision.Rejected;
        }

        return ModerationDecision.ManualReview;
    }

    private sealed record FastApiViolationPayload(
        string? Rule,
        string? Severity,
        string? Message);

    private sealed record FastApiModerationPayload(
        string? Decision,
        decimal? Score,
        decimal TotalRiskScore,
        decimal KeywordRiskScore,
        decimal PriceAnomalyScore,
        decimal CompletenessScore,
        decimal DuplicateScore,
        decimal? AiSemanticScore,
        string? Summary,
        int ProcessingTimeMs,
        IReadOnlyList<string>? ReasonCodes,
        IReadOnlyList<FastApiViolationPayload>? Violations,
        string? PriceWarningMessage);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "AME is disabled in configuration. Falling back to ManualReview for ListingId={ListingId}, CorrelationId={CorrelationId}.")]
    private partial void LogAmeDisabled(long listingId, Guid correlationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "AME service returned non-success HTTP {StatusCode} for ListingId={ListingId}, CorrelationId={CorrelationId}. Falling back to ManualReview.")]
    private partial void LogAmeHttpError(long listingId, int statusCode, Guid correlationId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "AME service returned an empty JSON payload for ListingId={ListingId}, CorrelationId={CorrelationId}. Falling back to ManualReview.")]
    private partial void LogAmeEmptyPayload(long listingId, Guid correlationId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Failed or timed out calling AME service for ListingId={ListingId}, CorrelationId={CorrelationId}. Falling back to ManualReview.")]
    private partial void LogAmeException(Exception exception, long listingId, Guid correlationId);
}
