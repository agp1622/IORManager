namespace IORManager.Dtos;

public record NcfAssignmentRequest(string? NcfNumber, string? NcfCategory, bool SkipNcf = false);

public record NcfAssignmentResponse(string NcfNumber, string NcfCategory);

public record NcfCategoryResponse(string Code, string Name, int SequenceLength, bool IsElectronic);

public record QuoteConversionResponse(Guid InvoiceId, string InvoiceNumber, string NcfNumber, string NcfCategory);

/// <summary>
/// Request body for PUT /api/invoices/ncf-sequences/{categoryCode}.
/// Sets the next number that will be auto-assigned for that NCF category.
/// </summary>
public record NcfSequenceSetRequest(long NextNumber);

/// <summary>
/// Summary of a fiscal regime returned by GET /api/invoices/fiscal-regimes.
/// Includes the regime's running invoice count, the last NCF used, and the next one that will be assigned.
/// </summary>
public record FiscalRegimeResponse(
    int Id,
    string Code,
    string Name,
    int InvoiceCount,
    string? LastNcfUsed,
    string NextNcf);
