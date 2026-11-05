# -*- coding: utf-8 -*-
"""
Chứa toàn bộ mã nguồn C# thực tế từ codebase PropNest.
100% đầy đủ, chính xác, không dùng '...' hay lược bỏ.
Mỗi snippet là trọn vẹn 1 class, 1 interface hoặc 1 method hoàn chỉnh.
Đã chia tách Snippet 6 và 8 thành 2 phần tiếp nối tự nhiên để dễ theo dõi.
Từ ngữ chuẩn xác: hoàn tiền (thay cho bồi hoàn), State Machine / máy trạng thái (thay cho cỗ máy).
"""

# =============================================================================
# 1. Domain Entity Wallet (trọn vẹn toàn bộ file Wallet.cs, dòng 1-62)
# =============================================================================
CODE_WALLET_FULL = '''public sealed class Wallet : AuditableEntity
{
    private Wallet()
    {
    }

    public Wallet(long userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);

        UserId = userId;
    }

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

    private static void EnsurePositive(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
    }
}'''

# =============================================================================
# 2. Phương thức TopUpAsync trong WalletService.cs (trọn vẹn dòng 43-67)
# =============================================================================
CODE_TOPUP_FULL = '''public async Task<WalletDto> TopUpAsync(TopUpWalletCommand command, CancellationToken cancellationToken = default)
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
}'''

# =============================================================================
# 3. Phương thức GetTransactionsByUserIdAsync trong WalletRepository.cs (trọn vẹn dòng 34-57)
# =============================================================================
CODE_TRANSACTIONS_FULL = '''public async Task<IEnumerable<WalletTransaction>> GetTransactionsByUserIdAsync(
    long userId, int? page = 1, WalletTransactionType? type = null, CancellationToken cancellationToken = default)
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
}'''

# =============================================================================
# 4. WalletsController.cs - Các API xem số dư và lịch sử giao dịch (trọn vẹn dòng 38-66)
# =============================================================================
CODE_WALLET_CONTROLLER_FULL = '''[HttpGet("me")]
public async Task<ActionResult<WalletDto>> GetMyWallet(CancellationToken cancellationToken)
{
    var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
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
    [FromQuery] int? page, [FromQuery] WalletTransactionType? type, CancellationToken cancellationToken)
{
    var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("A valid user identifier claim is required.");
    try
    {
        var transactions = await walletService.GetTransactionsByUserIdAsync(userId, page, type, cancellationToken);
        return Ok(transactions);
    }
    catch (Exception)
    {
        return NotFound("No transactions found for the current user.");
    }
}'''

# =============================================================================
# 5. WorkflowService.cs - StartPurchaseAsync (trọn vẹn 100% dòng 11-55)
# =============================================================================
CODE_START_PURCHASE_FULL = '''public async Task<WorkflowDto> StartPurchaseAsync(PurchasePackageCommand command, CancellationToken cancellationToken = default)
{
    if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
    {
        throw new ArgumentException("Idempotency-Key is required.", nameof(command));
    }

    var existing = await workflowRepository.GetIdempotencyRequestAsync(command.UserId, command.IdempotencyKey, cancellationToken);
    if (existing is not null)
    {
        if (!string.Equals(existing.RequestHash, command.RequestHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The Idempotency-Key has already been used with a different request.");
        }

        var replay = await workflowRepository.GetByCorrelationIdAsync(existing.CorrelationId, cancellationToken)
            ?? throw new InvalidOperationException("The idempotent request does not have an associated workflow.");
        return ToDto(replay);
    }

    var correlationId = Guid.NewGuid();
    var workflow = new WorkflowInstance(correlationId, command.ListingId, command.UserId, command.PackageCode, command.ChargeAmount);
    var request = new IdempotencyRequest(
        command.IdempotencyKey,
        command.UserId,
        command.RequestPath,
        command.RequestHash,
        correlationId,
        DateTimeOffset.UtcNow.AddHours(24));
    var payload = JsonSerializer.Serialize(new
    {
        workflow.CorrelationId,
        workflow.ListingId,
        workflow.UserId,
        workflow.PackageCode,
        workflow.ChargeAmount
    });

    await workflowRepository.AddAsync(workflow, cancellationToken);
    await workflowRepository.AddIdempotencyRequestAsync(request, cancellationToken);
    await workflowRepository.AddOutboxMessageAsync(new OutboxMessage("PurchasePackageRequested", payload, correlationId), cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return ToDto(workflow);
}'''

