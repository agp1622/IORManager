namespace IORManager.Dtos;

/// <summary>Request body for PUT /api/invoices/ecf-ranges/{categoryCode}, recording a DGII-authorized e-NCF numbering range.</summary>
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
    DateTime? RespondedAt,
    DateTime? ContingencySince,
    bool ContingencyOverdue);

/// <summary>One row of GET /api/invoices/ecf-contingencies — e-CFs stuck waiting to reach the DGII.</summary>
public record EcfContingencyResponse(
    Guid InvoiceId,
    string InvoiceNumber,
    string ENcf,
    DateTime ContingencySince,
    bool Overdue,
    string? ResponseMessage);
