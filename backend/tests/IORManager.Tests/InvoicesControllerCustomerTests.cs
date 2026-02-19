using IORManager.Dtos;
using IORManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Tests;

public class InvoicesControllerCustomerTests
{
    [Fact]
    public void CreateInvoice_ReturnsBadRequest_WhenAddressOrContactMissing()
    {
        using var scope = new TestScope();
        var controller = TestSupport.CreateInvoicesController(scope.Context);
        var request = BuildCreateRequest(
            customerAddress: "   ",
            customerContact: "809-555-0101");

        var action = controller.CreateInvoice(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Customer details required", problem.Title);
    }

    [Fact]
    public void CreateInvoice_UpsertsCustomer_ByName()
    {
        using var scope = new TestScope();
        var controller = TestSupport.CreateInvoicesController(scope.Context);

        var firstCreate = controller.CreateInvoice(
            BuildCreateRequest(
                customerName: "Acme SRL",
                customerAddress: "Street 1",
                customerContact: "809-100-0001"));

        var firstCreated = Assert.IsType<CreatedAtActionResult>(firstCreate.Result);
        var firstResponse = Assert.IsType<InvoiceCreateResponse>(firstCreated.Value);

        var secondCreate = controller.CreateInvoice(
            BuildCreateRequest(
                customerName: "Acme SRL",
                customerAddress: "Street 99",
                customerContact: "809-100-9999"));

        var secondCreated = Assert.IsType<CreatedAtActionResult>(secondCreate.Result);
        var secondResponse = Assert.IsType<InvoiceCreateResponse>(secondCreated.Value);

        var savedCustomers = scope.Context.Customers.ToList();
        Assert.Single(savedCustomers);

        var customer = savedCustomers[0];
        Assert.Equal("Acme SRL", customer.Name);
        Assert.Equal("Street 99", customer.Address);
        Assert.Equal("809-100-9999", customer.Contact);

        Assert.Equal(customer.Id, firstResponse.Invoice.CustomerId);
        Assert.Equal(customer.Id, secondResponse.Invoice.CustomerId);
        Assert.Equal(customer.Address, secondResponse.Invoice.CustomerAddress);
        Assert.Equal(customer.Contact, secondResponse.Invoice.CustomerContact);
    }

    [Fact]
    public void CreateInvoice_UsesCurrentDate_ForQuoteDate()
    {
        using var scope = new TestScope();
        var controller = TestSupport.CreateInvoicesController(scope.Context);
        var request = BuildCreateRequest();
        var expectedToday = DateOnly.FromDateTime(DateTime.UtcNow);

        request = request with { InvoiceDate = new DateOnly(2024, 1, 1) };
        var action = controller.CreateInvoice(request);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<InvoiceCreateResponse>(created.Value);
        Assert.Equal(expectedToday, response.Invoice.Date);
    }

    [Fact]
    public void Invoice_ComputesQuoteAndInvoiceExpirations()
    {
        var invoice = TestSupport.CreateInvoice(number: "QUO-000001");
        invoice.Date = new DateOnly(2026, 2, 11);

        Assert.Equal(new DateOnly(2026, 3, 11), invoice.QuoteExpirationDate);

        invoice.InvoiceGeneratedAt = new DateTime(2026, 8, 3, 13, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 8, 3), invoice.InvoiceDate);
        Assert.Equal(new DateOnly(2026, 12, 31), invoice.InvoiceExpirationDate);
    }

    [Fact]
    public void DuplicateInvoice_CreatesNewQuoteWithoutNcf()
    {
        using var scope = new TestScope();
        var customerId = Guid.NewGuid();
        scope.Context.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Acme SRL",
            Address = "Street 10",
            Contact = "809-555-0101",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        var source = TestSupport.CreateInvoice(
            number: "QUO-000123",
            customerName: "Acme SRL",
            customerAddress: "Street 10",
            customerContact: "809-555-0101",
            ncfNumber: "B0200000011",
            ncfCategory: "B02");
        source.CustomerId = customerId;
        source.InvoiceGeneratedAt = DateTime.UtcNow;
        source.Lines.Add(new DocumentLine
        {
            Description = "Consulting service",
            Quantity = 2,
            UnitPrice = 50m,
            UnitOfMeasure = "unit",
        });
        source.RecalculateTotal();

        scope.Context.Invoices.Add(source);
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateInvoicesController(scope.Context);
        var action = controller.DuplicateInvoice(source.Id);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var duplicate = Assert.IsType<Invoice>(created.Value);

        Assert.NotEqual(source.Id, duplicate.Id);
        Assert.Equal(source.CustomerId, duplicate.CustomerId);
        Assert.Equal(source.CustomerName, duplicate.CustomerName);
        Assert.Equal(source.CustomerAddress, duplicate.CustomerAddress);
        Assert.Equal(source.CustomerContact, duplicate.CustomerContact);
        Assert.Null(duplicate.NcfNumber);
        Assert.Null(duplicate.NcfCategory);
        Assert.Null(duplicate.InvoiceGeneratedAt);
        Assert.Single(duplicate.Lines);
        Assert.Equal("Consulting service", duplicate.Lines[0].Description);
        Assert.Equal(2, scope.Context.Invoices.Count());
    }

    private static InvoiceCreateRequest BuildCreateRequest(
        string customerName = "Test Customer",
        string customerAddress = "Main St 123",
        string customerContact = "test@example.com")
    {
        return new InvoiceCreateRequest
        {
            InvoiceDate = new DateOnly(2026, 2, 10),
            CustomerName = customerName,
            CustomerAddress = customerAddress,
            CustomerContact = customerContact,
            CurrencyCode = "DOP",
            Locale = "es-DO",
            Lines =
            [
                new InvoiceLineRequest
                {
                    Description = "Service",
                    Quantity = 1,
                    UnitPrice = 100m,
                    UnitOfMeasure = "unit"
                }
            ]
        };
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
