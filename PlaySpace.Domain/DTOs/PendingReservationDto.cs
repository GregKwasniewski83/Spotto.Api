namespace PlaySpace.Domain.DTOs;

public class CreatePendingReservationDto
{
    public Guid FacilityId { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public Guid? TrainerProfileId { get; set; }
}

public class PendingReservationDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public Guid UserId { get; set; }
    public Guid? TrainerProfileId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public string? FacilityName { get; set; }
    public string? TrainerDisplayName { get; set; }
    public int RemainingMinutes { get; set; }
}