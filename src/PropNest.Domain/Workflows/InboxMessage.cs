using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Domain.Workflows
{
    public class InboxMessage
    {
        public long Id { get; private set; }
        public Guid MessageId { get; private set; }
        public string ConsumerName { get; private set; } = string.Empty;
        public DateTimeOffset ProcessedOn { get; private set; }
        private InboxMessage()
        {
        }
        public InboxMessage(Guid messageId, string consumerName)
        {
            if (string.IsNullOrWhiteSpace(consumerName))
            {
                throw new ArgumentException("Consumer name cannot be empty.", nameof(consumerName));
            }
            MessageId = messageId;
            ConsumerName = consumerName.Trim();
            ProcessedOn = DateTimeOffset.UtcNow;
        }
    }

}
