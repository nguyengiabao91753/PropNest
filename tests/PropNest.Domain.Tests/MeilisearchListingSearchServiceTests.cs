using Meilisearch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PropNest.Application.Abstractions;
using PropNest.Application.Common;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
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
}
