using IORManager.Dtos;
using IORManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Tests;

public class QuotesControllerConversionTests
{
    [Fact]
    public void ConvertQuoteToInvoice_CreatesInvoice_AndMarksQuoteAsConverted()
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
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateQuotesController(scope.Context);
        var action = controller.ConvertQuoteToInvoice(
            quote.Id,
            new NcfAssignmentRequest("B0200000999", "B02"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<QuoteConversionResponse>(ok.Value);
        Assert.NotEqual(Guid.Empty, payload.InvoiceId);
        Assert.Equal("B0200000999", payload.NcfNumber);

        var savedQuote = scope.Context.Quotes.Single(existing => existing.Id == quote.Id);
        Assert.Equal(payload.InvoiceId, savedQuote.ConvertedInvoiceId);
        Assert.NotNull(savedQuote.ConvertedAt);

        var invoice = scope.Context.Invoices
            .Single(existing => existing.Id == payload.InvoiceId);
        Assert.Equal(quote.Id, invoice.QuoteId);
        Assert.StartsWith("INV-", invoice.Number);
        Assert.NotEqual("INV-000111", invoice.Number);
        Assert.Equal("B0200000999", invoice.NcfNumber);
        Assert.Equal("B02", invoice.NcfCategory);
        Assert.Single(invoice.Lines);

        var invoicesController = TestSupport.CreateInvoicesController(scope.Context);
        var invoiceAction = invoicesController.GetInvoice(invoice.Id);
        var invoiceOk = Assert.IsType<OkObjectResult>(invoiceAction.Result);
        var responseInvoice = Assert.IsType<Invoice>(invoiceOk.Value);
        Assert.Equal("QUO-000111", responseInvoice.QuoteNumber);
    }

    [Fact]
    public void ConvertQuoteToInvoice_ReturnsExistingInvoice_WhenAlreadyConverted()
    {
        using var scope = new TestScope();
        var quote = TestSupport.CreateQuote(number: "QUO-000222");
        scope.Context.Quotes.Add(quote);

        var invoice = TestSupport.CreateInvoice(
            number: "INV-000222",
            customerName: quote.CustomerName,
            customerAddress: quote.CustomerAddress,
            customerContact: quote.CustomerContact,
            ncfNumber: "B0200001222",
            ncfCategory: "B02");
        invoice.QuoteId = quote.Id;
        invoice.InvoiceGeneratedAt = DateTime.UtcNow;
        scope.Context.Invoices.Add(invoice);
        scope.Context.SaveChanges();

        quote.ConvertedInvoiceId = invoice.Id;
        quote.ConvertedAt = DateTime.UtcNow;
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateQuotesController(scope.Context);
        var action = controller.ConvertQuoteToInvoice(
            quote.Id,
            new NcfAssignmentRequest("B0200003333", "B02"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<QuoteConversionResponse>(ok.Value);
        Assert.Equal(invoice.Id, payload.InvoiceId);
        Assert.Equal("B0200001222", payload.NcfNumber);
        Assert.Equal("B02", payload.NcfCategory);
        Assert.Equal(1, scope.Context.Invoices.Count());
    }

    [Fact]
    public void UndoQuoteConversion_RemovesInvoice_AndClearsQuoteConversionState()
    {
        using var scope = new TestScope();
        var quote = TestSupport.CreateQuote(number: "QUO-000333");
        scope.Context.Quotes.Add(quote);

        var invoice = TestSupport.CreateInvoice(
            number: "INV-000333",
            customerName: quote.CustomerName,
            customerAddress: quote.CustomerAddress,
            customerContact: quote.CustomerContact,
            ncfNumber: "B0200001333",
            ncfCategory: "B02");
        invoice.QuoteId = quote.Id;
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
