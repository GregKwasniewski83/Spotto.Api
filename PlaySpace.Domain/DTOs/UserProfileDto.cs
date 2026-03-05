namespace PlaySpace.Domain.DTOs;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public bool PlayerTerms { get; set; }
    public bool BusinessTerms { get; set; }
    public bool TrainerTerms { get; set; }
    public List<string> ActivityInterests { get; set; } = new();
    public List<string> Roles { get; set; } = new();
}

public class UpdateUserProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public List<string>? ActivityInterests { get; set; }
}

public class UpdateUserActivityInterestsDto
{
    public List<string> ActivityInterests { get; set; } = new();
}