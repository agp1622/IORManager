using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrencyAndCulture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CultureName",
                table: "FinancialDocument",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                // match model default
                defaultValue: "en-US");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "FinancialDocument",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                // match model default
                defaultValue: "USD");

            migrationBuilder.CreateTable(
                name: "InvoiceNumberSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceNumberSequences", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "InvoiceNumberSequences",
                columns: new[] { "Id", "NextNumber" },
                values: new object[] { 1, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceNumberSequences");

            migrationBuilder.DropColumn(
                name: "CultureName",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "FinancialDocument");
        }
    }
}
