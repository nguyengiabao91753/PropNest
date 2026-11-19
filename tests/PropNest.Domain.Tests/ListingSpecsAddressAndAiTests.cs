using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PropNest.Application.Abstractions;
using PropNest.Application.Listings;
using PropNest.Domain.Listings;
using PropNest.Domain.Users;
using PropNest.Infrastructure.Persistence;
using PropNest.Infrastructure.Persistence.Repositories;
using PropNest.Infrastructure.Services;

namespace PropNest.Domain.Tests;

public sealed class ListingSpecsAddressAndAiTests
{
    [Fact]
    public void ListingShouldAllowPostMergerWithoutDistrictAndStoreOptionalSpecs()
    {
        var listing = new Listing(
            ownerUserId: 1,
            title: "Căn hộ view sông 2PN",
            description: "Nội thất đầy đủ, ban công thoáng mát.",
            propertyType: "Apartment",
            listingType: "Sale",
            price: 4_500_000_000m,
            area: 75m,
            city: "Thành phố Hồ Chí Minh",
            district: string.Empty,
            ward: "Phường Thạnh Mỹ Tây",
            address: "208 Nguyễn Hữu Cảnh",
            floors: 12,
            bedrooms: 2,
            bathrooms: 2,
            balconyDirection: "Đông Nam",
            administrativeMode: "PostMerger",
            provinceCode: 79,
            districtCode: null,
            wardCode: 26956,
            legacyAddress: "Phường 22, Quận Bình Thạnh, Thành phố Hồ Chí Minh");

        Assert.Equal("PostMerger", listing.AdministrativeMode);
        Assert.Equal(string.Empty, listing.District);
        Assert.Equal("Phường Thạnh Mỹ Tây", listing.Ward);
        Assert.Equal(12, listing.Floors);
        Assert.Equal(2, listing.Bedrooms);
        Assert.Equal(2, listing.Bathrooms);
        Assert.Equal("Đông Nam", listing.BalconyDirection);
        Assert.Equal(79, listing.ProvinceCode);
        Assert.Equal(26956, listing.WardCode);
        Assert.Equal("Phường 22, Quận Bình Thạnh, Thành phố Hồ Chí Minh", listing.LegacyAddress);
    }

    [Fact]
    public void ListingShouldRejectPreMergerWithoutDistrictOrInvalidSpecs()
    {
        Assert.Throws<ArgumentException>(() => new Listing(
            ownerUserId: 1,
            title: "Nhà phố trung tâm",
            description: "Mô tả hợp lệ.",
            propertyType: "House",
            listingType: "Sale",
            price: 8_000_000_000m,
            area: 80m,
            city: "Thành phố Hồ Chí Minh",
            district: "",
            ward: "Phường Bến Nghé",
            address: "10 Lê Lợi",
            administrativeMode: "PreMerger"));

        Assert.Throws<ArgumentOutOfRangeException>(() => new Listing(
            ownerUserId: 1,
            title: "Nhà phố trung tâm",
            description: "Mô tả hợp lệ.",
            propertyType: "House",
            listingType: "Sale",
            price: 8_000_000_000m,
            area: 80m,
            city: "Thành phố Hồ Chí Minh",
            district: "Quận 1",
            ward: "Phường Bến Nghé",
            address: "10 Lê Lợi",
            floors: 0,
            administrativeMode: "PreMerger"));

        Assert.Throws<ArgumentOutOfRangeException>(() => new Listing(
            ownerUserId: 1,
            title: "Căn hộ mini",
            description: "Mô tả hợp lệ.",
            propertyType: "Apartment",
            listingType: "Rent",
            price: 8_000_000m,
            area: 30m,
            city: "Thành phố Hồ Chí Minh",
            district: "",
            ward: "Phường Sài Gòn",
            address: "10 Lê Lợi",
            bedrooms: -1));
    }

    [Fact]
    public async Task ListingServiceShouldPersistNewSpecsAndAddressFieldsAndTrackDeltas()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var dbContext = await CreateSqliteContextAsync(connection);

        var repository = new ListingRepository(dbContext);
        var service = new ListingService(repository, dbContext);

        var created = await service.CreateAsync(new CreateListingCommand(
            OwnerUserId: 1,
            Title: "Nhà phố 3 tầng Quận Bình Thạnh",
            Description: "Nhà mới xây, đầy đủ công năng sử dụng.",
            PropertyType: "House",
            ListingType: "Sale",
            Price: 6_500_000_000m,
            Area: 65m,
            City: "Thành phố Hồ Chí Minh",
            District: "",
            Ward: "Phường Thạnh Mỹ Tây",
            Address: "208 Nguyễn Hữu Cảnh",
            Floors: 3,
            Bedrooms: 3,
            Bathrooms: 3,
            BalconyDirection: "Không có ban công",
            AdministrativeMode: "PostMerger",
            ProvinceCode: 79,
            DistrictCode: null,
            WardCode: 26956,
            LegacyAddress: "Phường 22, Quận Bình Thạnh, Thành phố Hồ Chí Minh"));

