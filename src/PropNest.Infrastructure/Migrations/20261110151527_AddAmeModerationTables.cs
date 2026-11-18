using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PropNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAmeModerationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AmeModerationResults",
                columns: table => new
                {
                    ResultId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ListingId = table.Column<long>(type: "bigint", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ScoreJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelInfo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmeModerationResults", x => x.ResultId);
                    table.ForeignKey(
                        name: "FK_AmeModerationResults_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "ListingId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentRules",
                columns: table => new
                {
                    RuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RuleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Pattern = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRules", x => x.RuleId);
                });

            migrationBuilder.CreateTable(
                name: "MarketPriceBenchmarks",
                columns: table => new
                {
                    BenchmarkId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DistrictCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ListingType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MinPricePerSqm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AvgPricePerSqm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxPricePerSqm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPriceBenchmarks", x => x.BenchmarkId);
                });

            migrationBuilder.InsertData(
                table: "ContentRules",
                columns: new[] { "RuleId", "Description", "IsActive", "Pattern", "RuleType", "Severity", "UpdatedAt" },
                values: new object[,]
                {
                    { 1L, "Từ khóa vi phạm pháp luật hoặc lừa đảo nghiêm trọng", true, "lừa đảo|cờ bạc|cá độ|tín dụng đen", "BadWord", "Reject", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 2L, "Từ khóa cam kết tài chính sai sự thật hoặc giấy tờ bất hợp pháp", true, "cam kết lợi nhuận 100%|x2 tài sản sau 1 tháng|sổ đỏ giả", "BadWord", "Reject", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 3L, "Chặn chèn số điện thoại trực tiếp trong tiêu đề hoặc nội dung mô tả", true, "(?:\\+84|0)(?:3|5|7|8|9)\\d(?:[\\s\\.\\-]?\\d){7}", "PhonePattern", "Reject", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 4L, "Tỷ lệ chữ IN HOA vượt quá 50% gây phản cảm hoặc spam", true, "UPPERCASE_RATIO_EXCEEDED:0.5", "Formatting", "ManualReview", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { 5L, "Mô tả tin đăng quá ngắn (dưới 30 ký tự), thiếu thông tin chi tiết", true, "MIN_DESCRIPTION_LENGTH:30", "Formatting", "Warning", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                table: "MarketPriceBenchmarks",
                columns: new[] { "BenchmarkId", "AvgPricePerSqm", "CityCode", "DistrictCode", "EffectiveFrom", "ListingType", "MaxPricePerSqm", "MinPricePerSqm", "PropertyType" },
                values: new object[,]
                {
                    { 1L, 150000000m, "TP. Hồ Chí Minh", "Quận 1", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sale", 350000000m, 80000000m, "Apartment" },
                    { 2L, 65000000m, "TP. Hồ Chí Minh", "Quận 7", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sale", 130000000m, 35000000m, "Apartment" },
                    { 3L, 120000000m, "TP. Hồ Chí Minh", "Quận 7", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sale", 250000000m, 60000000m, "House" },
                    { 4L, 55000000m, "TP. Hồ Chí Minh", "Thành phố Thủ Đức", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sale", 120000000m, 30000000m, "Apartment" },
                    { 5L, 70000000m, "Hà Nội", "Quận Cầu Giấy", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Sale", 130000000m, 40000000m, "Apartment" },
                    { 6L, 220000m, "TP. Hồ Chí Minh", "Quận 7", new DateTimeOffset(new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Rent", 450000m, 120000m, "Apartment" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AmeModerationResults_CorrelationId",
                table: "AmeModerationResults",
                column: "CorrelationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AmeModerationResults_ListingId_CheckedAt",
                table: "AmeModerationResults",
                columns: new[] { "ListingId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentRules_RuleType_IsActive",
                table: "ContentRules",
                columns: new[] { "RuleType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketPriceBenchmarks_CityCode_DistrictCode_PropertyType_ListingType_EffectiveFrom",
                table: "MarketPriceBenchmarks",
                columns: new[] { "CityCode", "DistrictCode", "PropertyType", "ListingType", "EffectiveFrom" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AmeModerationResults");

            migrationBuilder.DropTable(
                name: "ContentRules");

            migrationBuilder.DropTable(
                name: "MarketPriceBenchmarks");
        }
    }
}
