using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherTypeIdToJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VoucherTypeId",
                schema: "acc",
                table: "JournalEntries",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_VoucherTypeId",
                schema: "acc",
                table: "JournalEntries",
                column: "VoucherTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_JournalVoucherTypes_VoucherTypeId",
                schema: "acc",
                table: "JournalEntries",
                column: "VoucherTypeId",
                principalSchema: "acc",
                principalTable: "JournalVoucherTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_JournalVoucherTypes_VoucherTypeId",
                schema: "acc",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_VoucherTypeId",
                schema: "acc",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "VoucherTypeId",
                schema: "acc",
                table: "JournalEntries");
        }
    }
}
