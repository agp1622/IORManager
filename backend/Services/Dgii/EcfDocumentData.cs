namespace IORManager.Services.Dgii;

/// <summary>
/// Everything the <see cref="IEcfXmlBuilder"/> needs to build one e-CF, decoupled from the EF models
/// so the builder can be unit-tested without a database.
/// </summary>
public sealed record EcfDocumentData(
    string DocumentTypeCode,
    string ENcf,
    DateOnly? FechaVencimientoSecuencia,
    EcfEmisorData Emisor,
    EcfCompradorData Comprador,
    DateOnly FechaEmision,
    decimal ItbisRatePercent,
    IReadOnlyList<EcfLineItemData> Lines,
    EcfReferenciaData? Referencia = null);

public sealed record EcfEmisorData(
    string Rnc,
    string RazonSocial,
    string? NombreComercial,
    string? Direccion);

public sealed record EcfCompradorData(
    string? Rnc,
    string RazonSocial);

public sealed record EcfLineItemData(
    int NumeroLinea,
    string NombreItem,
    decimal Cantidad,
    decimal PrecioUnitario);

/// <summary>
/// Reference to the e-CF being modified, required on e-Notas de Débito (E33) and e-Notas de Crédito
/// (E34). <paramref name="CodigoModificacion"/>: 1 = anula el NCF modificado, 2 = corrige texto,
/// 3 = corrige montos (per DGII's published validation rules for notes).
/// </summary>
public sealed record EcfReferenciaData(string NcfModificado, int CodigoModificacion);
