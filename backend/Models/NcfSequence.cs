using System.ComponentModel.DataAnnotations;

namespace IORManager.Models;

public class NcfSequence
{
    public int Id { get; set; }

    [MaxLength(10)]
    public string CategoryCode { get; set; } = string.Empty;

    public long NextNumber { get; set; }
}
