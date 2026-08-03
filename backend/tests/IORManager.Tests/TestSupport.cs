using IORManager.Controllers;
using IORManager.Data;
using IORManager.Models;
using IORManager.Repositories;
using IORManager.Services;
using IORManager.Services.Dgii;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IORManager.Tests;

internal static class TestSupport
{
    public static IORManagerContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IORManagerContext>()
            .UseInMemoryDatabase($"ior-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings =>
                warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new IORManagerContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static Invoice CreateInvoice(
        string number,
        string customerName = "Test Customer",
        string customerAddress = "Main St 123",
        string customerContact = "test@example.com",
        string? ncfNumber = null,
        string? ncfCategory = null)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            Number = number,
            Date = new DateOnly(2026, 2, 10),
            CurrencyCode = "DOP",
            CultureName = "es-DO",
            CustomerName = customerName,
            CustomerAddress = customerAddress,
            CustomerContact = customerContact,
            NcfNumber = ncfNumber,
            NcfCategory = ncfCategory,
            TotalAmount = 100m,
            Lines = []
        };
    }

    public static Quote CreateQuote(
        string number,
        string customerName = "Test Customer",
        string customerAddress = "Main St 123",
        string customerContact = "test@example.com")
    {
        return new Quote
        {
            Id = Guid.NewGuid(),
            Number = number,
            Date = new DateOnly(2026, 2, 10),
            CurrencyCode = "DOP",
            CultureName = "es-DO",
            CustomerName = customerName,
            CustomerAddress = customerAddress,
            CustomerContact = customerContact,
            TotalAmount = 100m,
            Lines = []
        };
    }

    public static InvoicesController CreateInvoicesController(IORManagerContext context)
    {
        var repository = new EfFinancialDocumentRepository<Invoice>(
            context,
            query => query.Include(invoice => invoice.Lines));

        return new InvoicesController(
            repository,
            new StubFinancialDocumentPdfService(),
            new StubInvoiceNumberGenerator(),
            new NcfNumberGenerator(context),
            new EcfNumberGenerator(context),
            new StubEcfService(),
            context);
    }

    public static QuotesController CreateQuotesController(IORManagerContext context)
    {
        var repository = new EfFinancialDocumentRepository<Quote>(
            context,
            query => query.Include(quote => quote.Lines));

        return new QuotesController(
            repository,
            new StubFinancialDocumentPdfService(),
            new StubInvoiceNumberGenerator(),
            new NcfNumberGenerator(context),
            context);
    }

    private sealed class StubInvoiceNumberGenerator : IInvoiceNumberGenerator
    {
        private int _next = 1;

        public string GenerateNextNumber() => $"QUO-{_next++:000000}";
    }

    private sealed class StubEcfService : IEcfService
    {
        public Task<EcfSubmission> EmitAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<EcfSubmission> RefreshStatusAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class StubFinancialDocumentPdfService : IFinancialDocumentPdfService
    {
        public byte[] GenerateQuotePdf(Invoice invoice) => [];

        public byte[] GenerateInvoicePdf(Invoice invoice, string? ncfNumber = null) => [];

        public byte[] GenerateInvoiceWord(Invoice invoice, string? ncfNumber = null) => [];

        public byte[] GenerateReceiptPdf(Receipt receipt) => [];

        public byte[] GeneratePurchaseOrderPdf(PurchaseOrder purchaseOrder) => [];

        public byte[] GenerateInvoicesBatchPdf(IReadOnlyCollection<Invoice> invoices) => [];

        byte[] IFinancialDocumentPdfService.GenerateQuotePdf(Invoice invoice, string? comments)
        {
            throw new NotImplementedException();
        }
    }
}
