using System.ComponentModel.DataAnnotations;

namespace IORManager.Models;

public class QuoteAttachment
{
    public int Id { get; set; }

    public Guid QuoteId { get; set; }
    public Quote Quote { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public byte[] FileData { get; set; } = Array.Empty<byte>();

    public DateTime UploadedAt { get; set; }
}
