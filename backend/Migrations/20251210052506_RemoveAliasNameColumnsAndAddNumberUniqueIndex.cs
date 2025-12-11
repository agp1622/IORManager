using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAliasNameColumnsAndAddNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize duplicate document numbers before creating the unique index.
            // Any duplicates are suffixed deterministically so the migration can succeed
            // without manual data cleanup.
            migrationBuilder.Sql(@"
;WITH numbered AS (
    SELECT Id,
           Number,
           ROW_NUMBER() OVER (PARTITION BY Number ORDER BY Id) AS rn
    FROM FinancialDocument
)
UPDATE numbered
SET Number = CONCAT(
    LEFT(Number, 41),
    '-D',
    RIGHT('000000' + CAST(rn AS varchar(6)), 6)
)
WHERE rn > 1;
");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Receipt_CustomerName",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "SupplierName",
                table: "FinancialDocument");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_Number",
                table: "FinancialDocument",
                column: "Number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_Number",
                table: "FinancialDocument");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "FinancialDocument",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Receipt_CustomerName",
                table: "FinancialDocument",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierName",
                table: "FinancialDocument",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
