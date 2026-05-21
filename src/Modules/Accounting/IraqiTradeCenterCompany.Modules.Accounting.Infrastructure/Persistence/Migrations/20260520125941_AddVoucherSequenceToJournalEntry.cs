using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherSequenceToJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VoucherSequence",
                schema: "acc",
                table: "JournalEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_VoucherTypeId_VoucherSequence",
                schema: "acc",
                table: "JournalEntries",
                columns: new[] { "VoucherTypeId", "VoucherSequence" },
                unique: true,
                filter: "[VoucherTypeId] IS NOT NULL AND [VoucherSequence] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_VoucherTypeId_VoucherSequence",
                schema: "acc",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "VoucherSequence",
                schema: "acc",
                table: "JournalEntries");
        }
    }
}