        Assert.Equal(3, created.Floors);
        Assert.Equal(3, created.Bedrooms);
        Assert.Equal(3, created.Bathrooms);
        Assert.Equal("Không có ban công", created.BalconyDirection);
        Assert.Equal("PostMerger", created.AdministrativeMode);
        Assert.Equal("Phường 22, Quận Bình Thạnh, Thành phố Hồ Chí Minh", created.LegacyAddress);

        var updated = await service.UpdateAsync(new UpdateListingCommand(
            ListingId: created.ListingId,
            OwnerUserId: 1,
            Title: "Nhà phố 4 tầng Quận Bình Thạnh",
            Description: "Nhà mới nâng cấp thêm tầng.",
            PropertyType: "House",
            ListingType: "Sale",
            Price: 7_000_000_000m,
            Area: 65m,
            City: "Thành phố Hồ Chí Minh",
            District: "",
            Ward: "Phường Thạnh Mỹ Tây",
            Address: "208 Nguyễn Hữu Cảnh",
            RowVersion: created.RowVersion,
            Floors: 4,
            Bedrooms: 4,
            Bathrooms: 4,
            BalconyDirection: "Đông",
            AdministrativeMode: "PostMerger",
            ProvinceCode: 79,
            DistrictCode: null,
            WardCode: 26956,
            LegacyAddress: "Phường 22, Quận Bình Thạnh, Thành phố Hồ Chí Minh"));

        Assert.Equal(4, updated.Floors);
        Assert.Equal("Đông", updated.BalconyDirection);

