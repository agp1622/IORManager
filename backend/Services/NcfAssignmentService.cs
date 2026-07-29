using IORManager.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IORManager.Services;

/// <summary>
/// Shared NCF (Dominican fiscal receipt number) resolution/validation logic used when an
/// Invoice is generated, whether the trigger is a Quote or an Order conversion.
/// </summary>
public class NcfAssignmentService
{
    private readonly IORManagerContext _context;

    public NcfAssignmentService(IORManagerContext context)
    {
        _context = context;
    }

    public bool IsDuplicateNcf(string normalizedNcf) =>
        _context.Invoices.Any(invoice => invoice.NcfNumber == normalizedNcf);

    public ProblemDetails DuplicateNcfProblem(string normalizedNcf) => new()
    {
        Title = "Duplicate NCF",
        Detail = $"The NCF \"{normalizedNcf}\" is already assigned to another invoice.",
        Status = StatusCodes.Status409Conflict,
    };

    public bool TryResolveNcfData(
        string? ncfNumber,
        string? categoryCode,
        out string? normalizedNcf,
        out string? normalizedCategory,
        out ProblemDetails? error)
    {
        normalizedNcf = NormalizeNcfNumber(ncfNumber);
        if (!TryNormalizeCategoryCode(categoryCode, out normalizedCategory, out error))
        {
            return false;
        }

        if (NcfCategoryCatalog.TryInferCategoryFromNcf(normalizedNcf, out var inferredDefinition))
        {
            if (!string.IsNullOrWhiteSpace(normalizedCategory) &&
                !string.Equals(normalizedCategory, inferredDefinition.Code, StringComparison.OrdinalIgnoreCase))
            {
                error = NcfCategoryMismatchProblem(inferredDefinition.Code, normalizedCategory);
                return false;
            }

            normalizedCategory = inferredDefinition.Code;
        }

        error = null;
        return true;
    }

    private bool TryNormalizeCategoryCode(
        string? categoryCode,
        out string? normalizedCategory,
        out ProblemDetails? error)
    {
        normalizedCategory = null;
        if (string.IsNullOrWhiteSpace(categoryCode))
        {
            error = null;
            return true;
        }

        if (!NcfCategoryCatalog.TryGetByCode(categoryCode, out var definition))
        {
            error = new ProblemDetails
            {
                Title = "Invalid NCF category",
                Detail = $"The NCF category \"{categoryCode.Trim()}\" is not supported.",
                Status = StatusCodes.Status400BadRequest,
            };
            return false;
        }

        normalizedCategory = definition.Code;
        error = null;
        return true;
    }

    private static ProblemDetails NcfCategoryMismatchProblem(string inferredCategory, string providedCategory) => new()
    {
        Title = "NCF category mismatch",
        Detail = $"The NCF value belongs to category \"{inferredCategory}\" but \"{providedCategory}\" was requested.",
        Status = StatusCodes.Status400BadRequest,
    };

    private static string? NormalizeNcfNumber(string? ncfNumber)
    {
        if (string.IsNullOrWhiteSpace(ncfNumber))
        {
            return null;
        }

        var trimmed = ncfNumber.Trim().ToUpperInvariant();
        var compact = new string(trimmed.Where(ch => ch != '-' && !char.IsWhiteSpace(ch)).ToArray());
        const string ncfPrefix = "NCF";

        if (compact.Length >= ncfPrefix.Length &&
            compact.StartsWith(ncfPrefix, StringComparison.Ordinal))
        {
            var suffix = compact[ncfPrefix.Length..];
            if (string.IsNullOrWhiteSpace(suffix))
            {
                return null;
            }

            return $"{ncfPrefix}-{suffix}";
        }

        var normalizedKnown = NcfCategoryCatalog.TryNormalizeKnownNcfNumber(compact);
        if (!string.IsNullOrWhiteSpace(normalizedKnown))
        {
            return normalizedKnown;
        }

        return trimmed;
    }
}
