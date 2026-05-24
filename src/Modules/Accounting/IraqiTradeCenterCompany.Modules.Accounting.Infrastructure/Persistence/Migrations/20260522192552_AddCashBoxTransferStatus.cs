using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashBoxTransferStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ReceiveJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiveNotes",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReceivedAt",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceivedByUserId",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversalJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ‎المناقلات الموجودة قبل هذه الترقية كانت تُولِّد قيدَيْن دفعةً واحدة،
            // ‎لذا نضع حالتها = Received (1) بدلاً من PendingReceive (0) لأن
            // ‎قيد الاستلام (ReceiveJournalEntryId) موجود فعلاً.
            migrationBuilder.Sql(@"
                UPDATE [acc].[CashBoxTransfers]
                SET [Status] = 1
                WHERE [ReceiveJournalEntryId] IS NOT NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_ReversalJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_Status",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_CashBoxTransfers_JournalEntries_ReversalJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "ReversalJournalEntryId",
                principalSchema: "acc",
                principalTable: "JournalEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashBoxTransfers_JournalEntries_ReversalJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropIndex(
                name: "IX_CashBoxTransfers_ReversalJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropIndex(
                name: "IX_CashBoxTransfers_Status",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "ReceiveNotes",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "ReceivedAt",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "ReceivedByUserId",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "ReversalJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "acc",
                table: "CashBoxTransfers");

            migrationBuilder.AlterColumn<int>(
                name: "ReceiveJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
