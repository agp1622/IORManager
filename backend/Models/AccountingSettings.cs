namespace IORManager.Models;

/// <summary>Single-row settings table for the accounting/tax summary report. Always Id = 1.</summary>
public class AccountingSettings
{
    public int Id { get; set; }

    /// <summary>Flat ISR rate applied to net income when it's positive, e.g. 27 for 27%.</summary>
    public decimal IsrRatePercent { get; set; }
}
