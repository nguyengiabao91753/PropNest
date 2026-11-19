using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PropNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPaidPackagesAndCalendarLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PackageEndDate",
                table: "Listings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 1L,
                columns: new[] { "DurationDays", "IsActive", "Name" },
                values: new object[] { 15, false, "Gói Tiêu Chuẩn (Cũ)" });

            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 2L,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { false, "Gói VIP (Cũ)" });

            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 3L,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { false, "Gói Đẩy Tin (Ẩn)" });

            migrationBuilder.InsertData(
                table: "PackageDefinitions",
                columns: new[] { "PackageId", "DurationDays", "IsActive", "Kind", "Name", "PackageCode", "Price" },
                values: new object[,]
                {
                    { 4L, 7, true, "Standard", "Gói Tiêu Chuẩn - 7 ngày", "STANDARD_7", 30000m },
                    { 5L, 15, true, "Standard", "Gói Tiêu Chuẩn - 15 ngày", "STANDARD_15", 55000m },
                    { 6L, 30, true, "Standard", "Gói Tiêu Chuẩn - 30 ngày", "STANDARD_30", 99000m },
                    { 7L, 7, true, "VIP", "Gói VIP Nổi Bật - 7 ngày", "VIP_7", 90000m },
                    { 8L, 15, true, "VIP", "Gói VIP Nổi Bật - 15 ngày", "VIP_15", 170000m },
                    { 9L, 30, true, "VIP", "Gói VIP Nổi Bật - 30 ngày", "VIP_30", 299000m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_PackageCode_PackageEndDate",
                table: "Listings",
                columns: new[] { "Status", "PackageCode", "PackageEndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_Status_PackageCode_PackageEndDate",
                table: "Listings");

            migrationBuilder.DeleteData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 4L);

            migrationBuilder.DeleteData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 5L);

            migrationBuilder.DeleteData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 6L);

            migrationBuilder.DeleteData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 7L);

            migrationBuilder.DeleteData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 8L);

            migrationBuilder.DeleteData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 9L);

            migrationBuilder.DropColumn(
                name: "PackageEndDate",
                table: "Listings");

            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 1L,
                columns: new[] { "DurationDays", "IsActive", "Name" },
                values: new object[] { 30, true, "Gói Tiêu Chuẩn (Miễn phí)" });

            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 2L,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { true, "Gói VIP" });

            migrationBuilder.UpdateData(
                table: "PackageDefinitions",
                keyColumn: "PackageId",
                keyValue: 3L,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { true, "Gói Đẩy Tin" });
        }
    }
}
