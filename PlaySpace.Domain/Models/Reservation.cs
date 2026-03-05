using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public class Reservation
{
    public Guid Id { get; set; }
    public Guid? GroupId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public decimal RemainingPrice { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid? TrainerProfileId { get; set; }
    public decimal? TrainerPrice { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? ProductPurchaseId { get; set; }
    public string? Notes { get; set; }

    // Multi-user booking support
    public int NumberOfUsers { get; set; } = 1;
    public bool PaidForAllUsers { get; set; } = true;

    // Guest information for non-registered customers
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }

    // Tracks who created the reservation (Agent or Business Owner)
    public Guid? CreatedById { get; set; }

    // Tracks who cancelled the reservation (for agent cancellations)
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationNotes { get; set; }

    public Facility? Facility { get; set; }
    public User? User { get; set; }
    public TrainerProfile? TrainerProfile { get; set; }
    public Payment? Payment { get; set; }
    public ProductPurchase? ProductPurchase { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<ReservationSlot> Slots { get; set; } = new List<ReservationSlot>();
}
