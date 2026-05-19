using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashBoxesAndVoucherNature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Nature",
                schema: "acc",
                table: "JournalVoucherTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInSidebar",
                schema: "acc",
                table: "JournalVoucherTypes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CashBoxes",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBoxes_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CashBoxCurrencies",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CashBoxId = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    DebitLimit = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    CreditLimit = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashBoxCurrencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBoxCurrencies_CashBoxes_CashBoxId",
                        column: x => x.CashBoxId,
                        principalSchema: "acc",
                        principalTable: "CashBoxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxCurrencies_CashBoxId_Currency",
                schema: "acc",
                table: "CashBoxCurrencies",
                columns: new[] { "CashBoxId", "Currency" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxes_AccountId",
                schema: "acc",
                table: "CashBoxes",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxes_Code",
                schema: "acc",
                table: "CashBoxes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CashBoxes_DisplayOrder",
                schema: "acc",
                table: "CashBoxes",
                column: "DisplayOrder");

            // تهيئة طبيعة وإظهار الأنواع الافتراضية:
            //  - سند قبض RV: مدين، يظهر بالقائمة
            //  - سند دفع PV: دائن، يظهر بالقائمة
            //  - باقي الأنواع تبقى Mixed ولا تظهر منفصلة (تُستخدم من شاشة القيود)
            migrationBuilder.Sql(@"
UPDATE [acc].[JournalVoucherTypes]
SET [Nature] = 1, [ShowInSidebar] = 1
WHERE [Code] = 'RV';

UPDATE [acc].[JournalVoucherTypes]
SET [Nature] = 2, [ShowInSidebar] = 1
WHERE [Code] = 'PV';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashBoxCurrencies",
                schema: "acc");

            migrationBuilder.DropTable(
                name: "CashBoxes",
                schema: "acc");

            migrationBuilder.DropColumn(
                name: "Nature",
                schema: "acc",
                table: "JournalVoucherTypes");

            migrationBuilder.DropColumn(
                name: "ShowInSidebar",
                schema: "acc",
                table: "JournalVoucherTypes");
        }
    }
}
