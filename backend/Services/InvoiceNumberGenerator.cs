using System.Linq;
using IORManager.Data;
using IORManager.Models;

namespace IORManager.Services;

public class InvoiceNumberGenerator : IInvoiceNumberGenerator
{
    private static readonly object SequenceLock = new();
    private readonly IORManagerContext _context;

    public InvoiceNumberGenerator(IORManagerContext context)
    {
        _context = context;
    }

    public string GenerateNextNumber()
    {
        lock (SequenceLock)
        {
            var sequence = _context.InvoiceNumberSequences.SingleOrDefault();
            if (sequence is null)
            {
                sequence = new InvoiceNumberSequence
                {
                    Id = 1,
                    NextNumber = 1
                };
                _context.InvoiceNumberSequences.Add(sequence);
            }

            var formattedValue = FormatInvoiceNumber(sequence.NextNumber);
            sequence.NextNumber++;
            _context.SaveChanges();
            return formattedValue;
        }
    }

    private static string FormatInvoiceNumber(int sequenceNumber)
        => $"INV-{sequenceNumber:000000}";
}
