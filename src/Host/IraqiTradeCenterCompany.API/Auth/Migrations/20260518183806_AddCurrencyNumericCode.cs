using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.API.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyNumericCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumericCode",
                schema: "auth",
                table: "Currencies",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            // تعبئة الأرقام العالمية ISO 4217 لكل العملات المعروفة
            migrationBuilder.Sql(@"
;WITH src(Code, NumericCode) AS (
    SELECT * FROM (VALUES
        ('IQD','368'), ('USD','840'), ('EUR','978'), ('SAR','682'), ('AED','784'),
        ('KWD','414'), ('JOD','400'), ('TRY','949'), ('QAR','634'), ('BHD','048'),
        ('OMR','512'), ('EGP','818'), ('LBP','422'), ('SYP','760'), ('YER','886'),
        ('LYD','434'), ('TND','788'), ('DZD','012'), ('MAD','504'), ('SDG','938'),
        ('GBP','826'), ('JPY','392'), ('CHF','756'), ('CNY','156'), ('AUD','036'),
        ('CAD','124'), ('SEK','752'), ('NOK','578'), ('DKK','208'), ('SGD','702'),
        ('HKD','344'), ('NZD','554'), ('IRR','364'), ('INR','356'), ('PKR','586'),
        ('AFN','971'), ('KRW','410'), ('THB','764'), ('MYR','458'), ('IDR','360'),
        ('PHP','608'), ('RUB','643'), ('UAH','980'), ('PLN','985'), ('CZK','203'),
        ('NGN','566'), ('ZAR','710'), ('KES','404'), ('ETB','230')
    ) AS V(Code, NumericCode)
)
UPDATE c SET c.NumericCode = s.NumericCode
FROM [auth].[Currencies] c
INNER JOIN src s ON s.Code = c.Code;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumericCode",
                schema: "auth",
                table: "Currencies");
        }
    }
}
