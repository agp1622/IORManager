namespace IORManager.Dtos;

public record NcfAssignmentRequest(string? NcfNumber, string? NcfCategory, bool SkipNcf = false);

public record NcfAssignmentResponse(string NcfNumber, string NcfCategory);

public record NcfCategoryResponse(string Code, string Name, int SequenceLength, bool IsElectronic);

public record QuoteConversionResponse(Guid InvoiceId, string InvoiceNumber, string NcfNumber, string NcfCategory);
