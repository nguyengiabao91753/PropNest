using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingSpecsAndVietnamAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdministrativeMode",
                table: "Listings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "PostMerger");

            migrationBuilder.AddColumn<string>(
                name: "BalconyDirection",
                table: "Listings",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bathrooms",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bedrooms",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DistrictCode",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Floors",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyAddress",
                table: "Listings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvinceCode",
                table: "Listings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WardCode",
                table: "Listings",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdministrativeMode",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "BalconyDirection",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Bathrooms",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Bedrooms",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "DistrictCode",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Floors",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "LegacyAddress",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "WardCode",
                table: "Listings");
        }
    }
}