# =============================================================================
# 6a. PurchasePackageSaga.cs - Phần 1: Khởi tạo và trừ tiền ví (dòng 37-100)
# =============================================================================
CODE_SAGA_PART1 = '''public PurchasePackageSaga()
{
    InstanceState(x => x.CurrentState);

    // Khai báo CorrelationId mapping cho các Event
    Event(() => PurchasePackageRequestedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
    Event(() => WalletDeductedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
    Event(() => WalletDeductionFailedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
    Event(() => ListingUpgradedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
    Event(() => ListingUpgradeFailedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));
    Event(() => WalletRefundedEvent, x => x.CorrelateById(m => m.Message.CorrelationId));

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
                context.Saga.CreatedAt = DateTimeOffset.UtcNow;
                context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
            })
            .ThenAsync(async context =>
            {
                await RecordStepAsync(context, WorkflowStatus.DeductingWallet, "DeductWallet", isCompleted: false);
            })
            .TransitionTo(DeductingWallet)
            .Publish(context => new DeductWalletCommand(
                context.Saga.CorrelationId,
                context.Saga.UserId,
                context.Saga.ChargeAmount
            ))
    );

    // Step 2: trong lúc trừ tiền
    During(DeductingWallet,
        When(WalletDeductedEvent)
            .Then(context =>
            {
                context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
            })
            .ThenAsync(async context =>
            {
                await RecordStepAsync(context, WorkflowStatus.WalletDeducted, "DeductWallet", isCompleted: true);
                await RecordStepAsync(context, WorkflowStatus.UpgradingListing, "UpgradeListing", isCompleted: false);
            })
            .TransitionTo(UpgradingListing)
            .Publish(context => new UpgradeListingCommand(
                context.Saga.CorrelationId,
                context.Saga.ListingId,
                context.Saga.PackageCode
            )),
        When(WalletDeductionFailedEvent)
            .Then(context =>
            {
                context.Saga.ErrorMessage = context.Message.Reason;
                context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
            })
            .ThenAsync(async context =>
            {
                await RecordStepAsync(context, WorkflowStatus.Failed, "DeductWallet", isCompleted: false, error: context.Message.Reason);
            })
            .TransitionTo(Failed)
    );'''

# =============================================================================
# 6b. PurchasePackageSaga.cs - Phần 2: Nâng cấp tin đăng và hoàn tiền (dòng 102-152)
# =============================================================================
CODE_SAGA_PART2 = '''    // (Tiếp nối cấu hình PurchasePackageSaga: Step 3 & Step 4)
    // Step 3: nâng cấp tin
    During(UpgradingListing,
        When(ListingUpgradedEvent)
            .Then(context =>
            {
                context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
            })
            .ThenAsync(async context =>
            {
                await RecordStepAsync(context, WorkflowStatus.RecordingHistory, "UpgradeListing", isCompleted: true);
                await RecordStepAsync(context, WorkflowStatus.Completed, "RecordHistory", isCompleted: true);
            })
            .TransitionTo(Completed),

        When(ListingUpgradeFailedEvent)
            .Then(context =>
            {
                context.Saga.ErrorMessage = context.Message.Reason;
                context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
            })
            .ThenAsync(async context =>
            {
                await RecordStepAsync(context, WorkflowStatus.Compensating, "UpgradeListing", isCompleted: false, error: context.Message.Reason);
                await RecordStepAsync(context, WorkflowStatus.Compensating, "RefundWallet", isCompleted: false);
            })
            .TransitionTo(Compensating)
            .Publish(context => new RefundWalletCommand(
                context.Saga.CorrelationId,
                context.Saga.UserId,
                context.Saga.ChargeAmount))
    );

    // Step 4: hoàn trả tiền
    During(Compensating,
        When(WalletRefundedEvent)
            .Then(context =>
            {
                context.Saga.UpdatedAt = DateTimeOffset.UtcNow;
            })
            .ThenAsync(async context =>
            {
                await RecordStepAsync(context, WorkflowStatus.Compensated, "RefundWallet", isCompleted: true);
            })
            .TransitionTo(Compensated)
    );
}'''

