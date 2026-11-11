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
    private const string AmeModelInfo = "PropNest-AME-v1";

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

        // 2. Lấy danh sách ContentRules đang active và MarketPriceBenchmark tương ứng từ DB
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

        var matchingBenchmarks = await _dbContext.MarketPriceBenchmarks
            .AsNoTracking()
            .Where(b => b.CityCode == listing.City &&
                        b.DistrictCode == listing.District &&
                        b.PropertyType == listing.PropertyType &&
                        b.ListingType == listing.ListingType)
            .ToListAsync(context.CancellationToken);

        var benchmarkEntity = matchingBenchmarks
            .OrderByDescending(b => b.EffectiveFrom)
            .ThenByDescending(b => b.BenchmarkId)
            .FirstOrDefault();

        var benchmarkDto = benchmarkEntity is null
            ? null
            : new AmePriceBenchmarkDto(
                benchmarkEntity.PropertyType,
                benchmarkEntity.ListingType,
                benchmarkEntity.CityCode,
                benchmarkEntity.DistrictCode,
                benchmarkEntity.MinPricePerSqm,
                benchmarkEntity.AvgPricePerSqm,
                benchmarkEntity.MaxPricePerSqm);

        // 3. Gọi sang dịch vụ Python AME (có cơ chế Fallback an toàn sang ManualReview)
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
            PriceBenchmark: benchmarkDto);

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
        });

        var reasonsJson = JsonSerializer.Serialize(new
        {
            ameResponse.Summary,
            ameResponse.ReasonCodes
        });

        var warningsJson = ameResponse.ReasonCodes.Count > 0
            ? JsonSerializer.Serialize(ameResponse.ReasonCodes)
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
                await ApplyAutoApproveAsync(listing, reasonsJson, ameResponse.Summary, context.CancellationToken);
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

    private async Task ApplyAutoApproveAsync(
        Listing listing,
        string reasonsJson,
        string summary,
        CancellationToken cancellationToken)
    {
        var package = await _listingRepository.GetPackageByCodeAsync(listing.PackageCode, cancellationToken);
        var durationDays = package is { IsActive: true, DurationDays: > 0 } ? package.DurationDays : 30;

        var startDate = DateTimeOffset.UtcNow;
        var endDate = startDate.AddDays(durationDays);

        listing.Approve(startDate, endDate);

        _listingRepository.AddModerationReview(new ListingModerationReview(
            listing.ListingId,
            AmeSource,
            ModerationDecision.Approved,
            reasonsJson,
            reviewerUserId: null));

        _listingRepository.AddHistory(ListingHistory.CreateEvent(
            listing.ListingId,
            actorUserId: null,
            "AutoApprove",
            summary));

        var publishedPayload = JsonSerializer.Serialize(new
        {
            listing.ListingId,
            listing.OwnerUserId,
            listing.PackageCode,
            listing.StartDate,
            listing.EndDate
        });
        _listingRepository.AddOutboxMessage(new OutboxMessage("ListingPublished", publishedPayload));
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
        });
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
