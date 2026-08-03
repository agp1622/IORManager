using System.Data;
using IORManager.Data;
using IORManager.Models;
using Microsoft.EntityFrameworkCore;

namespace IORManager.Services.Dgii;

public class EcfNumberGenerator : IEcfNumberGenerator
{
    private readonly IORManagerContext _context;

    public EcfNumberGenerator(IORManagerContext context)
    {
        _context = context;
    }

    public string GenerateNextNumber(string categoryCode) =>
        Execute(categoryCode, advanceSequence: true)
        ?? throw new InvalidOperationException(
            $"No RFCE range is configured for \"{categoryCode}\". Ask the DGII for a numbering " +
            "range and register it before issuing this document type.");

    public string? PeekNextNumber(string categoryCode) => Execute(categoryCode, advanceSequence: false);

    public void SetRange(
        string categoryCode,
        long rangeStart,
        long rangeEnd,
        DateOnly? authorizedAt,
        DateOnly? expiresAt)
    {
        if (rangeStart < 1 || rangeEnd < rangeStart)
        {
            throw new ArgumentException("Invalid RFCE range: start must be >= 1 and end must be >= start.");
        }

        var documentTypeCode = ToDocumentTypeCode(categoryCode);
        var strategy = _context.Database.CreateExecutionStrategy();
        strategy.Execute(() =>
        {
            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);

            var range = _context.EcfRanges.SingleOrDefault(existing => existing.DocumentTypeCode == documentTypeCode);
            if (range is null)
            {
                range = new EcfRange { DocumentTypeCode = documentTypeCode };
                _context.EcfRanges.Add(range);
            }

            range.RangeStart = rangeStart;
            range.RangeEnd = rangeEnd;
            range.NextNumber = rangeStart;
            range.AuthorizedAt = authorizedAt;
            range.ExpiresAt = expiresAt;
            _context.SaveChanges();

            transaction.Commit();
        });
    }

    private string? Execute(string categoryCode, bool advanceSequence)
    {
        var documentTypeCode = ToDocumentTypeCode(categoryCode);
        var strategy = _context.Database.CreateExecutionStrategy();
        return strategy.Execute(() =>
        {
            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);

            var range = _context.EcfRanges.SingleOrDefault(existing => existing.DocumentTypeCode == documentTypeCode);
            if (range is null)
            {
                transaction.Commit();
                return null;
            }

            if (range.ExpiresAt.HasValue && range.ExpiresAt.Value < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new InvalidOperationException(
                    $"The RFCE range for \"{categoryCode}\" expired on {range.ExpiresAt:yyyy-MM-dd}. " +
                    "Request a new range from the DGII before issuing this document type.");
            }

            var candidate = range.NextNumber;
            string formatted;
            do
            {
                if (candidate > range.RangeEnd)
                {
                    throw new InvalidOperationException(
                        $"The RFCE range for \"{categoryCode}\" ({range.RangeStart}-{range.RangeEnd}) is exhausted. " +
                        "Request a new range from the DGII.");
                }

                formatted = NcfCategoryCatalog.FormatNumber(categoryCode, candidate);
                candidate++;
            }
            while (_context.Invoices.Any(invoice => invoice.NcfNumber == formatted));

            if (advanceSequence)
            {
                range.NextNumber = candidate;
                _context.SaveChanges();
            }

            transaction.Commit();
            return formatted;
        });
    }

    private static string ToDocumentTypeCode(string categoryCode)
    {
        var normalized = NcfCategoryCatalog.NormalizeCategoryCode(categoryCode);
        if (!NcfCategoryCatalog.TryGetByCode(normalized, out var definition) || !definition.IsElectronic)
        {
            throw new ArgumentException($"\"{categoryCode}\" is not an electronic NCF category.", nameof(categoryCode));
        }

        return normalized[1..];
    }
}
