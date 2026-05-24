using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashBoxTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashBoxTransfers",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromCashBoxId = table.Column<int>(type: "int", nullable: false),
                    ToCashBoxId = table.Column<int>(type: "int", nullable: false),
                    TransitAccountId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    SendDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SendJournalEntryId = table.Column<int>(type: "int", nullable: false),
                    ReceiveJournalEntryId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBoxTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBoxTransfers_Accounts_TransitAccountId",
                        column: x => x.TransitAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxTransfers_CashBoxes_FromCashBoxId",
                        column: x => x.FromCashBoxId,
                        principalSchema: "acc",
                        principalTable: "CashBoxes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxTransfers_CashBoxes_ToCashBoxId",
                        column: x => x.ToCashBoxId,
                        principalSchema: "acc",
                        principalTable: "CashBoxes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxTransfers_JournalEntries_ReceiveJournalEntryId",
                        column: x => x.ReceiveJournalEntryId,
                        principalSchema: "acc",
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashBoxTransfers_JournalEntries_SendJournalEntryId",
                        column: x => x.SendJournalEntryId,
                        principalSchema: "acc",
                        principalTable: "JournalEntries",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_FromCashBoxId",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "FromCashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_ReceiveDate",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "ReceiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_ReceiveJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "ReceiveJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_SendDate",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "SendDate");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_SendJournalEntryId",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "SendJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_ToCashBoxId",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "ToCashBoxId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_TransferNumber",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "TransferNumber",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxTransfers_TransitAccountId",
                schema: "acc",
                table: "CashBoxTransfers",
                column: "TransitAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashBoxTransfers",
                schema: "acc");
        }
    }
}
