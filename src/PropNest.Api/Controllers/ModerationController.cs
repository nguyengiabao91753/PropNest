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
        // 1. Status distribution from database
        var statusGroups = await dbContext.Listings.AsNoTracking()
            .GroupBy(l => l.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statusDict = statusGroups.ToDictionary(g => g.Status.ToString(), g => g.Count);

        var publishedCount = statusDict.GetValueOrDefault(nameof(ListingStatus.Published), 0);
        var pendingCount = statusDict.GetValueOrDefault(nameof(ListingStatus.PendingModeration), 0);
        var rejectedCount = statusDict.GetValueOrDefault(nameof(ListingStatus.Rejected), 0);
        var draftCount = statusDict.GetValueOrDefault(nameof(ListingStatus.Draft), 0);
        var hiddenCount = statusDict.GetValueOrDefault(nameof(ListingStatus.Hidden), 0);
        var expiredCount = statusDict.GetValueOrDefault(nameof(ListingStatus.Expired), 0);
        var totalProperties = await dbContext.Listings.CountAsync(cancellationToken);

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

        // 3. Real Wallet stats
        var walletTxCount = await dbContext.WalletTransactions.CountAsync(cancellationToken);
        var walletVolume = await dbContext.WalletTransactions.SumAsync(w => (decimal?)w.Amount, cancellationToken) ?? 0m;

        var topUpCount = await dbContext.WalletTransactions.AsNoTracking()
            .CountAsync(t => t.TransactionType == WalletTransactionType.TopUp, cancellationToken);
        var chargeCount = await dbContext.WalletTransactions.AsNoTracking()
            .CountAsync(t => t.TransactionType == WalletTransactionType.Charge, cancellationToken);
        var refundCount = await dbContext.WalletTransactions.AsNoTracking()
            .CountAsync(t => t.TransactionType == WalletTransactionType.Refund, cancellationToken);

        var totalTopUpAmount = await dbContext.WalletTransactions.AsNoTracking()
            .Where(t => t.TransactionType == WalletTransactionType.TopUp)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        var totalSpentAmount = await dbContext.WalletTransactions.AsNoTracking()
            .Where(t => t.TransactionType == WalletTransactionType.Charge)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        // 4. Real 6-month monthly metrics
        var now = DateTimeOffset.UtcNow;
        var sixMonthsAgo = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-5);

        var monthlyListings = await dbContext.Listings.AsNoTracking()
            .Where(l => l.CreatedAt >= sixMonthsAgo)
            .GroupBy(l => new { l.CreatedAt.Year, l.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var monthlyTxs = await dbContext.WalletTransactions.AsNoTracking()
            .Where(t => t.CreatedAt >= sixMonthsAgo)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Volume = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var monthlyMetrics = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var targetDate = now.AddMonths(-i);
            var mList = monthlyListings.FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month)?.Count ?? 0;
            var mTx = monthlyTxs.FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month);
            var txCount = mTx?.Count ?? 0;
            var rev = mTx?.Volume ?? 0m;

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
            rejectedCount,
            draftCount,
            hiddenCount,
            expiredCount,
            totalProperties,
            totalUsers,
            activeSellersCount,
            totalWalletTransactions = walletTxCount,
            totalWalletVolume = walletVolume,
            monthlyMetrics,
            statusDistribution = new Dictionary<string, int>
            {
                ["Published"] = publishedCount,
                ["PendingModeration"] = pendingCount,
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
                totalSpentAmount
            }
        });
    }

    [HttpGet("listings/all")]
    public async Task<IActionResult> GetAllListings(CancellationToken cancellationToken)
    {
        var query = from l in dbContext.Listings.AsNoTracking()
                    join u in dbContext.Users.AsNoTracking() on l.OwnerUserId equals u.Id into userGroup
                    from user in userGroup.DefaultIfEmpty()
                    orderby l.CreatedAt descending
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
                        l.Images.OrderBy(img => img.SortOrder).Select(img => img.Url).ToList());

        var items = await query.ToListAsync(cancellationToken);
        return Ok(new { total = items.Count, items });
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
        var moderatorUserId = currentUser.UserId ?? 1;

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
        var moderatorUserId = currentUser.UserId ?? 1;

        var listing = await listingService.RejectAsync(
            listingId,
            moderatorUserId,
            reasonsJson,
            cancellationToken);

        return Ok(listing);
    }
}