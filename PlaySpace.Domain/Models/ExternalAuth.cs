namespace PlaySpace.Domain.Models;

public class ExternalAuth
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AuthProvider Provider { get; set; }
    public required string ExternalUserId { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}

public enum AuthProvider
{
    Local = 0,
    Google = 1,
    Apple = 2
}