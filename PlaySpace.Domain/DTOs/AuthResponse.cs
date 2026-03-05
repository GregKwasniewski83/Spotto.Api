using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.DTOs;

public class AuthResponse
{
    public required User User { get; set; }
    public required string Token { get; set; }
    public required string RefreshToken { get; set; }
    public required string ExpiresAt { get; set; }
}

public class RegistrationResponse
{
    public bool Success { get; set; }
    public bool RequiresEmailVerification { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
}
