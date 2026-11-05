# -*- coding: utf-8 -*-
"""Real code snippets taken from the PropNest codebase (trimmed, identifiers kept verbatim)."""

SNIPPETS = {}

# src/PropNest.Domain/Wallets/Wallet.cs
SNIPPETS["wallet"] = r'''
public sealed class Wallet : AuditableEntity
{
    public long WalletId { get; private set; }
    public long UserId { get; private set; }
    public decimal MainBalance { get; private set; }
    public decimal PromoBalance { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    public void CreditMain(decimal amount)
    {
        EnsurePositive(amount);
        MainBalance += amount;
        Touch();
    }

    public (decimal PromoUsed, decimal MainUsed) Charge(decimal amount)
    {
        EnsurePositive(amount);
        if (MainBalance + PromoBalance < amount)
        {
            throw new InvalidOperationException("Insufficient wallet balance.");
        }

        var promoUsed = Math.Min(PromoBalance, amount);
        var mainUsed = amount - promoUsed;
        PromoBalance -= promoUsed;
        MainBalance -= mainUsed;
        Touch();
        return (promoUsed, mainUsed);
    }

    public void Refund(decimal amount)
    {
        EnsurePositive(amount);
        MainBalance += amount;
        Touch();
    }
}
'''

# src/PropNest.Infrastructure/Persistence/Configurations/WalletConfiguration.cs
SNIPPETS["wallet_config"] = r'''
public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets");
        builder.HasKey(x => x.WalletId);
        builder.Property(x => x.MainBalance).HasPrecision(18, 2);
        builder.Property(x => x.PromoBalance).HasPrecision(18, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
'''

# src/PropNest.Application/Wallets/WalletService.cs
SNIPPETS["topup"] = r'''
public async Task<WalletDto> TopUpAsync(TopUpWalletCommand command,
    CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(command.Amount);

    var wallet = await walletRepository.GetByUserIdAsync(command.UserId, cancellationToken)
        ?? throw new InvalidOperationException($"Wallet for user {command.UserId} not found.");

    var balanceBefore = wallet.MainBalance;
    wallet.CreditMain(command.Amount);
    var balanceAfter = wallet.MainBalance;

    var transaction = new WalletTransaction(
        walletId: wallet.WalletId,
        correlationId: command.CorrelationId ?? Guid.NewGuid(),
        type: WalletTransactionType.TopUp,
        amount: command.Amount,
        balanceBefore: balanceBefore,
        balanceAfter: balanceAfter,
        description: command.Description ?? "Nạp tiền vào ví"
        );

    await walletRepository.AddTransactionAsync(transaction, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return WalletMappingExtensions.ToDto(wallet);
}
'''

# src/PropNest.Infrastructure/Persistence/Repositories/WalletRepository.cs
SNIPPETS["transactions"] = r'''
public async Task<IEnumerable<WalletTransaction>> GetTransactionsByUserIdAsync(long userId,
    int? page = 1, WalletTransactionType? type = null, CancellationToken cancellationToken = default)
{
    var wallet = await dbContext.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken);
    if(wallet == null)
    {
        return Enumerable.Empty<WalletTransaction>();
    }
    const int pageSize = 10;
    var currentPage =Math.Max(1, page ?? 1);

    var transactions = dbContext.WalletTransactions
        .AsNoTracking()
        .Where(t => t.WalletId == wallet.WalletId);
    if (type.HasValue)
    {
        transactions = transactions.Where(t => t.TransactionType == type.Value);
    }

    return await transactions
        .OrderByDescending(t => t.CreatedAt)
        .Skip((currentPage - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);
}
'''

