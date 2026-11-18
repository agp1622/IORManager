using IORManager.Services.Dgii;

namespace IORManager.Tests;

public class EcfQrCodeGeneratorTests
{
    private static EcfQrCodeData CreateData(string? rncComprador = "101123456") => new(
        RncEmisor: "131586465",
        RncComprador: rncComprador,
        ENcf: "E310000000001",
        FechaEmision: new DateOnly(2026, 8, 3),
        MontoTotal: 2360.5m,
        FechaFirmaUtc: new DateTime(2026, 8, 3, 14, 30, 15, DateTimeKind.Utc),
        CodigoSeguridad: "AB12CD",
        VerificationBaseUrl: "https://ecf.dgii.gov.do/testecf/ConsultaTimbre");

    [Fact]
    public void BuildVerificationUrl_IncludesAllExpectedParameters()
    {
        var generator = new EcfQrCodeGenerator();
        var url = generator.BuildVerificationUrl(CreateData());

        Assert.StartsWith("https://ecf.dgii.gov.do/testecf/ConsultaTimbre?", url);
        Assert.Contains("RncEmisor=131586465", url);
        Assert.Contains("RncComprador=101123456", url);
        Assert.Contains("ENCF=E310000000001", url);
        Assert.Contains("FechaEmision=03-08-2026", url);
        Assert.Contains("MontoTotal=2360.50", url);
        Assert.Contains("FechaFirma=03-08-2026", url);
        Assert.Contains("CodigoSeguridad=AB12CD", url);
    }

    [Fact]
    public void BuildVerificationUrl_OmitsRncCompradorWhenMissing()
    {
        var generator = new EcfQrCodeGenerator();
        var url = generator.BuildVerificationUrl(CreateData(rncComprador: null));

        Assert.DoesNotContain("RncComprador", url);
    }

    [Fact]
    public void GeneratePng_ProducesNonEmptyPng()
    {
        var generator = new EcfQrCodeGenerator();
        var png = generator.GeneratePng(CreateData());

        Assert.NotEmpty(png);
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        Assert.Equal(0x89, png[0]);
        Assert.Equal((byte)'P', png[1]);
        Assert.Equal((byte)'N', png[2]);
        Assert.Equal((byte)'G', png[3]);
    }
}
