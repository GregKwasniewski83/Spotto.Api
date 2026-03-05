namespace PlaySpace.Domain.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Password { get; set; }
    public bool PlayerTerms { get; set; }
    public bool BusinessTerms { get; set; }
    public bool TrainerTerms { get; set; }
    public bool IsEmailVerified { get; set; }
    public List<string> ActivityInterests { get; set; } = new();
    public List<string> Roles { get; set; } = new List<string>();
}
