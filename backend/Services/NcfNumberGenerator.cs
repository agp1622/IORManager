using IORManager.Data;
using IORManager.Models;

namespace IORManager.Services;

public class NcfNumberGenerator : INcfNumberGenerator
{
    private static readonly object SequenceLock = new();
    private readonly IORManagerContext _context;

    public NcfNumberGenerator(IORManagerContext context)
    {
        _context = context;
    }

    public string GenerateNextNumber()
    {
        lock (SequenceLock)
        {
            var sequence = GetOrCreateSequence();
            return GetNextFormattedNumber(sequence, advanceSequence: true);
        }
    }

    public string PeekNextNumber()
    {
        lock (SequenceLock)
        {
            var sequence = GetOrCreateSequence();
            return GetNextFormattedNumber(sequence, advanceSequence: false);
        }
    }

    private NcfSequence GetOrCreateSequence()
    {
        var sequence = _context.NcfSequences.SingleOrDefault();
        if (sequence is null)
        {
            sequence = new NcfSequence
            {
                Id = 1,
                NextNumber = 1
            };
            _context.NcfSequences.Add(sequence);
            _context.SaveChanges();
        }

        return sequence;
    }

    private string GetNextFormattedNumber(NcfSequence sequence, bool advanceSequence)
    {
        var candidate = sequence.NextNumber;
        string formatted;

        do
        {
            formatted = FormatNcfNumber(candidate);
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

    private static string FormatNcfNumber(int value) => $"NCF-{value:00000000}";
}
