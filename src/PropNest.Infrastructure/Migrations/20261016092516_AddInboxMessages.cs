using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropNest.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProcessedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

           

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ConsumerName_MessageId",
                table: "InboxMessages",
                columns: new[] { "ConsumerName", "MessageId" },
                unique: true);

           
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

        }
    }
}
