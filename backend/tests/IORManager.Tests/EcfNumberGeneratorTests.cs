using IORManager.Services.Dgii;

namespace IORManager.Tests;

public class EcfNumberGeneratorTests
{
    [Fact]
    public void GenerateNextNumber_ThrowsWhenNoRangeConfigured()
    {
        using var scope = new TestScope();
        var generator = new EcfNumberGenerator(scope.Context);

        Assert.Throws<InvalidOperationException>(() => generator.GenerateNextNumber("E31"));
    }

    [Fact]
    public void PeekNextNumber_ReturnsNullWhenNoRangeConfigured()
    {
        using var scope = new TestScope();
        var generator = new EcfNumberGenerator(scope.Context);

        Assert.Null(generator.PeekNextNumber("E31"));
    }

    [Fact]
    public void GenerateNextNumber_UsesConfiguredRangeAndAdvancesSequence()
    {
        using var scope = new TestScope();
        var generator = new EcfNumberGenerator(scope.Context);
        generator.SetRange("E31", rangeStart: 1, rangeEnd: 100, authorizedAt: null, expiresAt: null);

        var first = generator.GenerateNextNumber("E31");
        var second = generator.GenerateNextNumber("E31");

        Assert.Equal("E310000000001", first);
        Assert.Equal("E310000000002", second);
    }

    [Fact]
    public void GenerateNextNumber_ThrowsWhenRangeExhausted()
    {
        using var scope = new TestScope();
        var generator = new EcfNumberGenerator(scope.Context);
        generator.SetRange("E32", rangeStart: 1, rangeEnd: 1, authorizedAt: null, expiresAt: null);

        generator.GenerateNextNumber("E32");

        Assert.Throws<InvalidOperationException>(() => generator.GenerateNextNumber("E32"));
    }

    [Fact]
    public void GenerateNextNumber_ThrowsWhenRangeExpired()
    {
        using var scope = new TestScope();
        var generator = new EcfNumberGenerator(scope.Context);
        generator.SetRange(
            "E33",
            rangeStart: 1,
            rangeEnd: 100,
            authorizedAt: new DateOnly(2020, 1, 1),
            expiresAt: new DateOnly(2020, 12, 31));

        Assert.Throws<InvalidOperationException>(() => generator.GenerateNextNumber("E33"));
    }

    [Fact]
    public void GenerateNextNumber_SkipsAlreadyAssignedNumbers()
    {
        using var scope = new TestScope();
        scope.Context.Invoices.Add(TestSupport.CreateInvoice(
            number: "QUO-001",
            ncfNumber: "E310000000001",
            ncfCategory: "E31"));
        scope.Context.SaveChanges();

        var generator = new EcfNumberGenerator(scope.Context);
        generator.SetRange("E31", rangeStart: 1, rangeEnd: 100, authorizedAt: null, expiresAt: null);

        var generated = generator.GenerateNextNumber("E31");

        Assert.Equal("E310000000002", generated);
    }

    [Fact]
    public void GenerateNextNumber_RejectsTraditionalCategory()
    {
        using var scope = new TestScope();
        var generator = new EcfNumberGenerator(scope.Context);

        Assert.Throws<ArgumentException>(() => generator.GenerateNextNumber("B01"));
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
