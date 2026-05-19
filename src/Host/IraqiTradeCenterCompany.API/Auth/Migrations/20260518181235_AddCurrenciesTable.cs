using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.API.Auth.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrenciesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "auth",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Symbol = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsBase = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsBase",
                schema: "auth",
                table: "Currencies",
                column: "IsBase");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsEnabled",
                schema: "auth",
                table: "Currencies",
                column: "IsEnabled");

            // ─────────────────────────────────────────────────────
            // بذور قائمة العملات (ISO 4217)
            //   - IQD: العملة الرئيسية الافتراضية + مفعّلة
            //   - أبرز العملات الإقليمية والعالمية: مفعّلة افتراضياً
            //   - باقي العملات: مدرجة لكن غير مفعّلة - يمكن للمستخدم تفعيلها لاحقاً
            // ─────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
DECLARE @now datetime2 = SYSUTCDATETIME();

;WITH src(Code, NameAr, NameEn, Symbol, DecimalPlaces, IsEnabled, IsBase, DisplayOrder) AS (
    SELECT * FROM (VALUES
        -- العملة الرئيسية
        ('IQD', N'الدينار العراقي',     'Iraqi Dinar',          N'د.ع',  0, 1, 1, 1),
        -- خليجية وإقليمية مُفعّلة افتراضياً
        ('USD', N'الدولار الأمريكي',    'US Dollar',            '$',    2, 1, 0, 10),
        ('EUR', N'اليورو',               'Euro',                 N'€',   2, 1, 0, 11),
        ('SAR', N'الريال السعودي',      'Saudi Riyal',          N'﷼',   2, 1, 0, 20),
        ('AED', N'الدرهم الإماراتي',    'UAE Dirham',           N'د.إ', 2, 1, 0, 21),
        ('KWD', N'الدينار الكويتي',     'Kuwaiti Dinar',        N'د.ك', 3, 1, 0, 22),
        ('JOD', N'الدينار الأردني',     'Jordanian Dinar',      N'د.أ', 3, 1, 0, 23),
        ('TRY', N'الليرة التركية',      'Turkish Lira',         N'₺',   2, 1, 0, 30),
        -- باقي العربية (متاحة - غير مفعلة افتراضياً)
        ('QAR', N'الريال القطري',       'Qatari Riyal',         N'ر.ق', 2, 0, 0, 24),
        ('BHD', N'الدينار البحريني',    'Bahraini Dinar',       N'.د.ب',3, 0, 0, 25),
        ('OMR', N'الريال العماني',      'Omani Rial',           N'﷼',   3, 0, 0, 26),
        ('EGP', N'الجنيه المصري',       'Egyptian Pound',       N'ج.م', 2, 0, 0, 27),
        ('LBP', N'الليرة اللبنانية',    'Lebanese Pound',       N'ل.ل', 2, 0, 0, 28),
        ('SYP', N'الليرة السورية',      'Syrian Pound',         N'ل.س', 2, 0, 0, 29),
        ('YER', N'الريال اليمني',       'Yemeni Rial',          N'﷼',   2, 0, 0, 31),
        ('LYD', N'الدينار الليبي',      'Libyan Dinar',         N'ل.د', 3, 0, 0, 32),
        ('TND', N'الدينار التونسي',     'Tunisian Dinar',       N'د.ت', 3, 0, 0, 33),
        ('DZD', N'الدينار الجزائري',    'Algerian Dinar',       N'د.ج', 2, 0, 0, 34),
        ('MAD', N'الدرهم المغربي',      'Moroccan Dirham',      N'د.م.',2, 0, 0, 35),
        ('SDG', N'الجنيه السوداني',     'Sudanese Pound',       N'ج.س', 2, 0, 0, 36),
        -- عملات عالمية رئيسية
        ('GBP', N'الجنيه الإسترليني',   'British Pound',        N'£',   2, 0, 0, 40),
        ('JPY', N'الين الياباني',       'Japanese Yen',         N'¥',   0, 0, 0, 41),
        ('CHF', N'الفرنك السويسري',     'Swiss Franc',          'CHF',  2, 0, 0, 42),
        ('CNY', N'اليوان الصيني',       'Chinese Yuan',         N'¥',   2, 0, 0, 43),
        ('AUD', N'الدولار الأسترالي',   'Australian Dollar',    'A$',   2, 0, 0, 44),
        ('CAD', N'الدولار الكندي',      'Canadian Dollar',      'C$',   2, 0, 0, 45),
        ('SEK', N'الكرونة السويدية',    'Swedish Krona',        'kr',   2, 0, 0, 46),
        ('NOK', N'الكرونة النرويجية',   'Norwegian Krone',      'kr',   2, 0, 0, 47),
        ('DKK', N'الكرونة الدنماركية',  'Danish Krone',         'kr',   2, 0, 0, 48),
        ('SGD', N'الدولار السنغافوري',  'Singapore Dollar',     'S$',   2, 0, 0, 49),
        ('HKD', N'الدولار الهونغ كونغي','Hong Kong Dollar',     'HK$',  2, 0, 0, 50),
        ('NZD', N'الدولار النيوزيلندي', 'New Zealand Dollar',   'NZ$',  2, 0, 0, 51),
        -- آسيوية أخرى
        ('IRR', N'الريال الإيراني',     'Iranian Rial',         N'﷼',   2, 0, 0, 60),
        ('INR', N'الروبية الهندية',     'Indian Rupee',         N'₹',   2, 0, 0, 61),
        ('PKR', N'الروبية الباكستانية', 'Pakistani Rupee',      N'₨',   2, 0, 0, 62),
        ('AFN', N'الأفغاني',            'Afghan Afghani',       N'؋',   2, 0, 0, 63),
        ('KRW', N'الوون الكوري الجنوبي','South Korean Won',     N'₩',   0, 0, 0, 64),
        ('THB', N'البات التايلاندي',    'Thai Baht',            N'฿',   2, 0, 0, 65),
        ('MYR', N'الرنغيت الماليزي',    'Malaysian Ringgit',    'RM',   2, 0, 0, 66),
        ('IDR', N'الروبية الإندونيسية', 'Indonesian Rupiah',    'Rp',   2, 0, 0, 67),
        ('PHP', N'البيزو الفلبيني',     'Philippine Peso',      N'₱',   2, 0, 0, 68),
        -- روسية وشرق أوروبا
        ('RUB', N'الروبل الروسي',       'Russian Ruble',        N'₽',   2, 0, 0, 70),
        ('UAH', N'الهريفنا الأوكرانية', 'Ukrainian Hryvnia',    N'₴',   2, 0, 0, 71),
        ('PLN', N'الزلوتي البولندي',    'Polish Zloty',         N'zł',  2, 0, 0, 72),
        ('CZK', N'الكورونا التشيكية',   'Czech Koruna',         N'Kč',  2, 0, 0, 73),
        -- إفريقية بارزة
        ('NGN', N'النايرة النيجيرية',   'Nigerian Naira',       N'₦',   2, 0, 0, 80),
        ('ZAR', N'الراند الجنوب أفريقي','South African Rand',   'R',    2, 0, 0, 81),
        ('KES', N'الشلن الكيني',        'Kenyan Shilling',      'KSh',  2, 0, 0, 82),
        ('ETB', N'البر الإثيوبي',       'Ethiopian Birr',       N'Br',  2, 0, 0, 83)
    ) AS V(Code, NameAr, NameEn, Symbol, DecimalPlaces, IsEnabled, IsBase, DisplayOrder)
)
INSERT INTO [auth].[Currencies]
    (Code, NameAr, NameEn, Symbol, DecimalPlaces, IsEnabled, IsBase, DisplayOrder, CreatedAt, UpdatedAt, UpdatedBy)
SELECT s.Code, s.NameAr, s.NameEn, s.Symbol, s.DecimalPlaces, s.IsEnabled, s.IsBase, s.DisplayOrder, @now, @now, 'system'
FROM src s
WHERE NOT EXISTS (SELECT 1 FROM [auth].[Currencies] c WHERE c.Code = s.Code);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "auth");
        }
    }
}