# src/PropNest.Api/Controllers/WalletsController.cs
SNIPPETS["wallet_api"] = r'''
[Route("api/v1/wallets")]
[ApiController]
public class WalletsController(ICurrentUser currentUser, IWalletService walletService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<WalletDto>> GetMyWallet(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException(
            "A valid user identifier claim is required.");
        try
        {
            var walletDto = await walletService.GetBalanceByUserIdAsync(userId, cancellationToken);
            return Ok(walletDto);
        }
        catch (Exception)
        {
            return NotFound("Wallet not found for the current user.");
        }
    }

    [HttpGet("me/transactions")]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetMyTransactions(
        [FromQuery] int? page, [FromQuery] WalletTransactionType? type,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException(
            "A valid user identifier claim is required.");
        var transactions = await walletService.GetTransactionsByUserIdAsync(
            userId, page, type, cancellationToken);
        return Ok(transactions);
    }
}
'''

# src/PropNest.Application/Workflows/WorkflowService.cs
SNIPPETS["start_purchase"] = r'''
public async Task<WorkflowDto> StartPurchaseAsync(PurchasePackageCommand command,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
    {
        throw new ArgumentException("Idempotency-Key is required.", nameof(command));
    }

    var existing = await workflowRepository.GetIdempotencyRequestAsync(
        command.UserId, command.IdempotencyKey, cancellationToken);
    if (existing is not null)
    {
        if (!string.Equals(existing.RequestHash, command.RequestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Idempotency-Key has already been used with a different request.");
        }
        var replay = await workflowRepository.GetByCorrelationIdAsync(existing.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException("The idempotent request does not have an associated workflow.");
        return ToDto(replay);
    }

    var correlationId = Guid.NewGuid();
    var workflow = new WorkflowInstance(correlationId, command.ListingId, command.UserId,
        command.PackageCode, command.ChargeAmount);
    // ... tạo IdempotencyRequest (hết hạn sau 24 giờ) và payload JSON

    await workflowRepository.AddAsync(workflow, cancellationToken);
    await workflowRepository.AddIdempotencyRequestAsync(request, cancellationToken);
    await workflowRepository.AddOutboxMessageAsync(
        new OutboxMessage("PurchasePackageRequested", payload, correlationId), cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return ToDto(workflow);
}
'''

# src/PropNest.Infrastructure/Services/MassTransitOutboxPublisher.cs
SNIPPETS["outbox_publisher"] = r'''
public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
{
    LogPublishing(_logger, message.Type, message.OutboxId, message.CorrelationId);

    switch (message.Type)
    {
        case "PurchasePackageRequested":
            var purchaseRequested = JsonSerializer.Deserialize<PurchasePackageRequested>(
                message.PayloadJson, JsonOptions);
            if(purchaseRequested is null)
            {
                throw new InvalidOperationException(
                    $"Không thể deserialize payload cho sự kiện {message.Type} (OutboxId: {message.OutboxId}).");
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
'''

# src/PropNest.Infrastructure/Workflows/PurchasePackageSaga.cs
SNIPPETS["saga"] = r'''
// Step 1: khởi tạo saga -> trừ tiền
Initially(
    When(PurchasePackageRequestedEvent)
        .Then(context =>
        {
            context.Saga.CorrelationId = context.Message.CorrelationId;
            context.Saga.ListingId = context.Message.ListingId;
            context.Saga.UserId = context.Message.UserId;
            context.Saga.PackageCode = context.Message.PackageCode;
            context.Saga.ChargeAmount = context.Message.ChargeAmount;
        })
        .TransitionTo(DeductingWallet)
        .Publish(context => new DeductWalletCommand(
            context.Saga.CorrelationId, context.Saga.UserId, context.Saga.ChargeAmount))
);

// Step 2: trong lúc trừ tiền
During(DeductingWallet,
    When(WalletDeductedEvent)
        .TransitionTo(UpgradingListing)
        .Publish(context => new UpgradeListingCommand(
            context.Saga.CorrelationId, context.Saga.ListingId, context.Saga.PackageCode)),
    When(WalletDeductionFailedEvent)
        .Then(context => context.Saga.ErrorMessage = context.Message.Reason)
        .TransitionTo(Failed)
);

// Step 3: nâng cấp tin
During(UpgradingListing,
    When(ListingUpgradedEvent)
        .TransitionTo(Completed),
    When(ListingUpgradeFailedEvent)
        .TransitionTo(Compensating)
        .Publish(context => new RefundWalletCommand(
            context.Saga.CorrelationId, context.Saga.UserId, context.Saga.ChargeAmount))
);

// Step 4: hoàn trả tiền
During(Compensating,
    When(WalletRefundedEvent)
        .TransitionTo(Compensated)
);
'''

