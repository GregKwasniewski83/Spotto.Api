namespace PlaySpace.Domain.DTOs;

public class RegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public List<string> Roles { get; set; } = new List<string> { "Player" };
    public List<string> ActivityInterests { get; set; } = new();
    public bool PlayerTerms { get; set; } = false;
    public bool BusinessTerms { get; set; } = false;
    public bool TrainerTerms { get; set; } = false;
}
