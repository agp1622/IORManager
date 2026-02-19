using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomersAndQuoteReuseSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Contact = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_CustomerId",
                table: "FinancialDocument",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                table: "Customers",
                column: "Name",
                unique: true);

            migrationBuilder.Sql("""
                WITH RankedCustomers AS (
                    SELECT
                        LTRIM(RTRIM([fd].[PartyName])) AS [NormalizedName],
                        LTRIM(RTRIM([fd].[CustomerAddress])) AS [NormalizedAddress],
                        LTRIM(RTRIM([fd].[CustomerContact])) AS [NormalizedContact],
                        ROW_NUMBER() OVER (
                            PARTITION BY LTRIM(RTRIM([fd].[PartyName]))
                            ORDER BY [fd].[Date] DESC, [fd].[Id] DESC
                        ) AS [RowNumber]
                    FROM [FinancialDocument] AS [fd]
                    WHERE [fd].[DocumentType] = 'Invoice'
                      AND [fd].[PartyName] IS NOT NULL
                      AND LTRIM(RTRIM([fd].[PartyName])) <> ''
                      AND [fd].[CustomerAddress] IS NOT NULL
                      AND LTRIM(RTRIM([fd].[CustomerAddress])) <> ''
                      AND [fd].[CustomerContact] IS NOT NULL
                      AND LTRIM(RTRIM([fd].[CustomerContact])) <> ''
                )
                INSERT INTO [Customers] ([Id], [Name], [Address], [Contact], [CreatedAt], [UpdatedAt])
                SELECT
                    NEWID(),
                    [RankedCustomers].[NormalizedName],
                    [RankedCustomers].[NormalizedAddress],
                    [RankedCustomers].[NormalizedContact],
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM [RankedCustomers]
                WHERE [RankedCustomers].[RowNumber] = 1;
                """);

            migrationBuilder.Sql("""
                UPDATE [invoice]
                SET [CustomerId] = [customer].[Id]
                FROM [FinancialDocument] AS [invoice]
                INNER JOIN [Customers] AS [customer]
                    ON [customer].[Name] = LTRIM(RTRIM([invoice].[PartyName]))
                WHERE [invoice].[DocumentType] = 'Invoice';
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialDocument_Customers_CustomerId",
                table: "FinancialDocument",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialDocument_Customers_CustomerId",
                table: "FinancialDocument");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_CustomerId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "FinancialDocument");
        }
    }
}
