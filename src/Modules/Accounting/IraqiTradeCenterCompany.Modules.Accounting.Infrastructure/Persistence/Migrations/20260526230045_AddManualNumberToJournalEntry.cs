using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualNumberToJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManualNumber",
                schema: "acc",
                table: "JournalEntries",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ManualNumber",
                schema: "acc",
                table: "JournalEntries",
                column: "ManualNumber",
                filter: "[ManualNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_ManualNumber",
                schema: "acc",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ManualNumber",
                schema: "acc",
                table: "JournalEntries");
        }
    }
}
