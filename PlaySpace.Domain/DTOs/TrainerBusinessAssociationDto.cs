namespace PlaySpace.Domain.DTOs;

public class AssociateBusinessProfileDto
{
    public required List<string> BusinessProfileIds { get; set; } = new();
}

public class DisassociateBusinessProfileDto
{
    public required List<string> BusinessProfileIds { get; set; } = new();
}

public class BusinessAssociationResultDto
{
    public List<string> Successful { get; set; } = new();
    public List<BusinessAssociationErrorDto> Failed { get; set; } = new();
}

public class BusinessAssociationErrorDto
{
    public required string BusinessProfileId { get; set; }
    public required string Error { get; set; }
}

// New DTOs for confirmation-based associations
public class RequestAssociationDto
{
    public required Guid BusinessProfileId { get; set; }
}

public class ConfirmAssociationDto
{
    public required string Token { get; set; }
    public bool Confirm { get; set; } = true;  // true = confirm, false = reject
    public string? RejectionReason { get; set; }

    // Permissions (only used when Confirm = true)
    /// <summary>
    /// Trainer can run their own independent trainings at the business location
    /// </summary>
    public bool CanRunOwnTrainings { get; set; } = false;

    /// <summary>
    /// Trainer works as an employee of the business profile
    /// </summary>
    public bool IsEmployee { get; set; } = false;
}

public class TrainerBusinessAssociationResponseDto
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public Guid BusinessProfileId { get; set; }
    public string Status { get; set; } = string.Empty;

    // Association permissions
    /// <summary>
    /// Trainer can run their own independent trainings at the business location
    /// </summary>
    public bool CanRunOwnTrainings { get; set; }

    /// <summary>
    /// Trainer works as an employee of the business profile
    /// </summary>
    public bool IsEmployee { get; set; }

    /// <summary>
    /// Unique color for this business in trainer's calendar/UI (hex format, e.g. "#FF5733")
    /// </summary>
    public string? Color { get; set; }

    // Pricing (set by business)
    /// <summary>
    /// Hourly rate for this trainer at this business (net price).
    /// </summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>
    /// VAT rate percentage (e.g., 23 for 23%).
    /// </summary>
    public decimal? VatRate { get; set; }

    /// <summary>
    /// Gross hourly rate (HourlyRate + VAT).
    /// </summary>
    public decimal? GrossHourlyRate { get; set; }

    /// <summary>
    /// Maximum number of users this trainer can handle per session.
    /// Null means no limit.
    /// </summary>
    public int? MaxNumberOfUsers { get; set; }

    // Trainer info
    public string? TrainerDisplayName { get; set; }
    public string? TrainerAvatarUrl { get; set; }
    public string? TrainerEmail { get; set; }

    // Business info
    public string? BusinessName { get; set; }
    public string? BusinessCity { get; set; }
    public string? BusinessAvatarUrl { get; set; }

    // Timestamps
    public DateTime RequestedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class PendingAssociationRequestDto
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public string TrainerDisplayName { get; set; } = string.Empty;
    public string? TrainerAvatarUrl { get; set; }
    public string? TrainerEmail { get; set; }
    public List<string> TrainerSpecializations { get; set; } = new();
    public decimal TrainerHourlyRate { get; set; }
    public DateTime RequestedAt { get; set; }
}

/// <summary>
/// DTO for business to update trainer's pricing for this association.
/// </summary>
public class UpdateTrainerPricingDto
{
    /// <summary>
    /// Hourly rate for this trainer at this business (net price).
    /// </summary>
    public decimal HourlyRate { get; set; }

    /// <summary>
    /// VAT rate percentage (e.g., 23 for 23%).
    /// </summary>
    public decimal VatRate { get; set; }
}

/// <summary>
/// DTO for business to update association permissions.
/// </summary>
public class UpdateAssociationPermissionsDto
{
    /// <summary>
    /// Trainer can run their own independent trainings at the business location.
    /// </summary>
    public bool CanRunOwnTrainings { get; set; }

    /// <summary>
    /// Trainer works as an employee of the business profile.
    /// </summary>
    public bool IsEmployee { get; set; }

    /// <summary>
    /// Maximum number of users this trainer can handle per session.
    /// Null means no limit.
    /// </summary>
    public int? MaxNumberOfUsers { get; set; }
}

/// <summary>
/// Request DTO for getting available trainers for specific timeslots.
/// </summary>
public class GetAvailableTrainersRequestDto
{
    /// <summary>
    /// The date to check availability for.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// List of timeslots to check (e.g., ["09:00", "09:30", "10:00"]).
    /// </summary>
    public List<string> TimeSlots { get; set; } = new();
}

/// <summary>
/// Response DTO for available trainers for a business.
/// </summary>
public class BusinessAvailableTrainerDto
{
    public Guid TrainerProfileId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Color assigned to this trainer for calendar display (from association).
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Hourly rate for this trainer at this business (net price).
    /// </summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>
    /// VAT rate percentage.
    /// </summary>
    public decimal? VatRate { get; set; }

    /// <summary>
    /// Gross hourly rate (HourlyRate + VAT).
    /// </summary>
    public decimal? GrossHourlyRate { get; set; }

    /// <summary>
    /// Whether trainer can run own trainings.
    /// </summary>
    public bool CanRunOwnTrainings { get; set; }

    /// <summary>
    /// Whether trainer is an employee.
    /// </summary>
    public bool IsEmployee { get; set; }

    /// <summary>
    /// Maximum number of users this trainer can handle per session.
    /// Null means no limit.
    /// </summary>
    public int? MaxNumberOfUsers { get; set; }

    /// <summary>
    /// List of timeslots this trainer is available for (from the requested slots).
    /// </summary>
    public List<string> AvailableTimeSlots { get; set; } = new();
}