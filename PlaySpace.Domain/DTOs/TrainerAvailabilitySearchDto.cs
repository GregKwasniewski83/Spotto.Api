namespace PlaySpace.Domain.DTOs;

public class TrainerAvailabilitySearchDto
{
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
}

public class AvailableTrainerDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public List<string> Specializations { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public string? Description { get; set; }
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public int ExperienceYears { get; set; }
    public decimal Rating { get; set; }
    public int TotalSessions { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> AvailableTimeSlots { get; set; } = new(); // The specific slots they're available for
}