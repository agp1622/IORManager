using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsPayable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPO",
                table: "FinancialDocument",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "FinancialDocument",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FinancialDocument",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FinancialDocument",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPO",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FinancialDocument");
        }
    }
}
