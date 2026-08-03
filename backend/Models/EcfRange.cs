using System.ComponentModel.DataAnnotations;

namespace IORManager.Models;

/// <summary>
/// A DGII-authorized RFCE (Rango de Facturación Comprobante Electrónico) — the block of e-NCF
/// sequence numbers the DGII grants a taxpayer for one electronic document type (e.g. "31" for
/// e-Crédito Fiscal / E31). Unlike traditional NCF sequences, e-CF numbers can only be generated
/// inside a range the DGII has explicitly authorized, and that authorization expires.
/// </summary>
public class EcfRange
{
    public int Id { get; set; }

    /// <summary>The 2-digit e-CF document type code (e.g. "31", "32", "33" ... "47"), without the "E" prefix.</summary>
    [Required]
    [MaxLength(2)]
    public string DocumentTypeCode { get; set; } = string.Empty;

    /// <summary>First sequence number of the authorized range (inclusive).</summary>
    public long RangeStart { get; set; }

    /// <summary>Last sequence number of the authorized range (inclusive).</summary>
    public long RangeEnd { get; set; }

    /// <summary>Next sequence number that will be assigned.</summary>
    public long NextNumber { get; set; }

    /// <summary>Date the DGII authorized this range.</summary>
    public DateOnly? AuthorizedAt { get; set; }

    /// <summary>
    /// Date the authorization expires. Per DGII rules this is normally December 31st of the year
    /// following authorization; numbers cannot be generated once this date has passed even if the
    /// range isn't exhausted.
    /// </summary>
    public DateOnly? ExpiresAt { get; set; }
}
