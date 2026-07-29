using IORManager.Dtos;
using IORManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Tests;

public class PurchaseOrdersControllerConversionTests
{
    [Fact]
    public void ConvertPurchaseOrderToInvoice_CreatesInvoice_AndMarksOrderAndQuoteAsConverted()
    {
        using var scope = new TestScope();
        var quote = TestSupport.CreateQuote(number: "QUO-000111");
        quote.Lines.Add(new DocumentLine
        {
            Description = "Service",
            Quantity = 2,
            UnitPrice = 150m,
            UnitOfMeasure = "unit",
        });
        quote.RecalculateTotal();
        scope.Context.Quotes.Add(quote);

        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            Number = "PO-000111",
            Date = new DateOnly(2026, 2, 10),
            CurrencyCode = "DOP",
            CultureName = "es-DO",
            SupplierName = "Internal",
            QuoteId = quote.Id,
            Status = "Completada",
        };
        scope.Context.PurchaseOrders.Add(order);
        scope.Context.SaveChanges();

        var controller = TestSupport.CreatePurchaseOrdersController(scope.Context);
        var action = controller.ConvertPurchaseOrderToInvoice(
            order.Id,
            new NcfAssignmentRequest("B0200000999", "B02"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<QuoteConversionResponse>(ok.Value);
        Assert.NotEqual(Guid.Empty, payload.InvoiceId);
        Assert.Equal("B0200000999", payload.NcfNumber);

        var savedOrder = scope.Context.PurchaseOrders.Single(existing => existing.Id == order.Id);
        Assert.Equal(payload.InvoiceId, savedOrder.ConvertedInvoiceId);
        Assert.NotNull(savedOrder.ConvertedAt);

        var savedQuote = scope.Context.Quotes.Single(existing => existing.Id == quote.Id);
        Assert.Equal(payload.InvoiceId, savedQuote.ConvertedInvoiceId);

        var invoice = scope.Context.Invoices.Single(existing => existing.Id == payload.InvoiceId);
        Assert.Equal(quote.Id, invoice.QuoteId);
        Assert.Equal(order.Id, invoice.OrderId);
        Assert.StartsWith("INV-", invoice.Number);
        Assert.Equal("B0200000999", invoice.NcfNumber);
        Assert.Equal("B02", invoice.NcfCategory);
        Assert.Single(invoice.Lines);
    }

    [Fact]
    public void ConvertPurchaseOrderToInvoice_ReturnsExistingInvoice_WhenAlreadyConverted()
    {
        using var scope = new TestScope();
        var quote = TestSupport.CreateQuote(number: "QUO-000222");
        scope.Context.Quotes.Add(quote);

        var order = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            Number = "PO-000222",
            Date = new DateOnly(2026, 2, 10),
            CurrencyCode = "DOP",
            CultureName = "es-DO",
            SupplierName = "Internal",
            QuoteId = quote.Id,
            Status = "Completada",
        };
        scope.Context.PurchaseOrders.Add(order);

        var invoice = TestSupport.CreateInvoice(
            number: "INV-000222",
            customerName: quote.CustomerName,
            customerAddress: quote.CustomerAddress,
            customerContact: quote.CustomerContact,
            ncfNumber: "B0200001222",
            ncfCategory: "B02");
        invoice.QuoteId = quote.Id;
        invoice.OrderId = order.Id;
        invoice.InvoiceGeneratedAt = DateTime.UtcNow;
        scope.Context.Invoices.Add(invoice);
        scope.Context.SaveChanges();

        order.ConvertedInvoiceId = invoice.Id;
        order.ConvertedAt = DateTime.UtcNow;
        quote.ConvertedInvoiceId = invoice.Id;
        quote.ConvertedAt = DateTime.UtcNow;
        scope.Context.SaveChanges();

        var controller = TestSupport.CreatePurchaseOrdersController(scope.Context);
        var action = controller.ConvertPurchaseOrderToInvoice(
            order.Id,
            new NcfAssignmentRequest("B0200003333", "B02"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<QuoteConversionResponse>(ok.Value);
        Assert.Equal(invoice.Id, payload.InvoiceId);
        Assert.Equal("B0200001222", payload.NcfNumber);
        Assert.Equal(1, scope.Context.Invoices.Count());
    }

    [Fact]
    public void ConvertPurchaseOrderToInvoice_RejectsOrder_WhenNotCompleted()
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
            Status = "Pendiente",
        };
        scope.Context.PurchaseOrders.Add(order);
        scope.Context.SaveChanges();

        var controller = TestSupport.CreatePurchaseOrdersController(scope.Context);
        var action = controller.ConvertPurchaseOrderToInvoice(order.Id, new NcfAssignmentRequest(null, null, true));

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(scope.Context.Invoices);
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