# =============================================================================
# 7. DeductWalletConsumer.cs - Hàm Consume (trọn vẹn 100% dòng 37-108)
# =============================================================================
CODE_DEDUCT_FULL = '''public async Task Consume(ConsumeContext<DeductWalletCommand> context)
{
    var commnad = context.Message;
    LogConsuming(_logger, commnad.CorrelationId, commnad.UserId, commnad.Amount);

    // Check Idempotency 
    var alreadyProcessed = await _dbContext.WalletTransactions.AnyAsync(
        t => t.CorrelationId == commnad.CorrelationId && t.TransactionType == WalletTransactionType.Charge, context.CancellationToken);

    if (alreadyProcessed)
    {
        LogAlreadyProcessed(_logger, commnad.CorrelationId);
        await context.Publish(new WalletDeducted(
            commnad.CorrelationId,
            commnad.UserId,
            commnad.Amount
        ));
        return;
    }

    //lấy thông tin ví
    var wallet = await _walletRepository.GetByUserIdAsync( commnad.UserId, context.CancellationToken );
    if(wallet is null)
    {
        LogFailed(_logger, commnad.CorrelationId, "Ví của người dùng không tồn tại");
        await context.Publish(new WalletDeductionFailed(
            commnad.CorrelationId,
            commnad.UserId,
            "Ví của người dùng không tồn tại"
        ));
        return;
    }

    //trừ tiền
    var totalBalanceBefore = wallet.MainBalance + wallet.PromoBalance;
    try
    {
        wallet.Charge(commnad.Amount);
    }
    catch(InvalidOperationException ex)
    {
        LogFailed(_logger, commnad.CorrelationId, ex.Message);
        await context.Publish(new WalletDeductionFailed(
            commnad.CorrelationId,
            commnad.UserId,
            ex.Message
        ));
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
        description: $"Thanh toán gói dịch vụ tin đăng ({commnad.Amount:N0}đ)"
    );

    await _walletRepository.AddTransactionAsync(transaction, context.CancellationToken);
    await _unitOfWork.SaveChangesAsync(context.CancellationToken);

    LogSuccess(_logger, commnad.CorrelationId, commnad.UserId, commnad.Amount);

    await context.Publish(new WalletDeducted(
        commnad.CorrelationId,
        commnad.UserId,
        commnad.Amount
    ));
}'''

# =============================================================================
# 8a. UpgradeListingConsumer.cs - Phần 1: Kiểm tra idempotency & gói dịch vụ (dòng 35-85)
# =============================================================================
CODE_UPGRADE_PART1 = '''public async Task Consume(ConsumeContext<UpgradeListingCommand> context)
{
    var command = context.Message;
    LogConsuming(_logger, command.CorrelationId, command.ListingId, command.PackageCode);

    //check idempotency
    var alreadyProcessed = await _dbContext.ListingHistories.AnyAsync(
            h =>h.ListingId == command.ListingId &&
                h.ActionType == "UpgradePackage" &&
                h.Note != null && 
                h.Note.Contains(command.CorrelationId.ToString()),
                context.CancellationToken
        );
    if (alreadyProcessed)
    {
        LogAlreadyProcessed(_logger, command.CorrelationId);
        await context.Publish(new ListingUpgraded(
                command.CorrelationId,
                command.ListingId,
                command.PackageCode
            ));
        return;
    }

    //tìm listing
    var listing = await _listingRepository.GetByIdAsync( command.ListingId, context.CancellationToken);
    if(listing is null)
    {
        LogFailed(_logger, command.CorrelationId, $"Tin đăng với ID {command.ListingId} không tồn tại.");
        await context.Publish(new ListingUpgradeFailed(
                command.CorrelationId,
                command.ListingId,
                $"Tin đăng với ID {command.ListingId} không tồn tại."
            ));
        return;
    }

    // check trạng thái listing
    if (listing.Status == ListingStatus.Deleted)
    {
        LogFailed(_logger, command.CorrelationId, "Không thể nâng cấp gói cho tin đăng đã bị xóa.");
        await context.Publish(new ListingUpgradeFailed(
            command.CorrelationId,
            command.ListingId,
            "Không thể nâng cấp gói cho tin đăng đã bị xóa."));
        return;
    }

    // lấy thông tin gói
    var package = await _listingRepository.GetPackageByCodeAsync(command.PackageCode, context.CancellationToken);
    if (package is null || !package.IsActive)
    {
        LogFailed(_logger, command.CorrelationId, $"Gói dịch vụ '{command.PackageCode}' không tồn tại hoặc đã ngừng áp dụng.");
        await context.Publish(new ListingUpgradeFailed(
            command.CorrelationId,
            command.ListingId,
            $"Gói dịch vụ '{command.PackageCode}' không tồn tại hoặc đã ngừng áp dụng."));
        return;
    }'''

