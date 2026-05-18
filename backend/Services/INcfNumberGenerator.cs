namespace IORManager.Services;

public interface INcfNumberGenerator
{
    string GenerateNextNumber(string categoryCode);
    string PeekNextNumber(string categoryCode);

    /// <summary>
    /// Manually sets the next number that will be used for <paramref name="categoryCode"/>.
    /// Useful for correcting a sequence after importing historical invoices.
    /// </summary>
    void SetNextNumber(string categoryCode, long nextNumber);
}
