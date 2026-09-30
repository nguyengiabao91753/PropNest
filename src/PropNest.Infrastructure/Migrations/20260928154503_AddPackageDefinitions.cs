using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PropNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageDefinitions",
                columns: table => new
                {
                    PackageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PackageCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageDefinitions", x => x.PackageId);
                });

            migrationBuilder.InsertData(
                table: "PackageDefinitions",
                columns: new[] { "PackageId", "DurationDays", "IsActive", "Kind", "Name", "PackageCode", "Price" },
                values: new object[,]
                {
                    { 1L, 30, true, "Standard", "Gói Tiêu Chuẩn", "Standard", 50000m },
                    { 2L, 30, true, "VIP", "Gói VIP", "VIP", 150000m },
                    { 3L, 7, true, "Boost", "Gói Đẩy Tin", "Boost", 30000m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageDefinitions_PackageCode",
                table: "PackageDefinitions",
                column: "PackageCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageDefinitions");
        }
    }
}