# src/PropNest.Infrastructure/Consumers/DeductWalletConsumer.cs
SNIPPETS["deduct"] = r'''
public async Task Consume(ConsumeContext<DeductWalletCommand> context)
{
    var commnad = context.Message;

    // Check Idempotency
    var alreadyProcessed = await _dbContext.WalletTransactions.AnyAsync(t =>
        t.CorrelationId == commnad.CorrelationId && t.TransactionType == WalletTransactionType.Charge,
        context.CancellationToken);
    if (alreadyProcessed)
    {
        await context.Publish(new WalletDeducted(commnad.CorrelationId, commnad.UserId, commnad.Amount));
        return;
    }

    //lấy thông tin ví
    var wallet = await _walletRepository.GetByUserIdAsync(commnad.UserId, context.CancellationToken);
    // ... nếu ví không tồn tại -> publish WalletDeductionFailed

    //trừ tiền
    var totalBalanceBefore = wallet.MainBalance + wallet.PromoBalance;
    try
    {
        wallet.Charge(commnad.Amount);
    }
    catch(InvalidOperationException ex)
    {
        await context.Publish(new WalletDeductionFailed(commnad.CorrelationId, commnad.UserId, ex.Message));
        return;
    }

    var totalBalanceAfter = wallet.MainBalance + wallet.PromoBalance;
    var transaction = new WalletTransaction(
        walletId: wallet.WalletId,
        correlationId: commnad.CorrelationId,
        type: WalletTransactionType.Charge,
        amount: commnad.Amount,
        balanceBefore: totalBalanceBefore,
        balanceAfter: totalBalanceAfter,
        description: $"Thanh toán gói dịch vụ tin đăng ({commnad.Amount:N0}đ)");

    await _walletRepository.AddTransactionAsync(transaction, context.CancellationToken);
    await _unitOfWork.SaveChangesAsync(context.CancellationToken);

    await context.Publish(new WalletDeducted(commnad.CorrelationId, commnad.UserId, commnad.Amount));
}
'''

# src/PropNest.Infrastructure/Consumers/UpgradeListingConsumer.cs
SNIPPETS["upgrade"] = r'''
// tính start - end
var now = DateTimeOffset.UtcNow;
var isStillActive = listing.EndDate.HasValue && listing.EndDate.Value > now;

//nếu tin đăng còn hạn thì cộng dồn ngày nối tiếp, nếu hết hạn thì tính từ thời điểm hiện tại
var startDate = (isStillActive && listing.StartDate.HasValue) ? listing.StartDate.Value : now;
var baseDate = isStillActive ? listing.EndDate!.Value : now;
var endDate = baseDate.AddDays(package.DurationDays);

//nâng cấp tin và ghi history
try
{
    listing.UpgradePackage(command.PackageCode, startDate, endDate);

    var history = ListingHistory.CreateEvent(
        listing.ListingId,
        listing.OwnerUserId,
        "UpgradePackage",
        $"Nâng cấp gói {command.PackageCode} (+{package.DurationDays} ngày, " +
        $"HSD đến: {endDate:yyyy-MM-dd HH:mm}) - CorrelationId: {command.CorrelationId}");

    _listingRepository.AddHistory( history );
    await _unitOfWork.SaveChangesAsync(context.CancellationToken);
}
catch (Exception ex)
{
    await context.Publish(new ListingUpgradeFailed(command.CorrelationId, command.ListingId, ex.Message));
    return;
}

await context.Publish(new ListingUpgraded(command.CorrelationId, command.ListingId, command.PackageCode));
'''

