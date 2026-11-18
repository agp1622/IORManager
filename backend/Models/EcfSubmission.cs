using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IORManager.Models;

/// <summary>
/// Tracks the e-CF (electronic fiscal receipt) lifecycle for one invoice: the generated XML, its
/// signed version, and the DGII's response as the document moves through submission and validation.
/// One row per invoice — an invoice only ever has one active e-CF submission, since a rejected e-NCF
/// cannot be resubmitted (DGII rules require voiding it and issuing a new e-NCF, which means a new
/// invoice/submission).
/// </summary>
public class EcfSubmission
{
    [Key]
    public Guid Id { get; set; }

    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>The 2-digit e-CF document type code (e.g. "31"), without the "E" prefix.</summary>
    [Required]
    [MaxLength(2)]
    public string DocumentTypeCode { get; set; } = string.Empty;

    /// <summary>The assigned e-NCF (e.g. "E310000000001").</summary>
    [Required]
    [MaxLength(20)]
    public string ENcf { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string? UnsignedXml { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? SignedXml { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = EcfSubmissionStatus.Generado;

    /// <summary>The DGII's tracking id for this submission, returned by the reception endpoint.</summary>
    [MaxLength(100)]
    public string? TrackId { get; set; }

    /// <summary>
    /// First 6 characters of the signature's hash, printed under the QR code on the printed
    /// representation of the e-CF.
    /// </summary>
    [MaxLength(20)]
    public string? SecurityCode { get; set; }

    public DateTime? SignedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Set the first time a submission attempt fails to reach the DGII (network/DGII outage). Marks
    /// the start of the 72-hour contingency window the DGII allows for sending an e-CF that was
    /// generated and signed offline. Cleared once the e-CF is successfully transmitted.
    /// </summary>
    public DateTime? ContingencySince { get; set; }

    /// <summary>Latest status message / validation errors reported by the DGII.</summary>
    [MaxLength(4000)]
    public string? ResponseMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lifecycle states for an <see cref="EcfSubmission"/>. Mirrors the states the DGII reports back
/// (Aceptado / Rechazado / Aceptado Condicional) plus local pre-submission states.
/// </summary>
public static class EcfSubmissionStatus
{
    /// <summary>XML built locally, not yet signed.</summary>
    public const string Generado = "Generado";

    /// <summary>XML signed, not yet sent to the DGII.</summary>
    public const string Firmado = "Firmado";

    /// <summary>Sent to the DGII; awaiting validation result.</summary>
    public const string Enviado = "Enviado";

    public const string Aceptado = "Aceptado";

    public const string AceptadoCondicional = "AceptadoCondicional";

    public const string Rechazado = "Rechazado";

    /// <summary>
    /// The signed e-CF could not be transmitted to the DGII (no connectivity / DGII unreachable).
    /// Per DGII rules this must be resolved — successfully sent — within 72 hours of
    /// <see cref="EcfSubmission.ContingencySince"/>.
    /// </summary>
    public const string Contingencia = "Contingencia";

    /// <summary>A local error unrelated to connectivity prevented submission (e.g. bad configuration).</summary>
    public const string Error = "Error";
}
