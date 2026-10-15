using MassTransit;
using Microsoft.Extensions.Logging;
using PropNest.Application.Workflows;
using PropNest.Domain.Workflows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
                    var purchaseRequested = JsonSerializer.Deserialize<PurchasePackageRequested>(message.PayloadJson, JsonOptions);
                    if(purchaseRequested is null)
                    {
                        throw new InvalidOperationException($"Không thể deserialize payload cho sự kiện {message.Type} (OutboxId: {message.OutboxId}).");
                    }

                    // bắn event
                    await _publishEndpoint.Publish(purchaseRequested, cancellationToken);
                    break;
                default:
                    LogUnknownMessageType(_logger, message.Type, message.OutboxId);
                    break;
            }
            LogPublished(_logger, message.Type, message.OutboxId, message.CorrelationId);
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "MassTransitOutboxPublisher: Dispatching outbox event '{EventType}' (OutboxId: {OutboxId}, CorrelationId: {CorrelationId}) to RabbitMQ.")]
        private static partial void LogPublishing(ILogger logger, string eventType, Guid outboxId, Guid? correlationId);
        [LoggerMessage(Level = LogLevel.Warning, Message = "MassTransitOutboxPublisher: Unknown outbox event type '{EventType}' for OutboxId: {OutboxId}. Skipped.")]
        private static partial void LogUnknownMessageType(ILogger logger, string eventType, Guid outboxId);
        [LoggerMessage(Level = LogLevel.Information, Message = "MassTransitOutboxPublisher: Successfully published '{EventType}' (OutboxId: {OutboxId}, CorrelationId: {CorrelationId}) to RabbitMQ.")]
        private static partial void LogPublished(ILogger logger, string eventType, Guid outboxId, Guid? correlationId);
    }
}
