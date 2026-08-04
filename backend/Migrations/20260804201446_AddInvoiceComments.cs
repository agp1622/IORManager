using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Quote_Comments",
                table: "FinancialDocument",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            // Invoice now also owns a (disambiguated, unprefixed) "Comments" column, which pushed
            // EF to move Quote's existing column to "Quote_Comments". Carry over the data that
            // already lived in the old shared column so existing quotes don't lose it.
            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [Quote_Comments] = [Comments],
                    [Comments] = NULL
                WHERE [DocumentType] = 'Quote' AND [Comments] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [Comments] = [Quote_Comments]
                WHERE [DocumentType] = 'Quote' AND [Quote_Comments] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Quote_Comments",
                table: "FinancialDocument");
        }
    }
}
