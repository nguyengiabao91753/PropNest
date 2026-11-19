using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSagaRefundSplitAndPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MainUsed",
                table: "PurchasePackageSagaStates",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PromoUsed",
                table: "PurchasePackageSagaStates",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId_CreatedAt",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Status_ModerationDecision_CreatedAt",
                table: "Listings",
                columns: new[] { "Status", "ModerationDecision", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_WalletId_CreatedAt",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Listings_Status_ModerationDecision_CreatedAt",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "MainUsed",
                table: "PurchasePackageSagaStates");

            migrationBuilder.DropColumn(
                name: "PromoUsed",
                table: "PurchasePackageSagaStates");
        }
    }
}
