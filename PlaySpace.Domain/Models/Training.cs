using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public class Training
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid? ReservationId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Specialization { get; set; }
    public int Duration { get; set; } // Duration in minutes
    public int MaxParticipants { get; set; }
    public decimal Price { get; set; }  // Net price
    public decimal? GrossPrice { get; set; }
    public int VatRate { get; set; } = 23;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public TrainerProfile? TrainerProfile { get; set; }
    public Facility? Facility { get; set; }
    public Reservation? Reservation { get; set; }
    public List<TrainingSession> Sessions { get; set; } = new();
    public List<TrainingParticipant> Participants { get; set; } = new();
}

public class TrainingSession
{
    public Guid Id { get; set; }
    public Guid TrainingId { get; set; }
    public DateTime Date { get; set; }
    public required string StartTime { get; set; }
    public required string EndTime { get; set; }
    public int CurrentParticipants { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public Training? Training { get; set; }
}

public class TrainingParticipant
{
    public Guid Id { get; set; }
    public Guid TrainingId { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentId { get; set; }
    public DateTime JoinedAt { get; set; }
    public string Status { get; set; } = "Active"; // Active, Cancelled, Completed
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public Training? Training { get; set; }
    public User? User { get; set; }
    public Payment? Payment { get; set; }
}