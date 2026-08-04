using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderInvoiceLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Order_ConvertedAt",
                table: "FinancialDocument",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Order_ConvertedInvoiceId",
                table: "FinancialDocument",
                type: "uniqueidentifier",
                nullable: true);

            // Backfill: every pre-existing Invoice gets a matching Order, already marked
            // "Completada" (the invoice already exists, so the order it would have come from
            // is logically already fulfilled). Creation order among the backfilled orders
            // doesn't matter.
            migrationBuilder.Sql("""
                DECLARE @Map TABLE (InvoiceId UNIQUEIDENTIFIER PRIMARY KEY, OrderId UNIQUEIDENTIFIER);

                INSERT INTO @Map (InvoiceId, OrderId)
                SELECT [Id], NEWID()
                FROM [FinancialDocument]
                WHERE [DocumentType] = 'Invoice' AND [DeletedAt] IS NULL;

                INSERT INTO [FinancialDocument]
                    ([Id], [Number], [Date], [CurrencyCode], [CultureName], [PartyName], [TotalAmount], [DeletedAt], [DocumentType],
                     [PurchaseOrder_QuoteId], [PurchaseOrder_Status], [Order_ConvertedInvoiceId], [Order_ConvertedAt])
                SELECT
                    m.[OrderId],
                    'ORD-' + inv.[Number],
                    inv.[Date],
                    inv.[CurrencyCode],
                    inv.[CultureName],
                    inv.[PartyName],
                    inv.[TotalAmount],
                    NULL,
                    'PurchaseOrder',
                    inv.[QuoteId],
                    'Completada',
                    inv.[Id],
                    COALESCE(inv.[InvoiceGeneratedAt], inv.[Date])
                FROM [FinancialDocument] AS inv
                INNER JOIN @Map AS m ON m.[InvoiceId] = inv.[Id];

                UPDATE inv
                SET inv.[OrderId] = m.[OrderId]
                FROM [FinancialDocument] AS inv
                INNER JOIN @Map AS m ON m.[InvoiceId] = inv.[Id];

                -- Clone the invoice's lines onto the backfilled order so it isn't left with no items.
                INSERT INTO [DocumentLines] ([Description], [Quantity], [UnitPrice], [UnitOfMeasure], [PurchaseOrderId])
                SELECT dl.[Description], dl.[Quantity], dl.[UnitPrice], dl.[UnitOfMeasure], m.[OrderId]
                FROM [DocumentLines] AS dl
                INNER JOIN @Map AS m ON m.[InvoiceId] = dl.[InvoiceId];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_Order_ConvertedInvoiceId",
                table: "FinancialDocument",
                column: "Order_ConvertedInvoiceId",
                unique: true,
                filter: "[Order_ConvertedInvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialDocument_OrderId",
                table: "FinancialDocument",
                column: "OrderId",
                unique: true,
                filter: "[OrderId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM [FinancialDocument]
                WHERE [DocumentType] = 'PurchaseOrder' AND [Order_ConvertedInvoiceId] IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_Order_ConvertedInvoiceId",
                table: "FinancialDocument");

            migrationBuilder.DropIndex(
                name: "IX_FinancialDocument_OrderId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Order_ConvertedAt",
                table: "FinancialDocument");

            migrationBuilder.DropColumn(
                name: "Order_ConvertedInvoiceId",
                table: "FinancialDocument");
        }
    }
}
