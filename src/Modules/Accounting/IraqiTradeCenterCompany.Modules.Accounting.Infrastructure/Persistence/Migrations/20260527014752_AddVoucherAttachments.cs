using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoucherAttachments",
                schema: "acc",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalEntryId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StorageProvider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherAttachments_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalSchema: "acc",
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAttachments_IsDeleted",
                schema: "acc",
                table: "VoucherAttachments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAttachments_JournalEntryId_UploadedAtUtc",
                schema: "acc",
                table: "VoucherAttachments",
                columns: new[] { "JournalEntryId", "UploadedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VoucherAttachments_UploadedByUserId",
                schema: "acc",
                table: "VoucherAttachments",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoucherAttachments",
                schema: "acc");
        }
    }
}
