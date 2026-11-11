using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Services
{
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
                return new AmeModerationResponse(
                    Decision: ParseDecision(payload.Decision),
                    TotalRiskScore: payload.TotalRiskScore,
                    KeywordRiskScore: payload.KeywordRiskScore,
                    PriceAnomalyScore: payload.PriceAnomalyScore,
                    CompletenessScore: payload.CompletenessScore,
                    DuplicateScore: payload.DuplicateScore,
                    AiSemanticScore: payload.AiSemanticScore,
                    Summary: string.IsNullOrWhiteSpace(payload.Summary)
                        ? "Đã hoàn tất chấm điểm tự động."
                        : payload.Summary,
                    ProcessingTimeMs: payload.ProcessingTimeMs,
                    ReasonCodes: payload.ReasonCodes ?? [],
                    RawResponseJson: rawJson);
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
        private sealed record FastApiModerationPayload(
            string? Decision,
            decimal TotalRiskScore,
            decimal KeywordRiskScore,
            decimal PriceAnomalyScore,
            decimal CompletenessScore,
            decimal DuplicateScore,
            decimal? AiSemanticScore,
            string? Summary,
            int ProcessingTimeMs,
            IReadOnlyList<string>? ReasonCodes);
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
}
