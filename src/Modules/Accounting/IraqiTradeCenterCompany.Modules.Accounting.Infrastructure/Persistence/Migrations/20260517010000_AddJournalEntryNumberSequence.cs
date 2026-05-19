using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IraqiTradeCenterCompany.Modules.Accounting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntryNumberSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) إعادة ترقيم القيود الموجودة بترتيب زمني (1, 2, 3, ...)
            // 2) إنشاء Sequence مخصصة تبدأ بعد آخر رقم
            // 3) عند الإنشاء الجديد: NEXT VALUE FOR acc.SeqJournalEntryNumber
            migrationBuilder.Sql(@"
SET NOCOUNT ON;

-- إعادة ترقيم القيود الموجودة بترتيب زمني
;WITH Ranked AS (
    SELECT Id, ROW_NUMBER() OVER (ORDER BY EntryDate, Id) AS rn
    FROM acc.JournalEntries
    WHERE IsDeleted = 0
)
UPDATE J
SET EntryNumber = CAST(R.rn AS NVARCHAR(50))
FROM acc.JournalEntries J
INNER JOIN Ranked R ON R.Id = J.Id;

-- حساب الرقم التالي
DECLARE @maxNum BIGINT;
SELECT @maxNum = ISNULL(MAX(TRY_CAST(EntryNumber AS BIGINT)), 0)
FROM acc.JournalEntries
WHERE IsDeleted = 0 AND TRY_CAST(EntryNumber AS BIGINT) IS NOT NULL;

DECLARE @startVal BIGINT = @maxNum + 1;

-- إنشاء أو إعادة تشغيل Sequence
IF NOT EXISTS (
    SELECT 1 FROM sys.sequences
    WHERE name = 'SeqJournalEntryNumber' AND schema_id = SCHEMA_ID('acc')
)
BEGIN
    DECLARE @createSql NVARCHAR(MAX) = N'CREATE SEQUENCE acc.SeqJournalEntryNumber AS BIGINT START WITH '
        + CAST(@startVal AS NVARCHAR(20))
        + N' INCREMENT BY 1 NO CYCLE CACHE 10;';
    EXEC sp_executesql @createSql;
END
ELSE
BEGIN
    DECLARE @restartSql NVARCHAR(MAX) = N'ALTER SEQUENCE acc.SeqJournalEntryNumber RESTART WITH '
        + CAST(@startVal AS NVARCHAR(20)) + N';';
    EXEC sp_executesql @restartSql;
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.sequences
    WHERE name = 'SeqJournalEntryNumber' AND schema_id = SCHEMA_ID('acc')
)
    DROP SEQUENCE acc.SeqJournalEntryNumber;
");
        }
    }
}
