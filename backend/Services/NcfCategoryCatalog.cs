using System.Text.RegularExpressions;

namespace IORManager.Services;

public sealed record NcfCategoryDefinition(
    string Code,
    string Name,
    int SequenceLength,
    bool IsElectronic);

public static class NcfCategoryCatalog
{
    public const string DefaultCategoryCode = "B02";

    private static readonly IReadOnlyList<NcfCategoryDefinition> Definitions =
    [
        new("B01", "Factura de Crédito Fiscal", 8, false),
        new("B02", "Factura de Consumo", 8, false),
        new("B03", "Notas de Débito", 8, false),
        new("B04", "Notas de Crédito", 8, false),
        new("B11", "Comprobante de Compras", 8, false),
        new("B12", "Registro Único de Ingresos", 8, false),
        new("B13", "Comprobante para Gastos Menores", 8, false),
        new("B14", "Comprobante para Regímenes Especiales", 8, false),
        new("B15", "Comprobante Gubernamental", 8, false),
        new("B16", "Comprobante para Exportaciones", 8, false),
        new("B17", "Comprobante para Pagos al Exterior", 8, false),
        new("E31", "e-Crédito Fiscal", 10, true),
        new("E32", "e-Consumo", 10, true),
        new("E33", "e-Notas de Débito", 10, true),
        new("E34", "e-Notas de Crédito", 10, true),
        new("E41", "e-Comprobante de Compras", 10, true),
        new("E43", "e-Comprobante para Gastos Menores", 10, true),
        new("E44", "e-Comprobante para Regímenes Especiales", 10, true),
        new("E45", "e-Comprobante Gubernamental", 10, true),
        new("E46", "e-Comprobante para Exportaciones", 10, true),
        new("E47", "e-Comprobante para Pagos al Exterior", 10, true),
    ];

    private static readonly IReadOnlyDictionary<string, NcfCategoryDefinition> ByCode =
        Definitions.ToDictionary(definition => definition.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex NumericRegex = new("^[0-9]+$", RegexOptions.Compiled);

    public static IReadOnlyList<NcfCategoryDefinition> GetAll() => Definitions;

    public static bool TryGetByCode(string? categoryCode, out NcfCategoryDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            definition = default!;
            return false;
        }

        return ByCode.TryGetValue(categoryCode.Trim().ToUpperInvariant(), out definition!);
    }

    public static string NormalizeCategoryCode(string categoryCode)
    {
        if (!TryGetByCode(categoryCode, out var definition))
        {
            throw new ArgumentException("Invalid NCF category code.", nameof(categoryCode));
        }

        return definition.Code;
    }

    public static bool TryInferCategoryFromNcf(string? ncfNumber, out NcfCategoryDefinition definition)
    {
        definition = default!;
        if (string.IsNullOrWhiteSpace(ncfNumber))
        {
            return false;
        }

        var compact = ToCompactNcfNumber(ncfNumber);
        if (compact.Length < 4)
        {
            return false;
        }

        var code = compact[..3];
        if (!TryGetByCode(code, out var candidate))
        {
            return false;
        }

        var suffix = compact[3..];
        if (!NumericRegex.IsMatch(suffix))
        {
            return false;
        }

        definition = candidate;
        return true;
    }

    public static string? TryNormalizeKnownNcfNumber(string? ncfNumber)
    {
        if (string.IsNullOrWhiteSpace(ncfNumber))
        {
            return null;
        }

        var compact = ToCompactNcfNumber(ncfNumber);
        if (!TryInferCategoryFromNcf(compact, out var definition))
        {
            return null;
        }

        var suffix = compact[3..];
        if (!NumericRegex.IsMatch(suffix))
        {
            return null;
        }

        return $"{definition.Code}{suffix}";
    }

    public static string FormatNumber(string categoryCode, long value)
    {
        var normalizedCategory = NormalizeCategoryCode(categoryCode);
        var definition = ByCode[normalizedCategory];
        return $"{definition.Code}{value.ToString($"D{definition.SequenceLength}")}";
    }

    /// <summary>
    /// Parses the numeric sequence value out of an NCF that belongs to the given category,
    /// tolerating legacy/malformed rows (wrong prefix, non-numeric suffix) by returning false.
    /// </summary>
    public static bool TryParseSequenceValue(string categoryCode, string? ncfNumber, out long value)
    {
        value = 0;
        if (!TryGetByCode(categoryCode, out var definition) || string.IsNullOrWhiteSpace(ncfNumber))
        {
            return false;
        }

        var compact = ToCompactNcfNumber(ncfNumber);
        if (!compact.StartsWith(definition.Code, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = compact[definition.Code.Length..];
        return suffix.Length > 0 && NumericRegex.IsMatch(suffix) && long.TryParse(suffix, out value);
    }

    private static string ToCompactNcfNumber(string value)
    {
        var trimmed = value.Trim().ToUpperInvariant();
        return new string(trimmed.Where(ch => ch != '-' && !char.IsWhiteSpace(ch)).ToArray());
    }
}
