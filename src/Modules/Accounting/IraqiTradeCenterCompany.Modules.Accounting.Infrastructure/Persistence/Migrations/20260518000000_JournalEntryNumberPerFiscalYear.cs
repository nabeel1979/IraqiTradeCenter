using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JournalEntryNumberPerFiscalYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) إسقاط الـ index الفريد القديم على EntryNumber فقط
            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JournalEntries_EntryNumber' AND object_id = OBJECT_ID('acc.JournalEntries'))
    DROP INDEX IX_JournalEntries_EntryNumber ON acc.JournalEntries;
");

            // 2) إعادة ترقيم القيود الموجودة لكل سنة مالية بشكل مستقل
            //    نستخدم ترقيم مؤقت أولاً ثم نهائياً لتجنب أي تعارض
            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

-- المرحلة 1: ترقيم مؤقت بقيم فريدة للقيود غير المحذوفة
UPDATE acc.JournalEntries
SET EntryNumber = 'TMP-' + CAST(Id AS NVARCHAR(20)) + '-' + REPLACE(CONVERT(NVARCHAR(40), NEWID()), '-', '')
WHERE IsDeleted = 0;

-- المرحلة 2: ترقيم نهائي حسب السنة المالية بترتيب زمني
;WITH Ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (PARTITION BY FiscalYearId ORDER BY EntryDate, Id) AS rn
    FROM acc.JournalEntries
    WHERE IsDeleted = 0
)
UPDATE J
SET EntryNumber = CAST(R.rn AS NVARCHAR(50))
FROM acc.JournalEntries J
INNER JOIN Ranked R ON R.Id = J.Id;
");

            // 3) إنشاء الـ composite unique index الجديد على (FiscalYearId, EntryNumber)
            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JournalEntries_FiscalYearId_EntryNumber' AND object_id = OBJECT_ID('acc.JournalEntries'))
    CREATE UNIQUE INDEX IX_JournalEntries_FiscalYearId_EntryNumber
        ON acc.JournalEntries(FiscalYearId, EntryNumber)
        WHERE IsDeleted = 0;
");

            // 4) إسقاط الـ Sequence القديم (لم يعد مستخدماً)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'SeqJournalEntryNumber' AND schema_id = SCHEMA_ID('acc'))
    DROP SEQUENCE acc.SeqJournalEntryNumber;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JournalEntries_FiscalYearId_EntryNumber' AND object_id = OBJECT_ID('acc.JournalEntries'))
    DROP INDEX IX_JournalEntries_FiscalYearId_EntryNumber ON acc.JournalEntries;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_JournalEntries_EntryNumber' AND object_id = OBJECT_ID('acc.JournalEntries'))
    CREATE UNIQUE INDEX IX_JournalEntries_EntryNumber ON acc.JournalEntries(EntryNumber) WHERE IsDeleted = 0;
");
        }
    }
}
