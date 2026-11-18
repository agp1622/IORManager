namespace IORManager.Services.Dgii;

public interface IEcfNumberGenerator
{
    /// <summary>
    /// Generates and reserves the next e-NCF for the given electronic NCF category (e.g. "E31").
    /// Throws <see cref="InvalidOperationException"/> if no e-NCF numbering range is configured for that
    /// document type, the range is exhausted, or its DGII authorization has expired.
    /// </summary>
    string GenerateNextNumber(string categoryCode);

    /// <summary>Same as <see cref="GenerateNextNumber"/> but without advancing the sequence.</summary>
    string? PeekNextNumber(string categoryCode);

    /// <summary>
    /// Records a DGII-authorized e-NCF numbering range for a document type. Call this once the DGII grants (or
    /// renews) a numbering range for an e-CF document type.
    /// </summary>
    void SetRange(string categoryCode, long rangeStart, long rangeEnd, DateOnly? authorizedAt, DateOnly? expiresAt);
}
