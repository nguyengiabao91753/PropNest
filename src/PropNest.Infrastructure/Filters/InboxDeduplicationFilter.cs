using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PropNest.Domain.Workflows;
using PropNest.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace PropNest.Infrastructure.Filters
{
    public sealed partial class InboxDeduplicationFilter<T> : IFilter<ConsumeContext<T>>
         where T : class
    {
        public void Probe(ProbeContext context) => context.CreateFilterScope("inbox-deduplication");
        public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            // Nếu message không có MessageId thì không áp dụng deduplication
            if (!context.MessageId.HasValue)
            {
                await next.Send(context);
                return;
            }
            var messageId = context.MessageId.Value;
            var serviceProvider = context.GetPayload<IServiceProvider>();
            var dbContext = serviceProvider.GetRequiredService<PropNestDbContext>();
            var logger = serviceProvider.GetService<ILogger<InboxDeduplicationFilter<T>>>();
            // Lấy tên Consumer đang xử lý (hoặc fallback về tên kiểu Message)
            var consumerName = typeof(T).Name;

            // Try to detect a ConsumerConsumeContext<TConsumer, TMessage> implemented by the runtime context.
            // Use reflection to read the Consumer property when available.
            var consumerConsumeContextGeneric = typeof(ConsumerConsumeContext<,>);
            var consumerInterface = context.GetType().GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == consumerConsumeContextGeneric);
            if (consumerInterface is not null)
            {
                var consumerProperty = consumerInterface.GetProperty("Consumer");
                if (consumerProperty is not null)
                {
                    var consumer = consumerProperty.GetValue(context);
                    if (consumer is not null)
                    {
                        consumerName = consumer.GetType().Name;
                    }
                }
            }

            // 1. Kiểm tra xem (ConsumerName, MessageId) đã từng được xử lý thành công chưa
            var alreadyProcessed = await dbContext.InboxMessages.AnyAsync(
                x => x.ConsumerName == consumerName && x.MessageId == messageId,
                context.CancellationToken);
            if (alreadyProcessed)
            {
                if (logger is not null)
                {
                    LogDuplicateMessage(logger, consumerName, messageId);
                }
                // Bỏ qua: Không gọi next.Send() để tránh xử lý trùng lặp
                return;
            }
            // 2. Chuyển tiếp cho Consumer xử lý nghiệp vụ
            await next.Send(context);
            // 3. Sau khi Consumer chạy thành công (không có lỗi), ghi nhận vào Inbox
            try
            {
                await dbContext.InboxMessages.AddAsync(
                    new InboxMessage(messageId, consumerName),
                    context.CancellationToken);
                await dbContext.SaveChangesAsync(context.CancellationToken);
            }
            catch (DbUpdateException)
            {
                // Bắt xung đột Unique Constraint nếu có 2 luồng xử lý cùng một lúc (Race Condition)
                if (logger is not null)
                {
                    LogDuplicateMessage(logger, consumerName, messageId);
                }
            }
        }
        [LoggerMessage(Level = LogLevel.Warning, Message = "InboxDeduplication: Message {MessageId} was already processed by {ConsumerName}. Skipping duplicate execution.")]
        private static partial void LogDuplicateMessage(ILogger logger, string consumerName, Guid messageId);
    }
}