        var histories = await dbContext.ListingHistories
            .AsNoTracking()
            .Where(h => h.ListingId == created.ListingId)
            .ToListAsync();
        var updateHistory = histories.Single(h => h.ActionType == "UpdateContent");
        Assert.NotNull(updateHistory.DeltasJson);
        Assert.Contains("\"Floors\"", updateHistory.DeltasJson);
        Assert.Contains("\"BalconyDirection\"", updateHistory.DeltasJson);
    }

    [Fact]
    public async Task VietnamAddressServiceShouldRecognizeLegacyAddressAndConvertToPostMerger()
    {
        using var handler = new StubHttpMessageHandler((request, _) =>
        {
            var pathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;
            string json = pathAndQuery switch
            {
                "/api/v2/?depth=2" => """
                    [
                      {
                        "name": "Thành phố Hồ Chí Minh",
                        "code": 79,
                        "division_type": "thành phố trung ương",
                        "codename": "ho_chi_minh",
                        "wards": [
                          { "name": "Phường Thạnh Mỹ Tây", "code": 26956, "division_type": "phường", "codename": "phuong_thanh_my_tay", "province_code": 79 },
                          { "name": "Phường Sài Gòn", "code": 26740, "division_type": "phường", "codename": "phuong_sai_gon", "province_code": 79 }
                        ]
                      }
                    ]
                    """,
                "/api/v1/?depth=3" => """
                    [
                      {
                        "name": "Thành phố Hồ Chí Minh",
                        "code": 79,
                        "division_type": "thành phố trung ương",
                        "codename": "thanh_pho_ho_chi_minh",
                        "districts": [
                          {
                            "name": "Quận Bình Thạnh",
                            "code": 765,
                            "division_type": "quận",
                            "codename": "quan_binh_thanh",
                            "province_code": 79,
                            "wards": [
                              { "name": "Phường 22", "code": 26956, "division_type": "phường", "codename": "phuong_22", "district_code": 765 }
                            ]
                          }
                        ]
                      }
                    ]
                    """,
                "/api/v2/w/from-legacy/?legacy_code=26956" => """
                    [
                      {
                        "source_code": 26956,
                        "ward": {
                          "name": "Phường Thạnh Mỹ Tây",
                          "code": 26956,
                          "division_type": "phường",
                          "codename": "phuong_thanh_my_tay",
                          "province_code": 79
                        }
                      }
                    ]
                    """,
                "/api/v2/w/26956/to-legacies/" => """
                    [
                      { "name": "Phường 19" },
                      { "name": "Phường 21" },
                      { "name": "Phường 22" }
                    ]
                    """,
                _ => "[]"
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://provinces.open-api.vn") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new VietnamAddressOptions());
        var service = new VietnamAddressService(httpClient, cache, options, NullLogger<VietnamAddressService>.Instance);

        var parsedLegacy = await service.ParseAndSuggestAddressAsync("208 Nguyễn Hữu Cảnh, Phường 22, Quận Bình Thạnh, TP. HCM");
        Assert.True(parsedLegacy.Matched);
        Assert.Equal("PreMerger", parsedLegacy.DetectedMode);
        Assert.Equal("208 Nguyễn Hữu Cảnh", parsedLegacy.StreetAddress);
        Assert.Equal(79, parsedLegacy.SuggestedProvinceCode);
        Assert.Equal("Thành phố Hồ Chí Minh", parsedLegacy.SuggestedProvinceName);
        Assert.Equal(26956, parsedLegacy.SuggestedWardCode);
        Assert.Equal("Phường Thạnh Mỹ Tây", parsedLegacy.SuggestedWardName);
        Assert.Equal("Phường 22, Quận Bình Thạnh, Thành phố Hồ Chí Minh", parsedLegacy.LegacyAddress);

        var parsedPost = await service.ParseAndSuggestAddressAsync("10 Lê Lợi, Phường Sài Gòn, Thành phố Hồ Chí Minh");
        Assert.True(parsedPost.Matched);
        Assert.Equal("PostMerger", parsedPost.DetectedMode);
        Assert.Equal("10 Lê Lợi", parsedPost.StreetAddress);
        Assert.Equal(26740, parsedPost.SuggestedWardCode);
        Assert.Equal("Phường Sài Gòn", parsedPost.SuggestedWardName);
    }

    [Fact]
    public async Task OllamaDescriptionGeneratorShouldUseOllamaAndFallbackGracefullyWhenOffline()
    {
        string? capturedBody = null;
        using var okHandler = new StubHttpMessageHandler(async (request, ct) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(ct);
            var json = JsonSerializer.Serialize(new
            {
                response = JsonSerializer.Serialize(new
                {
                    title = "Bán căn hộ 2PN 75m² Phường Thạnh Mỹ Tây, TP.HCM - 4.5 tỷ",
                    description = "Căn hộ 2 phòng ngủ tuyệt đẹp tại Phường Thạnh Mỹ Tây.\n- Diện tích 75m² thoáng sáng\n- Ban công hướng Đông Nam"
                })
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var okClient = new HttpClient(okHandler) { BaseAddress = new Uri("http://localhost:11434") };
        var options = Options.Create(new OllamaOptions { BaseUrl = "http://localhost:11434", Model = "qwen2.5:3b", Enabled = true });
        var generator = new OllamaListingDescriptionGenerator(okClient, options, NullLogger<OllamaListingDescriptionGenerator>.Instance);

        var command = new SuggestListingDescriptionCommand(
            Title: "Căn hộ view sông 2PN",
            PropertyType: "Apartment",
            ListingType: "sale",
            Price: 4_500_000_000m,
            Area: 75m,
            City: "Thành phố Hồ Chí Minh",
            District: "",
            Ward: "Phường Thạnh Mỹ Tây",
            Address: "208 Nguyễn Hữu Cảnh",
            LegacyAddress: "Phường 22, Quận Bình Thạnh, Thành phố Hồ Chí Minh",
            AdministrativeMode: "PostMerger",
            Floors: 12,
            Bedrooms: 2,
            Bathrooms: 2,
            BalconyDirection: "Đông Nam");

        var aiResult = await generator.GenerateDescriptionAsync(command);
        Assert.Equal("Ollama", aiResult.Source);
        Assert.Equal("qwen2.5:3b", aiResult.ModelName);
        Assert.Equal("Bán căn hộ 2PN 75m² Phường Thạnh Mỹ Tây, TP.HCM - 4.5 tỷ", aiResult.SuggestedTitle);
        Assert.Contains("<ul>", aiResult.SuggestedDescription);
        Assert.NotNull(capturedBody);
        Assert.Contains("Đông Nam", capturedBody);
        Assert.Contains("Phường 22, Quận Bình Thạnh", capturedBody);

        // Offline Ollama fallback
        using var failHandler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("Connection refused"));
        using var failClient = new HttpClient(failHandler) { BaseAddress = new Uri("http://localhost:11434") };
        var fallbackGenerator = new OllamaListingDescriptionGenerator(failClient, options, NullLogger<OllamaListingDescriptionGenerator>.Instance);

        var fallbackResult = await fallbackGenerator.GenerateDescriptionAsync(command);
        Assert.Equal("SmartTemplateFallback", fallbackResult.Source);
        Assert.Contains("Bán căn hộ 2PN 75m²", fallbackResult.SuggestedTitle);
        Assert.True(fallbackResult.SuggestedTitle.Length <= 100);
        Assert.Contains("2 phòng ngủ", fallbackResult.SuggestedDescription);
        Assert.Contains("Đông Nam", fallbackResult.SuggestedDescription);
        Assert.Contains("Phường 22, Quận Bình Thạnh", fallbackResult.SuggestedDescription);
    }

    private static async Task<PropNestDbContext> CreateSqliteContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<PropNestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new PropNestDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var seller = new ApplicationUser("seller@propnest.vn", "Seller Test", "0909123456", "Seller")
        {
            Id = 1,
            NormalizedEmail = "SELLER@PROPNEST.VN",
            NormalizedUserName = "SELLER@PROPNEST.VN"
        };
        context.Users.Add(seller);
        await context.SaveChangesAsync();

        return context;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request, cancellationToken);
    }
}
