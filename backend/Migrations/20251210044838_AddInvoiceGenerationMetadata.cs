using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceGenerationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerAddress",
                table: "FinancialDocument",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerContact",
                table: "FinancialDocument",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoiceGeneratedAt",
                table: "FinancialDocument",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NcfNumber",
                table: "FinancialDocument",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NcfSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NcfSequences", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "NcfSequences",
                columns: new[] { "Id", "NextNumber" },
                values: new object[] { 1, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NcfSequences");

            migrationBuilder.DropColumn(
                name: "CustomerAddress",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "CustomerContact",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "InvoiceGeneratedAt",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "NcfNumber",
                table: "FinancialDocument");
        }
    }
}
