namespace IORManager.Services;

public static class DocumentNumberFormatter
{
    public static string ToInvoiceNumber(string? sourceNumber)
    {
        if (string.IsNullOrWhiteSpace(sourceNumber))
        {
            return "INV-000000";
        }

        var trimmed = sourceNumber.Trim();

        if (trimmed.StartsWith("INV-", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.StartsWith("QUO-", StringComparison.OrdinalIgnoreCase))
        {
            return "INV-" + trimmed[4..];
        }

        return trimmed.StartsWith("INV", StringComparison.OrdinalIgnoreCase)
            ? $"INV-{trimmed[3..]}"
            : $"INV-{trimmed}";
    }
}
