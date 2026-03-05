using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public enum TrainerType
{
    Independent = 0,
    Employee = 1
}

public class TrainerProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TrainerType TrainerType { get; set; } = TrainerType.Independent;
    public string? Nip { get; set; }
    public string? CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Specializations { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public decimal VatRate { get; set; } = 23;
    public decimal GrossHourlyRate { get; set; }
    public string? Description { get; set; }
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public int ExperienceYears { get; set; }
    public decimal Rating { get; set; }
    public int TotalSessions { get; set; }
    public List<string> AssociatedBusinessIds { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // TPay registration fields
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; } = "PL";
    public string? Regon { get; set; }
    public string? Krs { get; set; }
    public int? LegalForm { get; set; }
    public int? CategoryId { get; set; }
    public string? Mcc { get; set; }
    public string? Website { get; set; }
    public string? WebsiteDescription { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonSurname { get; set; }
    
    // TPay merchant data (after registration)
    public string? TPayMerchantId { get; set; }
    public string? TPayAccountId { get; set; }
    public string? TPayPosId { get; set; }
    public string? TPayActivationLink { get; set; }
    public int? TPayVerificationStatus { get; set; }
    public DateTime? TPayRegisteredAt { get; set; }
    
    public User? User { get; set; }
    public List<TrainerScheduleTemplate> ScheduleTemplates { get; set; } = new();
    public List<TrainerDateAvailability> DateAvailabilities { get; set; } = new();
}

public class TrainerScheduleTemplate
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Business this slot is assigned to.
    /// Null = available for trainer's own trainings (requires reservation first).
    /// </summary>
    public Guid? AssociatedBusinessId { get; set; }

    public TrainerProfile? TrainerProfile { get; set; }
    public BusinessProfile? AssociatedBusiness { get; set; }
}

public class TrainerDateAvailability
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public DateTime Date { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Business this slot is assigned to.
    /// Null = available for trainer's own trainings (requires reservation first).
    /// </summary>
    public Guid? AssociatedBusinessId { get; set; }

    public TrainerProfile? TrainerProfile { get; set; }
    public BusinessProfile? AssociatedBusiness { get; set; }
}