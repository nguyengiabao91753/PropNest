using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using PropNest.Application.Listings;
using PropNest.Application.Listings.Search;
using PropNest.Application.Workflows;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Services
{
    public partial class MassTransitOutboxPublisher : IOutboxMessagePublisher
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<MassTransitOutboxPublisher> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public MassTransitOutboxPublisher(
           IPublishEndpoint publishEndpoint,
           ILogger<MassTransitOutboxPublisher> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            LogPublishing(_logger, message.Type, message.OutboxId, message.CorrelationId);

            switch (message.Type)
            {
                case "PurchasePackageRequested":
                    await PublishDeserializedAsync<PurchasePackageRequested>(message, cancellationToken);
                    break;

                case "ListingSubmitted":
                    await PublishDeserializedAsync<ListingSubmitted>(message, cancellationToken);
                    break;

                case "ListingPublished":
                    await PublishDeserializedAsync<ListingPublishedEvent>(message, cancellationToken);
                    break;

                case "ListingApprovedPendingPayment":
                    await PublishDeserializedAsync<ListingApprovedPendingPaymentEvent>(message, cancellationToken);
                    break;

                case "ListingRejected":
                    await PublishDeserializedAsync<ListingRejectedEvent>(message, cancellationToken);
                    break;
                case "ListingSubmitted":
                    var listingSubmitted = JsonSerializer.Deserialize<ListingSubmitted>(message.PayloadJson, JsonOptions) 
                        ?? throw new InvalidOperationException($"Không thể deserialize payload cho sự kiện {message.Type} (OutboxId: {message.OutboxId}).");
                    await _publishEndpoint.Publish(listingSubmitted, cancellationToken);
                    break;

                case "ListingExpired":
                    await PublishDeserializedAsync<ListingExpiredEvent>(message, cancellationToken);
                case "ListingPublished":
                case "ListingApprovedPendingPayment":
                case "ListingRejected":
                case "ListingExpired":
                case "ListingPackageDowngraded":
                case "ListingPackageUpdated":
                case "ListingSearchSyncRequested":
                case "ListingHidden":
                case "ListingRestored":
                case "ListingDeleted":
                case "ListingMediaUpdated":
                case "ListingBumped":
                    var syncRequested = TryCreateSearchSyncEvent(message);
                    if (syncRequested is not null)
                    {
                        await _publishEndpoint.Publish(syncRequested, cancellationToken);
                    }
                    break;

                default:
                    LogUnknownMessageType(_logger, message.Type, message.OutboxId);
                    return;
            }

            LogPublished(_logger, message.Type, message.OutboxId, message.CorrelationId);
        }

        private async Task PublishDeserializedAsync<TEvent>(OutboxMessage message, CancellationToken cancellationToken)
            where TEvent : class
        {
            var payload = JsonSerializer.Deserialize<TEvent>(message.PayloadJson, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Không thể deserialize payload cho sự kiện {message.Type} (OutboxId: {message.OutboxId}).");

            await _publishEndpoint.Publish(payload, cancellationToken);
        }

        private static ListingSearchSyncRequested? TryCreateSearchSyncEvent(OutboxMessage message)
        {
            try
            {
                using var doc = JsonDocument.Parse(message.PayloadJson);
                long listingId = 0;
                if (doc.RootElement.TryGetProperty("listingId", out var idElem) ||
                    doc.RootElement.TryGetProperty("ListingId", out idElem))
                {
                    if (idElem.ValueKind == JsonValueKind.Number && idElem.TryGetInt64(out var id))
                    {
                        listingId = id;
                    }
                    else if (idElem.ValueKind == JsonValueKind.String && long.TryParse(idElem.GetString(), out var parsedId))
                    {
                        listingId = parsedId;
                    }
                }

                if (listingId > 0)
                {
                    return new ListingSearchSyncRequested(
                        listingId,
                        message.Type,
                        message.OccurredOn,
                        message.CorrelationId);
                }
            }
            catch (JsonException)
            {
                // Bỏ qua payload lỗi cấu trúc JSON
            }

            return null;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "MassTransitOutboxPublisher: Dispatching outbox event '{EventType}' (OutboxId: {OutboxId}, CorrelationId: {CorrelationId}) to RabbitMQ.")]
        private static partial void LogPublishing(ILogger logger, string eventType, Guid outboxId, Guid? correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "MassTransitOutboxPublisher: Unknown outbox event type '{EventType}' for OutboxId: {OutboxId}. Skipped.")]
        private static partial void LogUnknownMessageType(ILogger logger, string eventType, Guid outboxId);

        [LoggerMessage(Level = LogLevel.Information, Message = "MassTransitOutboxPublisher: Successfully published '{EventType}' (OutboxId: {OutboxId}, CorrelationId: {CorrelationId}) to RabbitMQ.")]
        private static partial void LogPublished(ILogger logger, string eventType, Guid outboxId, Guid? correlationId);
    }
}
