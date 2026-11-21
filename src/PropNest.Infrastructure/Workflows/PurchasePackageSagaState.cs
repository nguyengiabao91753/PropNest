using MassTransit;
using MassTransit.Middleware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PropNest.Infrastructure.Workflows
{
    public class PurchasePackageSagaState : SagaStateMachineInstance, ISagaVersion
    {
        public Guid CorrelationId { get; set; }
        public string CurrentState { get; set; } = string.Empty;
        public int Version { get; set; }

        public long ListingId { get; set; }
        public long UserId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public decimal ChargeAmount { get; set; }
        public decimal PromoUsed { get; set; }
        public decimal MainUsed { get; set; }
        public string? ErrorMessage { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
