namespace PlaySpace.Domain.DTOs;

public class BusinessDateAvailabilitySlotDto
{
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsFromTemplate { get; set; } // Indicates if this comes from template or date-specific override
}

public class BusinessDateAvailabilityDto
{
    public DateTime Date { get; set; }
    public List<BusinessDateAvailabilitySlotDto> TimeSlots { get; set; } = new();
    public string TemplateType { get; set; } = string.Empty; // "weekdays", "saturday", "sunday"
}

public class CreateBusinessDateAvailabilityDto
{
    public DateTime Date { get; set; }
    public List<BusinessDateAvailabilitySlotDto> TimeSlots { get; set; } = new();
}

public class UpdateBusinessDateAvailabilityDto
{
    public DateTime Date { get; set; }
    public List<BusinessDateAvailabilitySlotDto> TimeSlots { get; set; } = new();
}