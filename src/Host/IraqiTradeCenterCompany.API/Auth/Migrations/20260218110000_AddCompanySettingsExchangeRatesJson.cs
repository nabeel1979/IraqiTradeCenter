using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.API.Auth.Migrations;

/// <inheritdoc />
public partial class AddCompanySettingsExchangeRatesJson : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ExchangeRatesJson",
            schema: "auth",
            table: "CompanySettings",
            type: "nvarchar(max)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ExchangeRatesJson",
            schema: "auth",
            table: "CompanySettings");
    }
}
