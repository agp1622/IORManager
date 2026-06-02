using System.ComponentModel.DataAnnotations;

namespace IORManager.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Admin | Manager | User</summary>
    [Required, MaxLength(50)]
    public string Role { get; set; } = "User";

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
