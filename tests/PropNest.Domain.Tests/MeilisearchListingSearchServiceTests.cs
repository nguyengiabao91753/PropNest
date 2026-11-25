using Meilisearch;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Services.Search;

namespace PropNest.Domain.Tests;

public class MeilisearchListingSearchServiceTests
{
    private readonly Mock<IListingRepository> _repositoryMock;
    private readonly MeilisearchClient _client;

    public MeilisearchListingSearchServiceTests()
    {
        _repositoryMock = new Mock<IListingRepository>();
        _client = new MeilisearchClient("http://127.0.0.1:17799", "masterKey");
    }

    [Fact]
    public async Task SearchListingsAsyncWhenDisabledShouldFallbackToRepository()
    {
        var options = Options.Create(new MeilisearchOptions
        {
            Enabled = false,
            ListingsIndexName = "listings"
        });

        var expectedResult = new PagedResult<ListingSummaryDto>(
            Items: new List<ListingSummaryDto>
            {
                new(1, "Nhà đẹp", 1000000000, 50, "Sale", "House", "TP. Hồ Chí Minh", "Quận 1", "Bến Nghé", "Standard", "1 Lê Duẩn", ListingStatus.Published, DateTimeOffset.UtcNow, ModerationDecision.Approved)
            },
            TotalCount: 1,
            PageNumber: 1,
            PageSize: 20);

        _repositoryMock
            .Setup(r => r.GetPublicListingsAsync(It.IsAny<GetPublicListingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var service = new MeilisearchListingSearchService(
            _client,
            options,
            _repositoryMock.Object,
            NullLogger<MeilisearchListingSearchService>.Instance);

        var query = new GetPublicListingsQuery(Q: "Nhà đẹp");

        var result = await service.SearchListingsAsync(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Nhà đẹp", result.Items[0].Title);
        _repositoryMock.Verify(r => r.GetPublicListingsAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchListingsAsyncWhenMeilisearchFailsShouldGracefullyFallbackToRepository()
    {
        var options = Options.Create(new MeilisearchOptions
        {
            Enabled = true,
            ListingsIndexName = "listings",
            RequestTimeoutMs = 100
        });

        var expectedResult = new PagedResult<ListingSummaryDto>(
            Items: new List<ListingSummaryDto>
            {
                new(2, "Căn hộ cao cấp", 2500000000, 75, "Sale", "Apartment", "Hà Nội", "Cầu Giấy", "Dịch Vọng", "VIP", "123 Xuân Thủy", ListingStatus.Published, DateTimeOffset.UtcNow, ModerationDecision.Approved)
            },
            TotalCount: 1,
            PageNumber: 1,
            PageSize: 20);

        _repositoryMock
            .Setup(r => r.GetPublicListingsAsync(It.IsAny<GetPublicListingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var service = new MeilisearchListingSearchService(
            _client,
            options,
            _repositoryMock.Object,
            NullLogger<MeilisearchListingSearchService>.Instance);

        var query = new GetPublicListingsQuery(Q: "Căn hộ cao cấp");

        var result = await service.SearchListingsAsync(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Căn hộ cao cấp", result.Items[0].Title);
        _repositoryMock.Verify(r => r.GetPublicListingsAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void BuildFilterShouldIgnoreTypeAllAndIncludeNormalizedLocationTokens()
    {
        var query = new GetPublicListingsQuery(
            Type: "all",
            City: "Hồ Chí Minh",
            District: "Quận 7",
            MinArea: 50,
            MinBedrooms: 2,
            BalconyDirection: "Đông Nam");

        var filter = MeilisearchListingSearchService.BuildFilter(query, 1_800_000_000);

        Assert.Contains("status = \"Published\"", filter);
        Assert.Contains("moderationDecision = \"Approved\"", filter);
        Assert.Contains("endDateUnix > 1800000000", filter);
        Assert.DoesNotContain("listingType =", filter);
        Assert.Contains("locationTokens = \"ho chi minh\"", filter);
        Assert.Contains("locationTokens = \"quan 7\"", filter);
        Assert.Contains("area >= 50", filter);
        Assert.Contains("bedrooms >= 2", filter);
        Assert.Contains("balconyDirection = \"Đông Nam\"", filter);
    }

    [Fact]
    public async Task SqlFallbackRepositoryShouldApplyExtendedFiltersSortingAndVipExpiryGuard()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<PropNestDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new PropNestDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Users.Add(new ApplicationUser("seller@propnest.vn", "Seller", "0901234567", "Seller") { Id = 1 });
        await dbContext.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;

        var smallCheap = new Listing(
            1, "Căn hộ mini 40m2", "Sổ hồng chính chủ", "Apartment", "Sale",
            1_800_000_000m, 40m, "Thành phố Hồ Chí Minh", "Quận 7", "Phường Tân Phong", "10 Nguyễn Văn Linh",
            floors: 1, bedrooms: 1, bathrooms: 1, balconyDirection: "Đông", provinceCode: 79);
        smallCheap.SubmitForModeration();
        smallCheap.Approve(now.AddDays(-5));
        smallCheap.ApplyPackagePurchase("Standard", 30, now.AddDays(-5));

        var matchingExpensive = new Listing(
            1, "Căn hộ 85m2 Phú Mỹ Hưng", "Hồ bơi tràn bờ và công viên nội khu", "Apartment", "Sale",
            5_500_000_000m, 85m, "Thành phố Hồ Chí Minh", "Quận 7", "Phường Tân Phong", "20 Nguyễn Văn Linh",
            floors: 1, bedrooms: 2, bathrooms: 2, balconyDirection: "Đông Nam", provinceCode: 79);
        matchingExpensive.SubmitForModeration();
        matchingExpensive.Approve(now.AddDays(-10));
        matchingExpensive.ApplyPackagePurchase("Standard", 30, now.AddDays(-10));
        // VIP đã hết hạn 2 ngày trước nhưng tin Standard vẫn còn hạn
        matchingExpensive.ApplyPackagePurchase("VIP", 3, now.AddDays(-10));

        var matchingCheaper = new Listing(
            1, "Căn hộ 75m2 Quận 7", "Hồ bơi đẹp gần siêu thị", "Apartment", "Sale",
            3_900_000_000m, 75m, "Thành phố Hồ Chí Minh", "Quận 7", "Phường Tân Phong", "30 Nguyễn Văn Linh",
            floors: 1, bedrooms: 2, bathrooms: 2, balconyDirection: "Đông Nam", provinceCode: 79);
        matchingCheaper.SubmitForModeration();
        matchingCheaper.Approve(now.AddDays(-1));
        matchingCheaper.ApplyPackagePurchase("Standard", 30, now.AddDays(-1));

        dbContext.Listings.AddRange(smallCheap, matchingExpensive, matchingCheaper);
        await dbContext.SaveChangesAsync();

        var repo = new ListingRepository(dbContext);
        var result = await repo.GetPublicListingsAsync(new GetPublicListingsQuery(
            Type: "all",
            Q: "Hồ bơi",
            MinArea: 60,
            MinBedrooms: 2,
            MinBathrooms: 2,
            BalconyDirection: "Đông Nam",
            ProvinceCode: 79,
            SortBy: "price_asc"));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(matchingCheaper.ListingId, result.Items[0].ListingId);
        Assert.Equal(matchingExpensive.ListingId, result.Items[1].ListingId);
        // VIP hết hạn phải tự chuẩn hóa badge hiển thị về Standard
        Assert.Equal("Standard", result.Items[1].PackageCode);
        Assert.Null(result.Items[1].PackageEndDate);
    }
}
