using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public class TimeSlot
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string Time { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsBooked { get; set; }
    public Guid? BookedByUserId { get; set; }
    
    // For date-specific slots
    public DateTime? Date { get; set; }
    
    // For template slots
    public bool IsAllTime { get; set; } = false;
    public ScheduleType? ScheduleType { get; set; } // Only for IsAllTime = true
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public Facility? Facility { get; set; }
    public User? BookedByUser { get; set; }
}
