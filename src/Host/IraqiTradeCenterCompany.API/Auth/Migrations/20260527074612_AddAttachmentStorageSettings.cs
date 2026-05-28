using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.API.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentStorageSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttachmentStorageSettings",
                schema: "auth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LocalRootPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    R2AccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    R2AccessKeyId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    R2SecretAccessKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    R2Bucket = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    R2PublicBaseUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttachmentStorageSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttachmentStorageSettings",
                schema: "auth");
        }
    }
}
