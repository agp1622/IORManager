using IORManager.Data;
using IORManager.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace IORManager.Services;

public class NcfNumberGenerator : INcfNumberGenerator
{
    private readonly IORManagerContext _context;

    public NcfNumberGenerator(IORManagerContext context)
    {
        _context = context;
    }

    public string GenerateNextNumber(string categoryCode)
    {
        var normalizedCategory = NcfCategoryCatalog.NormalizeCategoryCode(categoryCode);
        return ExecuteWithSerializableTransaction(normalizedCategory, advanceSequence: true);
    }

    public string PeekNextNumber(string categoryCode)
    {
        var normalizedCategory = NcfCategoryCatalog.NormalizeCategoryCode(categoryCode);
        return ExecuteWithSerializableTransaction(normalizedCategory, advanceSequence: false);
    }

    private string ExecuteWithSerializableTransaction(string categoryCode, bool advanceSequence)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return strategy.Execute(() =>
        {
            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);

            var sequence = GetOrCreateSequence(categoryCode);
            var generated = GetNextFormattedNumber(sequence, categoryCode, advanceSequence);

            transaction.Commit();
            return generated;
        });
    }

    private NcfSequence GetOrCreateSequence(string categoryCode)
    {
        var sequence = _context.NcfSequences
            .SingleOrDefault(existing => existing.CategoryCode == categoryCode);

        if (sequence is null)
        {
            var nextId = _context.NcfSequences
                .Select(existing => (int?)existing.Id)
                .Max() ?? 0;

            sequence = new NcfSequence
            {
                Id = nextId + 1,
                CategoryCode = categoryCode,
                NextNumber = 1
            };
            _context.NcfSequences.Add(sequence);
            _context.SaveChanges();
        }

        return sequence;
    }

    public void SetNextNumber(string categoryCode, long nextNumber)
    {
        if (nextNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextNumber), "Next number must be at least 1.");
        }

        var normalizedCategory = NcfCategoryCatalog.NormalizeCategoryCode(categoryCode);
        var strategy = _context.Database.CreateExecutionStrategy();
        strategy.Execute(() =>
        {
            using var transaction = _context.Database.BeginTransaction(IsolationLevel.Serializable);

            var sequence = GetOrCreateSequence(normalizedCategory);
            sequence.NextNumber = nextNumber;
            _context.SaveChanges();

            transaction.Commit();
        });
    }

    private string GetNextFormattedNumber(
        NcfSequence sequence,
        string categoryCode,
        bool advanceSequence)
    {
        var candidate = sequence.NextNumber;
        string formatted;

        do
        {
            formatted = NcfCategoryCatalog.FormatNumber(categoryCode, candidate);
            candidate++;
        }
        while (_context.Invoices.Any(invoice => invoice.NcfNumber == formatted));

        if (advanceSequence)
        {
            sequence.NextNumber = candidate;
            _context.SaveChanges();
        }

        return formatted;
    }
}
