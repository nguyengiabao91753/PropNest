using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Persistence;

namespace PropNest.Infrastructure.Filters
{
    public sealed partial class InboxDeduplicationFilter<T> : IFilter<ConsumeContext<T>>
         where T : class
    {
        public void Probe(ProbeContext context) => context.CreateFilterScope("inbox-deduplication");

        public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            if (!context.MessageId.HasValue)
            {
                await next.Send(context);
                return;
            }

            var messageId = context.MessageId.Value;
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<InboxDeduplicationFilter<T>>>();
            var consumerName = ResolveConsumerName(context);

            var alreadyProcessed = await dbContext.InboxMessages
                .AsNoTracking()
                .AnyAsync(
                    x => x.ConsumerName == consumerName && x.MessageId == messageId,
                    context.CancellationToken);

            if (alreadyProcessed)
            {
                if (logger is not null)
                {
                    LogDuplicateMessage(logger, consumerName, messageId);
                }
                return;
            }

            await next.Send(context);

            try
            {
                var inboxEntry = new InboxMessage(messageId, consumerName);
                await dbContext.InboxMessages.AddAsync(inboxEntry, context.CancellationToken);
                await dbContext.SaveChangesAsync(context.CancellationToken);
            }
            catch (DbUpdateException)
            {
                // Detach failed entity so subsequent scoped DB operations are not poisoned
                foreach (var entry in dbContext.ChangeTracker.Entries<InboxMessage>()
                    .Where(e => e.Entity.MessageId == messageId && e.Entity.ConsumerName == consumerName)
                    .ToList())
                {
                    entry.State = EntityState.Detached;
                }

                if (logger is not null)
                {
                    LogDuplicateMessage(logger, consumerName, messageId);
                }
            }
        }

        private static string ResolveConsumerName(ConsumeContext<T> context)
        {
            var interfaces = context.GetType().GetInterfaces();

            var consumerInterface = interfaces.FirstOrDefault(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ConsumerConsumeContext<,>));
            if (consumerInterface is not null)
            {
                var consumer = consumerInterface.GetProperty("Consumer")?.GetValue(context);
                if (consumer is not null)
                {
                    return consumer.GetType().Name;
                }
            }

            var sagaInterface = interfaces.FirstOrDefault(
                i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(SagaConsumeContext<,>));
            if (sagaInterface is not null)
            {
                var sagaType = sagaInterface.GetGenericArguments()[0];
                return $"{sagaType.Name}:{typeof(T).Name}";
            }

            return typeof(T).Name;
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "InboxDeduplication: Message {MessageId} was already processed by {ConsumerName}. Skipping duplicate execution.")]
        private static partial void LogDuplicateMessage(ILogger logger, string consumerName, Guid messageId);
    }
}
