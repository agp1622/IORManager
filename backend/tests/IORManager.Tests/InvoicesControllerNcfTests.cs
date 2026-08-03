using IORManager.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Tests;

public class InvoicesControllerNcfTests
{
    [Fact]
    public void GetNextNcf_ReturnsRequestedCategory()
    {
        using var scope = new TestScope();
        var controller = TestSupport.CreateInvoicesController(scope.Context);

        var action = controller.GetNextNcf("B11");

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<NcfAssignmentResponse>(ok.Value);
        Assert.Equal("B11", payload.NcfCategory);
        Assert.StartsWith("B11", payload.NcfNumber);
    }

    [Fact]
    public void GetNextNcf_ReturnsBadRequest_ForElectronicCategoryWithoutRfceRange()
    {
        using var scope = new TestScope();
        var controller = TestSupport.CreateInvoicesController(scope.Context);

        var action = controller.GetNextNcf("E31");

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("No RFCE range configured", problem.Title);
    }

    [Fact]
    public void GetNextNcf_ReturnsElectronicNcf_OnceRfceRangeIsConfigured()
    {
        using var scope = new TestScope();
        var controller = TestSupport.CreateInvoicesController(scope.Context);
        controller.SetEcfRange("E31", new EcfRangeSetRequest(1, 100, null, null));

        var action = controller.GetNextNcf("E31");

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<NcfAssignmentResponse>(ok.Value);
        Assert.Equal("E31", payload.NcfCategory);
        Assert.Equal("E310000000001", payload.NcfNumber);
    }

    [Fact]
    public void AssignNcf_ReturnsBadRequest_WhenCategoryIsInvalid()
    {
        using var scope = new TestScope();
        var invoice = TestSupport.CreateInvoice(number: "QUO-001");
        scope.Context.Invoices.Add(invoice);
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateInvoicesController(scope.Context);
        var action = controller.AssignNcf(invoice.Id, new NcfAssignmentRequest(null, "Z99"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Invalid NCF category", problem.Title);
    }

    [Fact]
    public void AssignNcf_ReturnsBadRequest_WhenCategoryDoesNotMatchNcf()
    {
        using var scope = new TestScope();
        var invoice = TestSupport.CreateInvoice(number: "QUO-001");
        scope.Context.Invoices.Add(invoice);
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateInvoicesController(scope.Context);
        var action = controller.AssignNcf(invoice.Id, new NcfAssignmentRequest("B0200000001", "B11"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("NCF category mismatch", problem.Title);
    }

    [Fact]
    public void AssignNcf_ReturnsConflict_WhenNcfIsDuplicate()
    {
        using var scope = new TestScope();
        var existing = TestSupport.CreateInvoice(
            number: "QUO-001",
            customerName: "Existing",
            ncfNumber: "B0200000001",
            ncfCategory: "B02");
        var target = TestSupport.CreateInvoice(number: "QUO-002", customerName: "Target");
        scope.Context.Invoices.AddRange(existing, target);
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateInvoicesController(scope.Context);
        var action = controller.AssignNcf(target.Id, new NcfAssignmentRequest("B0200000001", "B02"));

        var conflict = Assert.IsType<ConflictObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal("Duplicate NCF", problem.Title);
    }

    [Fact]
    public void AssignNcf_GeneratesFromRequestedCategory_WhenNcfIsMissing()
    {
        using var scope = new TestScope();
        var invoice = TestSupport.CreateInvoice(number: "QUO-001");
        scope.Context.Invoices.Add(invoice);
        scope.Context.SaveChanges();

        var controller = TestSupport.CreateInvoicesController(scope.Context);
        var action = controller.AssignNcf(invoice.Id, new NcfAssignmentRequest(null, "B11"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var payload = Assert.IsType<NcfAssignmentResponse>(ok.Value);
        Assert.Equal("B11", payload.NcfCategory);
        Assert.StartsWith("B11", payload.NcfNumber);

        var savedInvoice = scope.Context.Invoices.Single(existing => existing.Id == invoice.Id);
        Assert.Equal(payload.NcfNumber, savedInvoice.NcfNumber);
        Assert.Equal("B11", savedInvoice.NcfCategory);
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
