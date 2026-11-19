using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using PropNest.Application.Listings;
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

                case "ListingExpired":
                    await PublishDeserializedAsync<ListingExpiredEvent>(message, cancellationToken);
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

        [LoggerMessage(Level = LogLevel.Information, Message = "MassTransitOutboxPublisher: Dispatching outbox event '{EventType}' (OutboxId: {OutboxId}, CorrelationId: {CorrelationId}) to RabbitMQ.")]
        private static partial void LogPublishing(ILogger logger, string eventType, Guid outboxId, Guid? correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "MassTransitOutboxPublisher: Unknown outbox event type '{EventType}' for OutboxId: {OutboxId}. Skipped.")]
        private static partial void LogUnknownMessageType(ILogger logger, string eventType, Guid outboxId);

        [LoggerMessage(Level = LogLevel.Information, Message = "MassTransitOutboxPublisher: Successfully published '{EventType}' (OutboxId: {OutboxId}, CorrelationId: {CorrelationId}) to RabbitMQ.")]
        private static partial void LogPublished(ILogger logger, string eventType, Guid outboxId, Guid? correlationId);
    }
}
