namespace PlaySpace.Domain.Models;

public class ReservationSlot
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string TimeSlot { get; set; } = string.Empty; // e.g., "08:00-09:00"
    public decimal SlotPrice { get; set; }
    public string Status { get; set; } = "Active"; // Active, Cancelled
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Reservation? Reservation { get; set; }
}
