using Microsoft.EntityFrameworkCore;
using PropNest.Application.Abstractions;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;
using PropNest.Domain.Wallets;
using PropNest.Domain.Workflows;

namespace PropNest.Infrastructure.Persistence;

public sealed class PropNestDbContext(DbContextOptions<PropNestDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingHistory> ListingHistories => Set<ListingHistory>();
    public DbSet<ListingModerationReview> ListingModerationReviews => Set<ListingModerationReview>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PropNestDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
