using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.DTOs;

public class TrainerProfileDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TrainerType TrainerType { get; set; }
    public string? Nip { get; set; }
    public string? CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Specializations { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public decimal VatRate { get; set; }
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
    public TrainerAvailabilityDto? Availability { get; set; }

    // TPay registration fields
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; }
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
}

public class CreateTrainerProfileDto
{
    public TrainerType TrainerType { get; set; } = TrainerType.Independent;
    public string? Nip { get; set; }
    public string? CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public List<string> Specializations { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public decimal VatRate { get; set; } = 23;
    public decimal GrossHourlyRate { get; set; }
    public string? Description { get; set; }
    public TrainerAvailabilityDto? Availability { get; set; }
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public int ExperienceYears { get; set; }
    public decimal Rating { get; set; } = 0;
    public int TotalSessions { get; set; } = 0;
    public List<string> AssociatedBusinessIds { get; set; } = new();

    // TPay registration fields (optional)
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
    public bool AutoRegisterWithTPay { get; set; } = true;
}

public class UpdateTrainerProfileDto
{
    public string? Nip { get; set; }
    public string? CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public List<string> Specializations { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public decimal VatRate { get; set; } = 23;
    public decimal GrossHourlyRate { get; set; }
    public string? Description { get; set; }
    public TrainerAvailabilityDto? Availability { get; set; }
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public int ExperienceYears { get; set; }
    public decimal Rating { get; set; }
    public int TotalSessions { get; set; }
    public List<string> AssociatedBusinessIds { get; set; } = new();

    // TPay registration fields (optional)
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; }
    public string? Regon { get; set; }
    public string? Krs { get; set; }
    public int? LegalForm { get; set; }
    public int? CategoryId { get; set; }
    public string? Mcc { get; set; }
    public string? Website { get; set; }
    public string? WebsiteDescription { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonSurname { get; set; }
    public bool UpdateTPayRegistration { get; set; } = false;
}

public class TrainerAvailabilityDto
{
    public List<TimeSlotItemDto> Weekdays { get; set; } = new();
    public List<TimeSlotItemDto> Saturday { get; set; } = new();
    public List<TimeSlotItemDto> Sunday { get; set; } = new();
    public Dictionary<string, List<TimeSlotItemDto>> SpecificDates { get; set; } = new();
}

public class UpdateTrainerTimeSlotsDto
{
    // All-time templates
    public Dictionary<string, List<TimeSlotItemDto>> AllTimeSlots { get; set; } = new();
    // Keys: "weekdays", "saturday", "sunday"
    
    // Date-specific overrides  
    public Dictionary<string, List<TimeSlotItemDto>> DateSpecificSlots { get; set; } = new();
    // Keys: "2025-01-15", "2025-01-16", etc.
}

public class GetTrainerTimeSlotsResponseDto
{
    // All-time templates
    public Dictionary<string, List<TimeSlotItemDto>> AllTimeSlots { get; set; } = new();
    // Keys: "weekdays", "saturday", "sunday"
    
    // Date-specific overrides  
    public Dictionary<string, List<TimeSlotItemDto>> DateSpecificSlots { get; set; } = new();
    // Keys: "2025-01-15", "2025-01-16", etc.
}

public class TrainerScheduleTemplateDto
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public int ScheduleType { get; set; } // 0=Weekdays, 1=Saturday, 2=Sunday
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Business this slot is assigned to. Null = available for trainer's own trainings.
    /// </summary>
    public Guid? AssociatedBusinessId { get; set; }

    /// <summary>
    /// Name of the associated business (for display purposes).
    /// </summary>
    public string? AssociatedBusinessName { get; set; }

    /// <summary>
    /// Color from the trainer-business association (hex format).
    /// </summary>
    public string? Color { get; set; }
}