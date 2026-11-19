using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Api.Contracts.Moderation;
using PropNest.Domain.Listings;
using PropNest.Domain.Wallets;
using PropNest.Infrastructure.Persistence;
using System.Text.Json;

namespace PropNest.Api.Controllers;

[ApiController]
[Authorize(Policy = "CanModerateListings")]
[Route("api/v1/moderation")]
public sealed class ModerationController(
    ICurrentUser currentUser,
    IListingService listingService,
    PropNestDbContext dbContext) : ControllerBase
{
    [HttpGet("dashboard-stats")]
    [HttpGet("stats")]
    public async Task<IActionResult> GetDashboardStats(CancellationToken cancellationToken)
    {
        // 1. Status distribution from database (gộp query theo Status + ModerationDecision)
        var statusDecisionGroups = await dbContext.Listings.AsNoTracking()
            .GroupBy(l => new { l.Status, l.ModerationDecision })
            .Select(g => new { g.Key.Status, g.Key.ModerationDecision, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountByStatus(ListingStatus s) => statusDecisionGroups.Where(g => g.Status == s).Sum(g => g.Count);

        var publishedCount = CountByStatus(ListingStatus.Published);
        // Hàng chờ Admin chỉ bao gồm các tin AME từ chối tự động duyệt / cần duyệt thủ công (ManualReview)
        var pendingCount = statusDecisionGroups
            .Where(g => g.Status == ListingStatus.PendingModeration && g.ModerationDecision == ModerationDecision.ManualReview)
            .Sum(g => g.Count);
        var ameProcessingCount = statusDecisionGroups
            .Where(g => g.Status == ListingStatus.PendingModeration && g.ModerationDecision != ModerationDecision.ManualReview)
            .Sum(g => g.Count);
        var pendingPaymentCount = CountByStatus(ListingStatus.PendingPayment);
        var rejectedCount = CountByStatus(ListingStatus.Rejected);
        var draftCount = CountByStatus(ListingStatus.Draft);
        var hiddenCount = CountByStatus(ListingStatus.Hidden);
        var expiredCount = CountByStatus(ListingStatus.Expired);
        // Không tính các tin đã xóa (Deleted) vào tổng tin đăng
        var totalProperties = statusDecisionGroups
            .Where(g => g.Status != ListingStatus.Deleted)
            .Sum(g => g.Count);

        // 2. Real users and active sellers/brokers count
        var totalUsers = await dbContext.Users.CountAsync(cancellationToken);

        var sellerRoleIds = await dbContext.Roles.AsNoTracking()
            .Where(r => r.Name == "Seller" || r.Name == "Broker")
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var activeSellersCount = await dbContext.Users.AsNoTracking()
            .CountAsync(u => u.Role == "Seller" || u.Role == "Broker"
                          || dbContext.UserRoles.Any(ur => ur.UserId == u.Id && sellerRoleIds.Contains(ur.RoleId))
                          || dbContext.Listings.Any(l => l.OwnerUserId == u.Id), cancellationToken);

        // 3. Real Wallet stats (1 câu GROUP BY thay vì 7 câu query lẻ; doanh thu ròng = Charge - Refund)
        var walletGroups = await dbContext.WalletTransactions.AsNoTracking()
            .GroupBy(t => t.TransactionType)
            .Select(g => new { Type = g.Key, Count = g.Count(), Amount = g.Sum(x => (decimal?)x.Amount) ?? 0m })
            .ToListAsync(cancellationToken);

        var topUpGroup = walletGroups.FirstOrDefault(g => g.Type == WalletTransactionType.TopUp);
        var chargeGroup = walletGroups.FirstOrDefault(g => g.Type == WalletTransactionType.Charge);
        var refundGroup = walletGroups.FirstOrDefault(g => g.Type == WalletTransactionType.Refund);

        var topUpCount = topUpGroup?.Count ?? 0;
        var chargeCount = chargeGroup?.Count ?? 0;
        var refundCount = refundGroup?.Count ?? 0;
        var totalTopUpAmount = topUpGroup?.Amount ?? 0m;
        var totalSpentAmount = chargeGroup?.Amount ?? 0m;
        var totalRefundAmount = refundGroup?.Amount ?? 0m;
        var walletTxCount = topUpCount + chargeCount + refundCount;
        var netRevenue = Math.Max(0m, totalSpentAmount - totalRefundAmount);

        // 4. Real 6-month monthly metrics (doanh thu thực = Charge - Refund, không cộng gộp TopUp + Refund)
        var now = DateTimeOffset.UtcNow;
        var sixMonthsAgo = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-5);

        var monthlyListings = await dbContext.Listings.AsNoTracking()
            .Where(l => l.CreatedAt >= sixMonthsAgo && l.Status != ListingStatus.Deleted)
            .GroupBy(l => new { l.CreatedAt.Year, l.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var monthlyTxs = await dbContext.WalletTransactions.AsNoTracking()
            .Where(t => t.CreatedAt >= sixMonthsAgo)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month, t.TransactionType })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.TransactionType,
                Count = g.Count(),
                Amount = g.Sum(x => (decimal?)x.Amount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var monthlyMetrics = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var targetDate = now.AddMonths(-i);
            var mList = monthlyListings.FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month)?.Count ?? 0;
            var monthTxRows = monthlyTxs.Where(x => x.Year == targetDate.Year && x.Month == targetDate.Month).ToList();
            var txCount = monthTxRows.Sum(x => x.Count);
            var monthCharge = monthTxRows.Where(x => x.TransactionType == WalletTransactionType.Charge).Sum(x => x.Amount);
            var monthRefund = monthTxRows.Where(x => x.TransactionType == WalletTransactionType.Refund).Sum(x => x.Amount);
            var rev = Math.Max(0m, monthCharge - monthRefund);

            monthlyMetrics.Add(new
            {
                month = $"T{targetDate.Month}/{targetDate.Year}",
                revenue = rev,
                listingCount = mList,
                walletTransactionCount = txCount
            });
        }

        // 5. Real recent wallet transactions (top 15)
        var recentTransactions = await (from tx in dbContext.WalletTransactions.AsNoTracking()
                                        join w in dbContext.Wallets.AsNoTracking() on tx.WalletId equals w.WalletId into wg
                                        from wallet in wg.DefaultIfEmpty()
                                        join u in dbContext.Users.AsNoTracking() on (wallet != null ? (long?)wallet.UserId : null) equals (long?)u.Id into ug
                                        from user in ug.DefaultIfEmpty()
                                        orderby tx.CreatedAt descending
                                        select new
                                        {
                                            transactionId = "TX-" + tx.TransactionId,
                                            userName = user != null
                                                ? (!string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.UserName ?? user.Email ?? ("Người dùng #" + (wallet != null ? wallet.UserId : 0))))
                                                : (wallet != null ? ("Người dùng #" + wallet.UserId) : "Hệ thống"),
                                            transactionType = tx.TransactionType.ToString(),
                                            amount = tx.Amount,
                                            createdAt = tx.CreatedAt,
                                            status = "Thành công",
                                            description = tx.Description ?? string.Empty
                                        })
                                        .Take(15)
                                        .ToListAsync(cancellationToken);

        return Ok(new
        {
            publishedCount,
            pendingCount,
            ameProcessingCount,
            pendingPaymentCount,
            rejectedCount,
            draftCount,
            hiddenCount,
            expiredCount,
            totalProperties,
            totalUsers,
            activeSellersCount,
            totalWalletTransactions = walletTxCount,
            totalWalletVolume = netRevenue,
            monthlyMetrics,
            statusDistribution = new Dictionary<string, int>
            {
                ["Published"] = publishedCount,
                ["PendingModeration"] = pendingCount,
                ["PendingPayment"] = pendingPaymentCount,
                ["Draft"] = draftCount,
                ["Rejected"] = rejectedCount,
                ["Hidden"] = hiddenCount,
                ["Expired"] = expiredCount
            },
            recentTransactions,
            walletStats = new
            {
                topUpCount,
                packagePurchaseCount = chargeCount,
                refundCount,
                totalTopUpAmount,
                totalSpentAmount,
                totalRefundAmount,
                netRevenue
            }
        });
    }

    [HttpGet("listings/all")]
    public async Task<IActionResult> GetAllListings(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 200,
        [FromQuery] ListingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, pageNumber);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var baseListings = dbContext.Listings.AsNoTracking().AsQueryable();
        if (status.HasValue)
        {
            baseListings = baseListings.Where(l => l.Status == status.Value);
        }

        var total = await baseListings.CountAsync(cancellationToken);

        var query = from l in baseListings
                    join u in dbContext.Users.AsNoTracking() on l.OwnerUserId equals u.Id into userGroup
                    from user in userGroup.DefaultIfEmpty()
                    orderby l.ListingId descending
                    select new ListingDto(
                        l.ListingId,
                        l.OwnerUserId,
                        l.Title,
                        l.Description,
                        l.PropertyType,
                        l.ListingType,
                        l.Price,
                        l.Area,
                        l.City,
                        l.District,
                        l.Ward,
                        l.Address,
                        l.Status,
                        l.ModerationDecision,
                        l.PackageCode,
                        l.StartDate,
                        l.EndDate,
                        l.RowVersion,
                        user != null ? (user.FullName ?? user.UserName) : null,
                        user != null ? user.Email : null,
                        user != null ? user.PhoneNumber : null,
                        l.CreatedAt,
                        l.Images.OrderBy(img => img.SortOrder).Select(img => img.Url).ToList(),
                        l.Floors,
                        l.Bedrooms,
                        l.Bathrooms,
                        l.BalconyDirection,
                        l.AdministrativeMode,
                        l.ProvinceCode,
                        l.DistrictCode,
                        l.WardCode,
                        l.LegacyAddress,
                        l.PackageEndDate,
                        null,
                        null);

        var items = await query
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return Ok(new { total, pageNumber = safePage, pageSize = safePageSize, items });
    }

    [HttpGet("listings")]
    [HttpGet("listings/pending")]
    public async Task<ActionResult<ListingCollectionDto>> GetPendingListings(CancellationToken cancellationToken)
    {
        return Ok(await listingService.GetPendingModerationAsync(cancellationToken));
    }

    [HttpGet("listings/{listingId:long}")]
    public async Task<ActionResult<ListingDto>> GetListing(long listingId, CancellationToken cancellationToken)
    {
        var listing = await listingService.GetByIdAsync(listingId, cancellationToken);
        return listing is null ? NotFound() : Ok(listing);
    }

    [HttpPost("listings/{listingId:long}/approve")]
    public async Task<ActionResult<ListingDto>> Approve(
        long listingId,
        CancellationToken cancellationToken)
    {
        var moderatorUserId = GetRequiredUserId();

        var listing = await listingService.ApproveAsync(
            listingId,
            moderatorUserId,
            cancellationToken);

        return Ok(listing);
    }

    private static readonly JsonSerializerOptions NoEscapeJsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public sealed record RejectPayload(string? ReasonsJson, string? Reason, IReadOnlyList<string>? Reasons);

    [HttpPost("listings/{listingId:long}/reject")]
    public async Task<ActionResult<ListingDto>> Reject(
        long listingId,
        [FromBody] RejectPayload? request,
        CancellationToken cancellationToken)
    {
        var reasonsList = new List<string>();

        if (request?.Reasons != null && request.Reasons.Count > 0)
        {
            reasonsList.AddRange(request.Reasons.Where(r => !string.IsNullOrWhiteSpace(r)));
        }
        else if (!string.IsNullOrWhiteSpace(request?.Reason))
        {
            reasonsList.Add(request.Reason.Trim());
        }
        else if (!string.IsNullOrWhiteSpace(request?.ReasonsJson))
        {
            try
            {
                using var reasonsDocument = JsonDocument.Parse(request.ReasonsJson);
                if (reasonsDocument.RootElement.ValueKind == JsonValueKind.Array)
                {
                    reasonsList.AddRange(reasonsDocument.RootElement.EnumerateArray()
                        .Select(e => e.GetString() ?? e.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                }
                else if (reasonsDocument.RootElement.ValueKind == JsonValueKind.String)
                {
                    var val = reasonsDocument.RootElement.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) reasonsList.Add(val);
                }
            }
            catch
            {
                reasonsList.Add(request.ReasonsJson.Trim());
            }
        }

        if (reasonsList.Count == 0)
        {
            return BadRequest(new { message = "Lý do từ chối không được để trống." });
        }

        // Clean any accidental Unicode escapes in input
        for (int i = 0; i < reasonsList.Count; i++)
        {
            var item = reasonsList[i];
            if (item.Contains(@"\u", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    reasonsList[i] = System.Text.RegularExpressions.Regex.Replace(
                        item,
                        @"\\u([0-9a-fA-F]{4})",
                        m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
                }
                catch
                {
                    // Ignore regex error
                }
            }
        }

        var reasonsJson = JsonSerializer.Serialize(reasonsList, NoEscapeJsonOptions);
        var moderatorUserId = GetRequiredUserId();

        var listing = await listingService.RejectAsync(
            listingId,
            moderatorUserId,
            reasonsJson,
            cancellationToken);

        return Ok(listing);
    }

    private long GetRequiredUserId() => currentUser.UserId
        ?? throw new UnauthorizedAccessException("A valid moderator user identifier claim is required.");

    [HttpGet("listings/{listingId:long}/ame-result")]
    public async Task<IActionResult> GetAmeResult(
    long listingId, CancellationToken cancellationToken)
    {
        var result = await listingService.GetAmeResultAsync(listingId, cancellationToken);
        return result is null
            ? NotFound(new { message = $"Chưa có kết quả AME cho listing {listingId}" })
            : Ok(result);
    }
}