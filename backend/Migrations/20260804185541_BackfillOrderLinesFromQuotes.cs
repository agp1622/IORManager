using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IORManager.Migrations
{
    /// <inheritdoc />
    public partial class BackfillOrderLinesFromQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The new orders flow (5a0d52f) dropped the line-item editor from order creation,
            // so orders linked to a quote were persisted with no lines and TotalAmount 0. Backfill
            // by cloning each such order's linked quote's lines onto it. Only touch orders that are
            // still completely empty (no lines, total 0) so orders that already have expenses
            // recorded against them are left alone.
            migrationBuilder.Sql("""
                INSERT INTO [DocumentLines] ([Description], [Quantity], [UnitPrice], [UnitOfMeasure], [PurchaseOrderId])
                SELECT ql.[Description], ql.[Quantity], ql.[UnitPrice], ql.[UnitOfMeasure], ord.[Id]
                FROM [FinancialDocument] AS ord
                INNER JOIN [DocumentLines] AS ql ON ql.[QuoteId] = ord.[PurchaseOrder_QuoteId]
                WHERE ord.[DocumentType] = 'PurchaseOrder'
                  AND ord.[PurchaseOrder_QuoteId] IS NOT NULL
                  AND ord.[TotalAmount] = 0
                  AND NOT EXISTS (SELECT 1 FROM [DocumentLines] WHERE [PurchaseOrderId] = ord.[Id]);

                UPDATE ord
                SET ord.[TotalAmount] = quoteTotals.[LinesTotal]
                FROM [FinancialDocument] AS ord
                INNER JOIN (
                    SELECT [PurchaseOrderId], SUM([Quantity] * [UnitPrice]) AS [LinesTotal]
                    FROM [DocumentLines]
                    WHERE [PurchaseOrderId] IS NOT NULL
                    GROUP BY [PurchaseOrderId]
                ) AS quoteTotals ON quoteTotals.[PurchaseOrderId] = ord.[Id]
                WHERE ord.[DocumentType] = 'PurchaseOrder'
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
                WHERE ord.[DocumentType] = 'PurchaseOrder'
                  AND ord.[PurchaseOrder_QuoteId] IS NOT NULL
                  AND ord.[Order_ConvertedInvoiceId] IS NULL;

                UPDATE ord
                SET ord.[TotalAmount] = 0
                FROM [FinancialDocument] AS ord
                WHERE ord.[DocumentType] = 'PurchaseOrder'
                  AND ord.[PurchaseOrder_QuoteId] IS NOT NULL
                  AND ord.[Order_ConvertedInvoiceId] IS NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM [DocumentLines] dl2
                      WHERE dl2.[PurchaseOrderId] = ord.[Id]
                  );
                """);
        }
    }
}
