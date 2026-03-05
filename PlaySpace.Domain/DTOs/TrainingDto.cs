namespace PlaySpace.Domain.DTOs;

public class TrainingDto
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public TrainerProfileDto? TrainerProfile { get; set; }
    public Guid FacilityId { get; set; }
    public FacilityDto? Facility { get; set; }
    public Guid? ReservationId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Specialization { get; set; }
    public int Duration { get; set; }
    public int MaxParticipants { get; set; }
    public decimal Price { get; set; }  // Net price
    public decimal? GrossPrice { get; set; }
    public int VatRate { get; set; } = 23;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<TrainingSessionDto> Sessions { get; set; } = new();
    public List<TrainingParticipantDto> Participants { get; set; } = new();
    public int CurrentParticipantCount { get; set; }
    public int PendingParticipantCount { get; set; }
    public bool IsFull => (CurrentParticipantCount + PendingParticipantCount) >= MaxParticipants;
}

public class CreateTrainingDto
{
    public Guid FacilityId { get; set; }
    public Guid? ReservationId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Specialization { get; set; }
    public int Duration { get; set; }
    public int MaxParticipants { get; set; }
    public decimal Price { get; set; }  // Net price
    public decimal? GrossPrice { get; set; }
    public int VatRate { get; set; } = 23;
    public List<CreateTrainingSessionDto> Sessions { get; set; } = new();
}

public class UpdateTrainingDto
{
    public Guid FacilityId { get; set; }
    public Guid? ReservationId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Specialization { get; set; }
    public int Duration { get; set; }
    public int MaxParticipants { get; set; }
    public decimal Price { get; set; }  // Net price
    public decimal? GrossPrice { get; set; }
    public int VatRate { get; set; } = 23;
    public List<CreateTrainingSessionDto> Sessions { get; set; } = new();
}

public class TrainingSessionDto
{
    public Guid Id { get; set; }
    public Guid TrainingId { get; set; }
    public DateTime Date { get; set; }
    public required string StartTime { get; set; }
    public required string EndTime { get; set; }
    public int CurrentParticipants { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateTrainingSessionDto
{
    public required string Date { get; set; } // "2025-01-25" format
    public required string StartTime { get; set; } // "10:00" format
    public required string EndTime { get; set; } // "12:00" format
}

public class TrainingParticipantDto
{
    public Guid Id { get; set; }
    public Guid TrainingId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // User information for display
    public string? UserFirstName { get; set; }
    public string? UserLastName { get; set; }
    public string? UserEmail { get; set; }
}

public class JoinTrainingDto
{
    public required Guid PaymentId { get; set; }
    public string? Notes { get; set; }
}

public class CreateTrainingPaymentDto
{
    public Guid UserId { get; set; }
    public Guid TrainingId { get; set; }
    public string? Notes { get; set; }
    
    // Customer information for TPay
    public required string CustomerEmail { get; set; }
    public required string CustomerName { get; set; }
    public required string CustomerPhone { get; set; }
    
    // URLs for redirects
    public required string ReturnUrl { get; set; }
    public required string ErrorUrl { get; set; }

    // Push notification token
    public string? PushToken { get; set; }
}

public class UpdateParticipantStatusDto
{
    public required string Status { get; set; } // Active, Cancelled, Completed
    public string? Notes { get; set; }
}

public class TrainingSearchDto
{
    public string? City { get; set; }
    public string? Date { get; set; } // Changed to string for better control over parsing
    public string? Activity { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MaxParticipants { get; set; }
}

public class TrainingSearchResultDto
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Specialization { get; set; }
    public int Duration { get; set; }
    public int MaxParticipants { get; set; }
    public int CurrentParticipantCount { get; set; }
    public decimal Price { get; set; }  // Net price
    public decimal? GrossPrice { get; set; }
    public int VatRate { get; set; } = 23;
    public bool IsFull => CurrentParticipantCount >= MaxParticipants;

    
    // Trainer information
    public TrainerSearchResultDto? Trainer { get; set; }
    
    // Facility information
    public FacilitySearchResultDto? Facility { get; set; }
    
    // Sessions for the searched date
    public List<TrainingSessionDto> Sessions { get; set; } = new();
    
    // Participants information
    public List<TrainingParticipantDto> Participants { get; set; } = new();
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TrainerSearchResultDto
{
    public Guid Id { get; set; }
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public List<string> Specializations { get; set; } = new();
    public decimal HourlyRate { get; set; }
    public string? Description { get; set; }
    public List<string> Certifications { get; set; } = new();
    public List<string> Languages { get; set; } = new();
    public int ExperienceYears { get; set; }
    public decimal Rating { get; set; }
    public int TotalSessions { get; set; }
}

public class ReserveTrainingDto
{
    public string? Notes { get; set; }
}

public class PendingTrainingParticipantDto
{
    public Guid Id { get; set; }
    public Guid TrainingId { get; set; }
    public Guid UserId { get; set; }
    public string? Notes { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}