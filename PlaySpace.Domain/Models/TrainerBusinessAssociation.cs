namespace PlaySpace.Domain.Models;

public enum AssociationStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2
}

public class TrainerBusinessAssociation
{
    public Guid Id { get; set; }
    public Guid TrainerProfileId { get; set; }
    public Guid BusinessProfileId { get; set; }
    public AssociationStatus Status { get; set; } = AssociationStatus.Pending;

    // Confirmation token for email verification
    public string? ConfirmationToken { get; set; }
    public DateTime? ConfirmationTokenExpiresAt { get; set; }

    // Association permissions (set by business when confirming)
    /// <summary>
    /// Trainer can run their own independent trainings at the business location
    /// </summary>
    public bool CanRunOwnTrainings { get; set; } = false;

    /// <summary>
    /// Trainer works as an employee of the business profile
    /// </summary>
    public bool IsEmployee { get; set; } = false;

    /// <summary>
    /// Unique color for this business in trainer's calendar/UI (hex format, e.g. "#FF5733")
    /// </summary>
    public string? Color { get; set; }

    // Pricing set by business
    /// <summary>
    /// Hourly rate for this trainer at this business (net price).
    /// </summary>
    public decimal? HourlyRate { get; set; }

    /// <summary>
    /// VAT rate percentage (e.g., 23 for 23%).
    /// </summary>
    public decimal? VatRate { get; set; }

    /// <summary>
    /// Gross hourly rate (HourlyRate + VAT). Calculated field.
    /// </summary>
    public decimal? GrossHourlyRate { get; set; }

    /// <summary>
    /// Maximum number of users this trainer can handle per session at this business.
    /// Null means no limit (use trainer's default or facility's limit).
    /// </summary>
    public int? MaxNumberOfUsers { get; set; }

    // Audit fields
    public DateTime RequestedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    // Navigation properties
    public TrainerProfile? TrainerProfile { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
}
