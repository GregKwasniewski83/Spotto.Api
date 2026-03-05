using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public class PendingTimeSlotReservation
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public Guid UserId { get; set; }
    public Guid? TrainerProfileId { get; set; }
    public Guid? PaymentId { get; set; }  // Link to the payment that created this pending reservation
    public int NumberOfUsers { get; set; } = 1;
    public bool PayForAllUsers { get; set; } = true;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Facility? Facility { get; set; }
    public User? User { get; set; }
    public TrainerProfile? TrainerProfile { get; set; }
}