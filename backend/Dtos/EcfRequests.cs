namespace IORManager.Dtos;

/// <summary>Request body for PUT /api/invoices/ecf-ranges/{categoryCode}, recording a DGII-authorized RFCE range.</summary>
public record EcfRangeSetRequest(long RangeStart, long RangeEnd, DateOnly? AuthorizedAt, DateOnly? ExpiresAt);

public record EcfRangeResponse(
    string CategoryCode,
    string DocumentTypeCode,
    long? RangeStart,
    long? RangeEnd,
    long? NextNumber,
    DateOnly? AuthorizedAt,
    DateOnly? ExpiresAt,
    string? NextENcf);

public record EcfSubmissionResponse(
    Guid InvoiceId,
    string ENcf,
    string DocumentTypeCode,
    string Status,
    string? TrackId,
    string? SecurityCode,
    string? ResponseMessage,
    DateTime? SignedAt,
    DateTime? SubmittedAt,
    DateTime? RespondedAt);
