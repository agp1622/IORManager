using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingSettingsAndExpenseDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "OrderExpenses",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Backfill: existing expenses don't have a real "expense date," so use the day they
            // were logged as the best available approximation.
            migrationBuilder.Sql("""
                UPDATE [OrderExpenses]
                SET [Date] = CAST([CreatedAt] AS date);
                """);

            migrationBuilder.CreateTable(
                name: "AccountingSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IsrRatePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AccountingSettings",
                columns: new[] { "Id", "IsrRatePercent" },
                values: new object[] { 1, 0m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingSettings");

            migrationBuilder.DropColumn(
                name: "Date",
                table: "OrderExpenses");
        }
    }
}
