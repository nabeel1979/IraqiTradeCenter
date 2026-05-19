using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalVoucherTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalVoucherTypes",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DefaultDebitAccountId = table.Column<int>(type: "int", nullable: true),
                    DefaultCreditAccountId = table.Column<int>(type: "int", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalVoucherTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalVoucherTypes_Accounts_DefaultCreditAccountId",
                        column: x => x.DefaultCreditAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalVoucherTypes_Accounts_DefaultDebitAccountId",
                        column: x => x.DefaultDebitAccountId,
                        principalSchema: "acc",
                        principalTable: "Accounts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherTypes_Code",
                schema: "acc",
                table: "JournalVoucherTypes",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherTypes_DefaultCreditAccountId",
                schema: "acc",
                table: "JournalVoucherTypes",
                column: "DefaultCreditAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherTypes_DefaultDebitAccountId",
                schema: "acc",
                table: "JournalVoucherTypes",
                column: "DefaultDebitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalVoucherTypes_DisplayOrder",
                schema: "acc",
                table: "JournalVoucherTypes",
                column: "DisplayOrder");

            // ─────────────────────────────────────────────────────────────
            // بذور افتراضية: 3 أنواع شائعة (يتم ربط الحسابات لاحقاً من الواجهة)
            // ─────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
INSERT INTO [acc].[JournalVoucherTypes]
    ([Code], [NameAr], [NameEn], [Description], [IsEnabled], [IsSystem], [DisplayOrder], [CreatedAt], [IsDeleted])
VALUES
    (N'JV',  N'قيد محاسبي عام',     N'Journal Voucher',    N'قيد محاسبي يدوي عام',                                  1, 1, 10,  SYSUTCDATETIME(), 0),
    (N'RV',  N'سند قبض',           N'Receipt Voucher',     N'استلام نقدي من العملاء أو الإيرادات',                  1, 1, 20,  SYSUTCDATETIME(), 0),
    (N'PV',  N'سند دفع',           N'Payment Voucher',     N'صرف نقدي للموردين أو المصروفات',                       1, 1, 30,  SYSUTCDATETIME(), 0),
    (N'AV',  N'سند تسوية',          N'Adjustment Voucher',  N'قيود التسوية والمناقلة بين الحسابات',                  1, 1, 40,  SYSUTCDATETIME(), 0),
    (N'OV',  N'قيد افتتاحي',        N'Opening Voucher',     N'الأرصدة الافتتاحية لبداية الفترة المحاسبية',           1, 1, 50,  SYSUTCDATETIME(), 0),
    (N'CV',  N'قيد إقفال',          N'Closing Voucher',     N'إقفال حسابات النتيجة في نهاية الفترة',                 1, 1, 60,  SYSUTCDATETIME(), 0);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalVoucherTypes",
                schema: "acc");
        }
    }
}