# =============================================================================
# 8b. UpgradeListingConsumer.cs - Phần 2: Nâng cấp gói tin và lưu lịch sử (dòng 87-136)
# =============================================================================
CODE_UPGRADE_PART2 = '''    // (Tiếp nối UpgradeListingConsumer: Tính hạn dùng và nâng cấp)
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
            $"Nâng cấp gói {command.PackageCode} (+{package.DurationDays} ngày, HSD đến: {endDate:yyyy-MM-dd HH:mm}) - CorrelationId: {command.CorrelationId}");

        _listingRepository.AddHistory( history );
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
    catch (Exception ex)
    {
        LogFailed(_logger, command.CorrelationId, ex.Message);
        await context.Publish(new ListingUpgradeFailed(
            command.CorrelationId,
            command.ListingId,
            ex.Message));
        return;
    }

    //publish event thành công
    LogSuccess(_logger, command.CorrelationId, command.ListingId, command.PackageCode);

    await context.Publish(new ListingUpgraded(
        command.CorrelationId,
        command.ListingId,
        command.PackageCode
        ));
}'''

# =============================================================================
# 9. RefundWalletConsumer.cs - Hàm Consume (trọn vẹn 100% dòng 34-86)
# =============================================================================
CODE_REFUND_FULL = '''public async Task Consume(ConsumeContext<RefundWalletCommand> context)
{
    var command = context.Message;
    LogConsuming(_logger, command.CorrelationId, command.UserId, command.Amount);

    // check idempotency
    var alreadyProcessed = await _dbContext.WalletTransactions.AnyAsync(
        w=>w.CorrelationId == command.CorrelationId && w.TransactionType == WalletTransactionType.Refund,
        context.CancellationToken
        );
    if (alreadyProcessed)
    {
        LogAlreadyProcessed(_logger, command.CorrelationId);
        await context.Publish(new WalletRefunded(
            command.CorrelationId,
            command.UserId,
            command.Amount));
        return;
    }

    //lấy thông tin ví
    var wallet = await _walletRepository.GetByUserIdAsync(command.UserId, context.CancellationToken);
    if (wallet is null)
    {
        LogWalletNotFound(_logger, command.CorrelationId, command.UserId);
        throw new InvalidOperationException($"Không tìm thấy ví của người dùng có ID {command.UserId} để hoàn tiền.");
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
        description: $"Hoàn tiền giao dịch mua gói tin đăng thất bại ({command.Amount:N0}đ)"
    );
    await _walletRepository.AddTransactionAsync(transaction, context.CancellationToken);
    await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    LogSuccess(_logger, command.CorrelationId, command.UserId, command.Amount);

    //publish event
    await context.Publish(new WalletRefunded(
        command.CorrelationId,
        command.UserId,
        command.Amount));
}'''

# =============================================================================
# 10. ServiceCollectionExtensions.cs - Đăng ký MassTransit, Saga, Consumers (trọn vẹn dòng 42-79)
# =============================================================================
CODE_MASSTRANSIT_FULL = '''// CẤU HÌNH MASSTRANSIT + RABBITMQ
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
        var port = ushort.TryParse(configuration["RabbitMq:Port"], out var p) ? p : (ushort)5672;
        var virtualHost = configuration["RabbitMq:VirtualHost"] ?? "/";
        var username = configuration["RabbitMq:Username"] ?? "guest";
        var password = configuration["RabbitMq:Password"] ?? "guest";
        cfg.Host(host, port, virtualHost, h =>
        {
            h.Username(username);
            h.Password(password);
        });
        cfg.UseConsumeFilter(typeof(InboxDeduplicationFilter<>), context);
        cfg.ConfigureEndpoints(context);
    });
});'''

# =============================================================================
# 11. WalletController.cs - Giao diện Seller (trọn vẹn dòng 10-46)
# =============================================================================
CODE_SELLER_WALLET_FULL = '''public class WalletController : Controller
{
    private readonly ISellerPropertyService _sellerService;

    public WalletController(ISellerPropertyService sellerService)
    {
        _sellerService = sellerService;
    }

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
}'''

ALL_FULL_SNIPPETS = {
    "wallet": CODE_WALLET_FULL,
    "topup": CODE_TOPUP_FULL,
    "transactions": CODE_TRANSACTIONS_FULL,
    "wallet_api": CODE_WALLET_CONTROLLER_FULL,
    "start_purchase": CODE_START_PURCHASE_FULL,
    "saga_part1": CODE_SAGA_PART1,
    "saga_part2": CODE_SAGA_PART2,
    "deduct": CODE_DEDUCT_FULL,
    "upgrade_part1": CODE_UPGRADE_PART1,
    "upgrade_part2": CODE_UPGRADE_PART2,
    "refund": CODE_REFUND_FULL,
    "masstransit": CODE_MASSTRANSIT_FULL,
    "seller_wallet": CODE_SELLER_WALLET_FULL,
}
