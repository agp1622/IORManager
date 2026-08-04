using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceCustomerPONumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Quote_CustomerPONumber",
                table: "FinancialDocument",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Invoice now also owns a (disambiguated, unprefixed) "CustomerPONumber" column, which
            // pushed EF to move Quote's existing column to "Quote_CustomerPONumber". Carry over the
            // data that already lived in the old shared column so existing quotes don't lose it.
            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [Quote_CustomerPONumber] = [CustomerPONumber],
                    [CustomerPONumber] = NULL
                WHERE [DocumentType] = 'Quote' AND [CustomerPONumber] IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [CustomerPONumber] = [Quote_CustomerPONumber]
                WHERE [DocumentType] = 'Quote' AND [Quote_CustomerPONumber] IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "Quote_CustomerPONumber",
                table: "FinancialDocument");
        }
    }
}
