using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountPayableInvoiceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_InvoiceId",
                table: "FinancialDocument",
                column: "InvoiceId",
                unique: true,
                filter: "[InvoiceId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialDocument_FinancialDocument_InvoiceId",
                table: "FinancialDocument",
                column: "InvoiceId",
                principalTable: "FinancialDocument",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialDocument_FinancialDocument_InvoiceId",
                table: "FinancialDocument");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_InvoiceId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "FinancialDocument");
        }
    }
}
