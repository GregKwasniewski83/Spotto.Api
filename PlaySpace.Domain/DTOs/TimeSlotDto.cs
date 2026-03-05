namespace PlaySpace.Domain.DTOs;

public class TimeSlotItemDto
{
    public required string Id { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsBooked { get; set; }
    public string? BookedBy { get; set; }

    /// <summary>
    /// Business this slot is assigned to. Null = available for trainer's own trainings.
    /// </summary>
    public Guid? AssociatedBusinessId { get; set; }

    /// <summary>
    /// Name of the associated business (for display purposes).
    /// </summary>
    public string? AssociatedBusinessName { get; set; }

    /// <summary>
    /// Color from the trainer-business association (hex format, e.g. "#FF6B6B").
    /// </summary>
    public string? Color { get; set; }
}

public class UpdateTimeSlotsDto
{
    // All-time templates
    public Dictionary<string, List<TimeSlotItemDto>> AllTimeSlots { get; set; } = new();
    // Keys: "weekdays", "saturday", "sunday"
    
    // Date-specific overrides  
    public Dictionary<string, List<TimeSlotItemDto>> DateSpecificSlots { get; set; } = new();
    // Keys: "2024-01-15", "2024-01-16", etc.
}

public class GetTimeSlotsResponseDto
{
    // All-time templates
    public Dictionary<string, List<TimeSlotItemDto>> AllTimeSlots { get; set; } = new();
    // Keys: "weekdays", "saturday", "sunday"
    
    // Date-specific overrides  
    public Dictionary<string, List<TimeSlotItemDto>> DateSpecificSlots { get; set; } = new();
    // Keys: "2024-01-15", "2024-01-16", etc.
}

public class TimeSlotDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsBooked { get; set; }
    public Guid? BookedByUserId { get; set; }
    public DateTime? Date { get; set; }
    public bool IsAllTime { get; set; }
    public int? ScheduleType { get; set; } // Using int to avoid circular reference
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TrainerDateTimeSlotsDto
{
    public DateTime Date { get; set; }
    public List<TimeSlotItemDto> TimeSlots { get; set; } = new();
    public bool IsFromTemplate { get; set; } // Indicates if slots are from weekly template or date-specific
    public string? TemplateType { get; set; } // "weekdays", "saturday", "sunday" if from template
}

/// <summary>
/// DTO for setting a trainer's time slot availability with business assignment.
/// </summary>
public class SetTrainerTimeSlotDto
{
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Business to assign this slot to. Null = available for trainer's own trainings.
    /// Must be a confirmed association.
    /// </summary>
    public Guid? AssociatedBusinessId { get; set; }
}

/// <summary>
/// DTO for updating trainer schedule templates with business assignments.
/// </summary>
public class UpdateTrainerScheduleWithBusinessDto
{
    /// <summary>
    /// Schedule type: "weekdays", "saturday", or "sunday"
    /// </summary>
    public required string ScheduleType { get; set; }

    /// <summary>
    /// List of time slots with their business assignments.
    /// </summary>
    public List<SetTrainerTimeSlotDto> TimeSlots { get; set; } = new();
}

/// <summary>
/// DTO for updating trainer date-specific availability with business assignments.
/// </summary>
public class UpdateTrainerDateAvailabilityWithBusinessDto
{
    /// <summary>
    /// The specific date (format: "2025-01-15")
    /// </summary>
    public required DateTime Date { get; set; }

    /// <summary>
    /// List of time slots with their business assignments.
    /// </summary>
    public List<SetTrainerTimeSlotDto> TimeSlots { get; set; } = new();
}