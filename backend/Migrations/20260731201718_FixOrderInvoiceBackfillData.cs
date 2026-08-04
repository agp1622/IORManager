using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class FixOrderInvoiceBackfillData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The AddOrderInvoiceLink backfill created a PurchaseOrder for every pre-existing
            // Invoice, but hard-coded TotalAmount to 0 and never cloned the invoice's lines onto
            // the new order. Fix both here: clone the invoice's DocumentLines onto the order and
            // copy over the invoice's TotalAmount.
            migrationBuilder.Sql("""
                INSERT INTO [DocumentLines] ([Description], [Quantity], [UnitPrice], [UnitOfMeasure], [PurchaseOrderId])
                SELECT dl.[Description], dl.[Quantity], dl.[UnitPrice], dl.[UnitOfMeasure], ord.[Id]
                FROM [FinancialDocument] AS ord
                INNER JOIN [DocumentLines] AS dl ON dl.[InvoiceId] = ord.[Order_ConvertedInvoiceId]
                WHERE ord.[DocumentType] = 'PurchaseOrder'
                  AND ord.[Order_ConvertedInvoiceId] IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [DocumentLines] WHERE [PurchaseOrderId] = ord.[Id]);

                UPDATE ord
                SET ord.[TotalAmount] = inv.[TotalAmount]
                FROM [FinancialDocument] AS ord
                INNER JOIN [FinancialDocument] AS inv ON inv.[Id] = ord.[Order_ConvertedInvoiceId]
                WHERE ord.[DocumentType] = 'PurchaseOrder'
                  AND ord.[Order_ConvertedInvoiceId] IS NOT NULL
                  AND ord.[TotalAmount] = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE dl
                FROM [DocumentLines] AS dl
                INNER JOIN [FinancialDocument] AS ord ON ord.[Id] = dl.[PurchaseOrderId]
                WHERE ord.[DocumentType] = 'PurchaseOrder' AND ord.[Order_ConvertedInvoiceId] IS NOT NULL;

                UPDATE ord
                SET ord.[TotalAmount] = 0
                FROM [FinancialDocument] AS ord
                WHERE ord.[DocumentType] = 'PurchaseOrder' AND ord.[Order_ConvertedInvoiceId] IS NOT NULL;
                """);
        }
    }
}
