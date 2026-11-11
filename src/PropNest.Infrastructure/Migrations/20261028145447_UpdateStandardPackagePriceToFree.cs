using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStandardPackagePriceToFree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 1L,
                columns: new[] { "Name", "Price" },
                values: new object[] { "Gói Tiêu Chuẩn (Miễn phí)", 0m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 1L,
                columns: new[] { "Name", "Price" },
                values: new object[] { "Gói Tiêu Chuẩn", 50000m });
        }
    }
}