# src/PropNest.Infrastructure/Consumers/RefundWalletConsumer.cs
SNIPPETS["refund"] = r'''
public async Task Consume(ConsumeContext<RefundWalletCommand> context)
{
    var command = context.Message;

    // check idempotency
    var alreadyProcessed = await _dbContext.WalletTransactions.AnyAsync(
        w=>w.CorrelationId == command.CorrelationId && w.TransactionType == WalletTransactionType.Refund,
        context.CancellationToken);
    if (alreadyProcessed)
    {
        await context.Publish(new WalletRefunded(command.CorrelationId, command.UserId, command.Amount));
        return;
    }

    //lấy thông tin ví
    var wallet = await _walletRepository.GetByUserIdAsync(command.UserId, context.CancellationToken);
    if (wallet is null)
    {
        throw new InvalidOperationException(
            $"Không tìm thấy ví của người dùng có ID {command.UserId} để hoàn tiền.");
    }

    //hoàn tiền
    var totalBalanceBefore = wallet.MainBalance + wallet.PromoBalance;
    wallet.Refund(command.Amount);
    var totalBalanceAfter = wallet.MainBalance + wallet.PromoBalance;

    var transaction = new WalletTransaction(
        walletId: wallet.WalletId,
        correlationId: command.CorrelationId,
        type: WalletTransactionType.Refund,
        amount: command.Amount,
        balanceBefore: totalBalanceBefore,
        balanceAfter: totalBalanceAfter,
        description: $"Hoàn tiền giao dịch mua gói tin đăng thất bại ({command.Amount:N0}đ)");
    await _walletRepository.AddTransactionAsync(transaction, context.CancellationToken);
    await _unitOfWork.SaveChangesAsync(context.CancellationToken);

    await context.Publish(new WalletRefunded(command.CorrelationId, command.UserId, command.Amount));
}
'''

# src/PropNest.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
SNIPPETS["masstransit"] = r'''
// CẤU HÌNH MASSTRANSIT + RABBITMQ
services.AddMassTransit(busConfig =>
{
    busConfig.SetKebabCaseEndpointNameFormatter();

    // đăng ký saga
    busConfig.AddSagaStateMachine<PurchasePackageSaga, PurchasePackageSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<PropNestDbContext>();
            r.UseSqlServer();
        });

    // đăng ký consumers
    busConfig.AddConsumer<DeductWalletConsumer>();
    busConfig.AddConsumer<UpgradeListingConsumer>();
    busConfig.AddConsumer<RefundWalletConsumer>();
    busConfig.UsingRabbitMq((context, cfg) =>
    {
        var host = configuration["RabbitMq:Host"] ?? "localhost";
        // ... port, virtualHost, username, password
        cfg.Host(host, port, virtualHost, h =>
        {
            h.Username(username);
            h.Password(password);
        });
        cfg.UseConsumeFilter(typeof(InboxDeduplicationFilter<>), context);
        cfg.ConfigureEndpoints(context);
    });
});
'''

# web/PropNest.Web.Seller/Controllers/WalletController.cs
SNIPPETS["seller_wallet"] = r'''
[Route("Wallet")]
[Route("seller/wallet")]
public class WalletController : Controller
{
    private readonly ISellerPropertyService _sellerService;

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "Wallet";
        var model = await _sellerService.GetWalletAsync();
        return View(model);
    }

    [HttpPost("TopUp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopUp([FromForm] TopUpInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Số tiền nạp không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var success = await _sellerService.TopUpWalletAsync(input.Amount);
        if (success)
        {
            TempData["SuccessMessage"] = $"Nạp thành công {FormatHelper.FormatVnd(input.Amount)} vào số dư chính!";
        }

        return RedirectToAction(nameof(Index));
    }
}
'''
