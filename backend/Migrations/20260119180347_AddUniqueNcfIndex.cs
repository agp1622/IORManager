using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueNcfIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_NcfNumber",
                table: "FinancialDocument",
                column: "NcfNumber",
                unique: true,
                filter: "[NcfNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_NcfNumber",
                table: "FinancialDocument");
        }
    }
}
