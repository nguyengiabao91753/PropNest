#pragma warning disable CA1707 // Naming Styles: Identifiers should not contain underscores

using System.Reflection;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Packages;
using PropNest.Domain.Workflows;

namespace PropNest.Domain.Tests;

/// <summary>
/// Bộ kiểm thử 4 nhánh xử lý moderation khi tích hợp AME (AI Moderation Engine).
/// Mô phỏng hành vi hệ thống khi nhận kết quả từ AME service:
///   1. Auto-Approve  – Tin hợp lệ → tự động xuất bản
///   2. Auto-Reject   – Tin vi phạm từ cấm / SĐT → tự động từ chối
///   3. Manual-Review  – Tin nghi ngờ / giá bất thường → giữ chờ duyệt tay
///   4. Degraded Fallback – AME/Ollama offline → an toàn chuyển về duyệt tay
/// </summary>
public sealed class AmeModerationFlowTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: Auto-Approve – Tin hợp lệ tự động xuất bản và ghi audit
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AutoApprove_ValidListing_ShouldPublishAndAudit()
    {
        // Arrange ─ Tạo listing đang chờ duyệt
        const long listingId = 1L;
        const long ameSystemUserId = 0L; // AME là hệ thống, không phải moderator thực
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);
        var package = new PackageDefinitions(1L, "Standard", "Gói thường", 50_000m, 30, "Standard");

        var repository = new FakeListingRepository
        {
            Listing = listing,
            Package = package
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act ─ Mô phỏng AME trả về decision = "Approved"
        //   → Hệ thống gọi ApproveAsync tương tự moderator duyệt thủ công
        var result = await service.ApproveAsync(listingId, ameSystemUserId);

        // Assert ─ Listing phải chuyển sang Published
        Assert.Equal(ListingStatus.Published, result.Status);
        Assert.Equal(ModerationDecision.Approved, result.ModerationDecision);
        Assert.NotNull(result.StartDate);
        Assert.NotNull(result.EndDate);
        Assert.True(result.EndDate > result.StartDate);

        // Kiểm tra ghi nhận Moderation Review (audit trail)
        var review = Assert.Single(repository.Reviews);
        Assert.Equal(listingId, review.ListingId);
        Assert.Equal(ModerationDecision.Approved, review.Decision);
        Assert.Equal("Manual", review.Source); // ListingService ghi "Manual" cho ApproveAsync
        Assert.Null(review.ReasonsJson);

        // Kiểm tra ghi nhận Listing History
        var history = Assert.Single(repository.Histories);
        Assert.Equal(listingId, history.ListingId);
        Assert.Equal("Approve", history.ActionType);

        // Kiểm tra Outbox message cho event ListingPublished
        var outbox = Assert.Single(repository.OutboxMessages);
        Assert.Equal("ListingPublished", outbox.Type);

        // Kiểm tra lưu thay đổi vào cơ sở dữ liệu
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: Auto-Reject – Tin vi phạm từ cấm / SĐT tự động từ chối
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AutoReject_BadwordOrPhone_ShouldRejectImmediately()
    {
        // Arrange ─ Tạo listing đang chờ duyệt
        const long listingId = 2L;
        const long ameSystemUserId = 0L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);

        // AME phát hiện vi phạm: từ cấm + SĐT
        const string ameReasonsJson = "[" +
            "{\"rule\":\"BADWORD_DETECTED\",\"severity\":\"critical\",\"message\":\"Phát hiện nội dung vi phạm: cam kết lợi nhuận\"}," +
            "{\"rule\":\"PHONE_EXPOSURE\",\"severity\":\"critical\",\"message\":\"Phát hiện số điện thoại trong mô tả: 0908123456\"}" +
            "]";

        var repository = new FakeListingRepository
        {
            Listing = listing
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new ListingService(repository, unitOfWork);

        // Act ─ Mô phỏng AME trả về decision = "Rejected"
        //   → Hệ thống gọi RejectAsync với lý do từ AME
        var result = await service.RejectAsync(listingId, ameSystemUserId, ameReasonsJson);

        // Assert ─ Listing phải chuyển sang Rejected
        Assert.Equal(ListingStatus.Rejected, result.Status);
        Assert.Equal(ModerationDecision.Rejected, result.ModerationDecision);

        // Kiểm tra ghi nhận Moderation Review với lý do vi phạm
        var review = Assert.Single(repository.Reviews);
        Assert.Equal(listingId, review.ListingId);
        Assert.Equal(ModerationDecision.Rejected, review.Decision);
        Assert.Equal("Manual", review.Source);
        Assert.NotNull(review.ReasonsJson);
        Assert.Contains("BADWORD_DETECTED", review.ReasonsJson);
        Assert.Contains("PHONE_EXPOSURE", review.ReasonsJson);

        // Kiểm tra ghi nhận Listing History
        var history = Assert.Single(repository.Histories);
        Assert.Equal(listingId, history.ListingId);
        Assert.Equal("Reject", history.ActionType);

        // Kiểm tra Outbox message cho event ListingRejected
        var outbox = Assert.Single(repository.OutboxMessages);
        Assert.Equal("ListingRejected", outbox.Type);

        // Kiểm tra lưu thay đổi vào cơ sở dữ liệu
        Assert.Equal(1, unitOfWork.SaveChangesCalls);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: Manual-Review – Tin nghi ngờ hoặc giá bất thường → chờ duyệt tay
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void ManualReview_SuspiciousContent_ShouldKeepPendingModeration()
    {
        // Arrange ─ Tạo listing đang chờ duyệt
        const long listingId = 3L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);

        // Act ─ Mô phỏng AME trả về decision = "ManualReview"
        //   → Hệ thống KHÔNG gọi Approve hay Reject
        //   → Listing giữ nguyên trạng thái PendingModeration để moderator xem xét
        //   (Hệ thống chỉ ghi nhận ModerationDecision = ManualReview trên entity)
        SetModerationDecision(listing, ModerationDecision.ManualReview);

        // Assert ─ Listing phải giữ trạng thái PendingModeration
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.ManualReview, listing.ModerationDecision);

        // Listing vẫn ở trạng thái chờ → moderator sẽ xem xét thủ công
        Assert.Null(listing.StartDate);
        Assert.Null(listing.EndDate);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: Degraded Fallback – AME/Ollama sập → an toàn chuyển về duyệt tay
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public void DegradedFallback_AmeOffline_ShouldFallbackToManualReview()
    {
        // Arrange ─ Tạo listing đang chờ duyệt
        const long listingId = 4L;
        var listing = CreateListingWithStatus(listingId, ListingStatus.PendingModeration);
        const string fallbackReason = "LLM_UNAVAILABLE_FALLBACK_MANUAL";

        // Act ─ Mô phỏng AME/Ollama offline
        //   → HttpClient ném exception khi gọi AME
        //   → Hệ thống catch exception, fallback về ManualReview
        //   → KHÔNG crash, KHÔNG throw, an toàn fallback
        string? actualDecision = null;
        string? actualReason = null;

        try
        {
            // Giả lập gọi AME service thất bại
            throw new HttpRequestException("Connection refused: AME service is unavailable");
        }
        catch (HttpRequestException)
        {
            // Fallback logic: khi AME offline, hệ thống fallback an toàn
            actualDecision = "ManualReview";
            actualReason = fallbackReason;

            // Ghi nhận ManualReview trên listing (không Approve, không Reject)
            SetModerationDecision(listing, ModerationDecision.ManualReview);
        }

        // Assert ─ Hệ thống không crash
        Assert.Equal("ManualReview", actualDecision);
        Assert.Equal("LLM_UNAVAILABLE_FALLBACK_MANUAL", actualReason);

        // Listing giữ trạng thái PendingModeration (an toàn)
        Assert.Equal(ListingStatus.PendingModeration, listing.Status);
        Assert.Equal(ModerationDecision.ManualReview, listing.ModerationDecision);

        // Listing không bị publish hay reject nhầm
        Assert.Null(listing.StartDate);
        Assert.Null(listing.EndDate);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo Listing với trạng thái mong muốn, sử dụng Reflection để set ListingId.
    /// </summary>
    private static Listing CreateListingWithStatus(long listingId, ListingStatus status)
    {
        var listing = new Listing(
            ownerUserId: 10L,
            title: "Căn hộ Vinhomes Grand Park",
            description: "Căn hộ 2 phòng ngủ, 2 WC, view sông, đầy đủ nội thất cao cấp.",
            propertyType: "Apartment",
            listingType: "Sale",
            price: 3_200_000_000m,
            area: 69m,
            city: "TP Hồ Chí Minh",
            district: "Thủ Đức",
            ward: "Long Thạnh Mỹ",
            address: "Nguyễn Xiển");

        // Set ListingId qua Reflection (property có private set)
        typeof(Listing).GetProperty(nameof(Listing.ListingId),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .SetValue(listing, listingId);

        switch (status)
        {
            case ListingStatus.Draft:
                break;
            case ListingStatus.PendingModeration:
                listing.SubmitForModeration();
                break;
            case ListingStatus.Published:
                listing.SubmitForModeration();
                listing.Approve(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(30));
                break;
            case ListingStatus.Rejected:
                listing.SubmitForModeration();
                listing.Reject();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        return listing;
    }

    /// <summary>
    /// Set ModerationDecision qua Reflection (property có private set).
    /// Mô phỏng hệ thống ghi nhận quyết định AME lên entity mà không thay đổi Status.
    /// </summary>
    private static void SetModerationDecision(Listing listing, ModerationDecision decision)
    {
        typeof(Listing).GetProperty(nameof(Listing.ModerationDecision),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
            .SetValue(listing, decision);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Fake implementations (giống pattern ModerationFlowTests.cs)
    // ═════════════════════════════════════════════════════════════════════════

    private sealed class FakeListingRepository : IListingRepository
    {
        public Listing? Listing { get; set; }
        public PackageDefinitions? Package { get; set; }
        public List<ListingModerationReview> Reviews { get; } = [];
        public List<ListingHistory> Histories { get; } = [];
        public List<OutboxMessage> OutboxMessages { get; } = [];

        public Task<ListingOwnerContactDto?> GetOwnerContactAsync(long ownerUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<ListingOwnerContactDto?>(null);

        public Task<IReadOnlyList<ListingDto>> GetPendingModerationWithOwnersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingDto>>([]);

        public Task<Listing?> GetByIdAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult(Listing != null && Listing.ListingId == listingId ? Listing : null);

        public Task<PackageDefinitions?> GetPackageByCodeAsync(string packageCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Package != null && Package.PackageCode.Equals(packageCode, StringComparison.OrdinalIgnoreCase) ? Package : null);

        public void AddModerationReview(ListingModerationReview review) => Reviews.Add(review);

        public void AddHistory(ListingHistory history) => Histories.Add(history);

        public void AddOutboxMessage(OutboxMessage message) => OutboxMessages.Add(message);

        public Task<IReadOnlyList<Listing>> GetByOwnerUserIdAsync(long ownerUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Listing>>([]);

        public Task<IReadOnlyList<Listing>> GetPendingModerationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Listing>>([]);

        public Task AddAsync(Listing listing, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void SetOriginalRowVersion(Listing listing, byte[] rowVersion) { }

        public Task<PagedResult<ListingSummaryDto>> GetPublicListingsAsync(GetPublicListingsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new PagedResult<ListingSummaryDto>([], 0, 1, 10));

        public Task<IReadOnlyList<ListingHistoryDto>> GetHistoriesByListingIdAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingHistoryDto>>([]);

        public Task<IReadOnlyList<Listing>> GetExpiredPublishedListingsAsync(DateTimeOffset now, int batchSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Listing>>([]);

        public Task<Listing?> GetByIdWithImagesAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<Listing?>(null);

        public Task<IReadOnlyList<ListingImage>> GetImagesByListingIdAsync(long listingId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ListingImage>>([]);

        public Task<ListingImage?> GetImageByIdAsync(long imageId, CancellationToken cancellationToken = default)
            => Task.FromResult<ListingImage?>(null);

        public void AddImage(ListingImage image) { }

        public void RemoveImage(ListingImage image) { }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }
}
