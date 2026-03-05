namespace PlaySpace.Domain.Models;

public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Password { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }
    public AuthProvider AuthProvider { get; set; } = AuthProvider.Local;
    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }

    public bool PlayerTerms { get; set; }
    public bool BusinessTerms { get; set; }
    public bool TrainerTerms { get; set; }
    public List<string> ActivityInterests { get; set; } = new();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<ExternalAuth> ExternalAuths { get; set; } = new List<ExternalAuth>();
}
