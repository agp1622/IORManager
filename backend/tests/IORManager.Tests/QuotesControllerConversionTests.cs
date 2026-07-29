using IORManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Tests;

public class QuotesControllerConversionTests
{
    [Fact]
    public void UndoQuoteConversion_RemovesInvoice_AndClearsQuoteAndOrderConversionState()
    {
        using var scope = new TestScope();
        var quote = TestSupport.CreateQuote(number: "QUO-000333");
        scope.Context.Quotes.Add(quote);

        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            Number = "PO-000333",
            Date = new DateOnly(2026, 2, 10),
            CurrencyCode = "DOP",
            CultureName = "es-DO",
            SupplierName = "Internal",
            QuoteId = quote.Id,
            Status = "Completada",
        };
        scope.Context.PurchaseOrders.Add(order);

        var invoice = TestSupport.CreateInvoice(
            number: "INV-000333",
            customerName: quote.CustomerName,
            customerAddress: quote.CustomerAddress,
            customerContact: quote.CustomerContact,
            ncfNumber: "B0200001333",
            ncfCategory: "B02");
        invoice.QuoteId = quote.Id;
        invoice.OrderId = order.Id;
        invoice.InvoiceGeneratedAt = DateTime.UtcNow;
        invoice.Lines.Add(new DocumentLine
        {
            Description = "Converted line",
            Quantity = 1,
            UnitPrice = 100m,
            UnitOfMeasure = "unit",
        });
        invoice.RecalculateTotal();
        scope.Context.Invoices.Add(invoice);
        scope.Context.SaveChanges();

        quote.ConvertedInvoiceId = invoice.Id;
        quote.ConvertedAt = DateTime.UtcNow;
        order.ConvertedInvoiceId = invoice.Id;
        order.ConvertedAt = DateTime.UtcNow;
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateQuotesController(scope.Context);
        var action = controller.UndoQuoteConversion(quote.Id);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<Quote>(ok.Value);
        Assert.Null(payload.ConvertedInvoiceId);
        Assert.Null(payload.ConvertedAt);

        var refreshedQuote = scope.Context.Quotes.Single(existing => existing.Id == quote.Id);
        Assert.Null(refreshedQuote.ConvertedInvoiceId);
        Assert.Null(refreshedQuote.ConvertedAt);

        var refreshedOrder = scope.Context.PurchaseOrders.Single(existing => existing.Id == order.Id);
        Assert.Null(refreshedOrder.ConvertedInvoiceId);
        Assert.Null(refreshedOrder.ConvertedAt);

        Assert.Empty(scope.Context.Invoices.Where(existing => existing.Id == invoice.Id));
        Assert.Empty(scope.Context.DocumentLines.Where(existing => existing.InvoiceId == invoice.Id));
    }

    [Fact]
    public void UndoQuoteConversion_ReturnsNotFound_WhenQuoteDoesNotExist()
    {
        using var scope = new TestScope();
        var controller = TestSupport.CreateQuotesController(scope.Context);

        var action = controller.UndoQuoteConversion(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(action.Result);
    }

    private sealed class TestScope : IDisposable
    {
        public TestScope()
        {
            Context = TestSupport.CreateContext();
        }

        public IORManager.Data.IORManagerContext Context { get; }

        public void Dispose()
        {
            Context.Dispose();
        }
    }
}
