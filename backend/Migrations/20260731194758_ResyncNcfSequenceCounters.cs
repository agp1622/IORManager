using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class ResyncNcfSequenceCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data-only fix: NcfSequences.NextNumber was never synced to historical/seeded
            // invoice data, so it could issue an NCF number lower than one already used in
            // the same category (a DGII compliance violation). This advances each category's
            // counter to (highest already-issued sequence number in that category) + 1.
            // The WHERE guard only ever moves a counter forward, never backward, so this is
            // safe to run even if a category's counter is already correct or ahead.
            migrationBuilder.Sql(@"
;WITH ParsedNcf AS (
    SELECT NcfCategory,
           TRY_CAST(SUBSTRING(NcfNumber, LEN(NcfCategory) + 1, 20) AS BIGINT) AS SeqNumber
    FROM FinancialDocument
    WHERE DocumentType = 'Invoice'
      AND NcfNumber IS NOT NULL
      AND NcfCategory IS NOT NULL
      AND NcfNumber LIKE NcfCategory + '%'
),
MaxByCategory AS (
    SELECT NcfCategory, MAX(SeqNumber) AS MaxSeq
    FROM ParsedNcf
    WHERE SeqNumber IS NOT NULL
    GROUP BY NcfCategory
)
UPDATE ns
SET ns.NextNumber = mx.MaxSeq + 1
FROM NcfSequences ns
INNER JOIN MaxByCategory mx ON mx.NcfCategory = ns.CategoryCode
WHERE mx.MaxSeq + 1 > ns.NextNumber;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible: this is a data correction, not a schema change.
            // Rolling counters back to their previous (wrong) values would reintroduce the bug.
        }
    }
}
