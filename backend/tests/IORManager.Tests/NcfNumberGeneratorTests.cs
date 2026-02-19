using IORManager.Services;

namespace IORManager.Tests;

public class NcfNumberGeneratorTests
{
    [Fact]
    public void GenerateNextNumber_UsesIndependentSequencesPerCategory()
    {
        using var scope = new TestScope();
        var generator = new NcfNumberGenerator(scope.Context);

        var firstFiscal = generator.GenerateNextNumber("B01");
        var firstConsumer = generator.GenerateNextNumber("B02");
        var secondFiscal = generator.GenerateNextNumber("B01");
        var nextConsumer = generator.PeekNextNumber("B02");

        Assert.Equal("B0100000001", firstFiscal);
        Assert.Equal("B0200000001", firstConsumer);
        Assert.Equal("B0100000002", secondFiscal);
        Assert.Equal("B0200000002", nextConsumer);
    }

    [Fact]
    public void GenerateNextNumber_SkipsAlreadyAssignedNumbers()
    {
        using var scope = new TestScope();
        scope.Context.Invoices.Add(TestSupport.CreateInvoice(
            number: "QUO-001",
            ncfNumber: "B0100000001",
            ncfCategory: "B01"));
        scope.Context.SaveChanges();

        var generator = new NcfNumberGenerator(scope.Context);
        var generated = generator.GenerateNextNumber("B01");

        Assert.Equal("B0100000002", generated);
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
