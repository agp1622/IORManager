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
            var sequence = _context.NcfSequences.SingleOrDefault();
            if (sequence is null)
            {
                sequence = new NcfSequence
                {
                    Id = 1,
                    NextNumber = 1
                };
                _context.NcfSequences.Add(sequence);
            }

            var formatted = FormatNcfNumber(sequence.NextNumber);
            sequence.NextNumber++;
            _context.SaveChanges();
            return formatted;
        }
    }

    private static string FormatNcfNumber(int value) => $"NCF-{value:00000000}";
}
