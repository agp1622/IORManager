using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class SplitQuotesFromInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedAt",
                table: "FinancialDocument",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedInvoiceId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuoteId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Quote_CustomerAddress",
                table: "FinancialDocument",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Quote_CustomerContact",
                table: "FinancialDocument",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Quote_CustomerId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quote_ItbisRate",
                table: "FinancialDocument",
                type: "decimal(5,4)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuoteId",
                table: "DocumentLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [Quote_CustomerAddress] = [CustomerAddress],
                    [Quote_CustomerContact] = [CustomerContact],
                    [Quote_CustomerId] = [CustomerId],
                    [Quote_ItbisRate] = [ItbisRate]
                WHERE [DocumentType] = 'Invoice'
                  AND [NcfNumber] IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [FinancialDocument]
                SET [DocumentType] = 'Quote'
                WHERE [DocumentType] = 'Invoice'
                  AND [NcfNumber] IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE [line]
                SET [line].[QuoteId] = [line].[InvoiceId],
                    [line].[InvoiceId] = NULL
                FROM [DocumentLines] AS [line]
                INNER JOIN [FinancialDocument] AS [doc]
                    ON [doc].[Id] = [line].[InvoiceId]
                WHERE [doc].[DocumentType] = 'Quote';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_ConvertedInvoiceId",
                table: "FinancialDocument",
                column: "ConvertedInvoiceId",
                unique: true,
                filter: "[ConvertedInvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_Quote_CustomerId",
                table: "FinancialDocument",
                column: "Quote_CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_QuoteId",
                table: "FinancialDocument",
                column: "QuoteId",
                unique: true,
                filter: "[QuoteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_QuoteId",
                table: "DocumentLines",
                column: "QuoteId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentLines_FinancialDocument_QuoteId",
                table: "DocumentLines",
                column: "QuoteId",
                principalTable: "FinancialDocument",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialDocument_Customers_Quote_CustomerId",
                table: "FinancialDocument",
                column: "Quote_CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DocumentLines_FinancialDocument_QuoteId",
                table: "DocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancialDocument_Customers_Quote_CustomerId",
                table: "FinancialDocument");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_ConvertedInvoiceId",
                table: "FinancialDocument");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_Quote_CustomerId",
                table: "FinancialDocument");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_QuoteId",
                table: "FinancialDocument");

            migrationBuilder.DropIndex(
                name: "IX_DocumentLines_QuoteId",
                table: "DocumentLines");

            migrationBuilder.DropColumn(
                name: "ConvertedAt",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "ConvertedInvoiceId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "QuoteId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Quote_CustomerAddress",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Quote_CustomerContact",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Quote_CustomerId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Quote_ItbisRate",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "QuoteId",
                table: "DocumentLines");
        }
    }
}
