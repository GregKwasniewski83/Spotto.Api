using System.Text.Json.Serialization;

namespace PlaySpace.Domain.DTOs;

public class CreateGroupReservationDto
{
    [JsonPropertyName("facilityReservations")]
    public List<FacilityReservationDto> FacilityReservations { get; set; } = new();

    [JsonPropertyName("paymentId")]
    public Guid? PaymentId { get; set; }

    [JsonPropertyName("purchaseId")]
    public Guid? PurchaseId { get; set; }
}

public class FacilityReservationDto
{
    [JsonPropertyName("facilityId")]
    public Guid FacilityId { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("timeSlots")]
    public List<string> TimeSlots { get; set; } = new();

    [JsonPropertyName("trainerProfileId")]
    public Guid? TrainerProfileId { get; set; }

    [JsonPropertyName("numberOfUsers")]
    public int NumberOfUsers { get; set; } = 1;

    [JsonPropertyName("payForAllUsers")]
    public bool PayForAllUsers { get; set; } = true;
}

public class GroupReservationResponseDto
{
    public Guid GroupId { get; set; }
    public List<ReservationDto> Reservations { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating admin/agent group reservations without requiring payment upfront
/// </summary>
public class CreateAdminGroupReservationDto
{
    [JsonPropertyName("facilityReservations")]
    public List<AdminFacilityReservationDto> FacilityReservations { get; set; } = new();

    /// <summary>
    /// Optional: User ID if reserving for a registered user.
    /// If null, guest information should be provided.
    /// </summary>
    [JsonPropertyName("userId")]
    public Guid? UserId { get; set; }

    /// <summary>
    /// Guest name for non-registered customers (required if UserId is null)
    /// </summary>
    [JsonPropertyName("guestName")]
    public string? GuestName { get; set; }

    /// <summary>
    /// Guest phone number
    /// </summary>
    [JsonPropertyName("guestPhone")]
    public string? GuestPhone { get; set; }

    /// <summary>
    /// Guest email address
    /// </summary>
    [JsonPropertyName("guestEmail")]
    public string? GuestEmail { get; set; }

    /// <summary>
    /// General notes for the entire group reservation
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// Admin/agent facility reservation with additional fields
/// </summary>
public class AdminFacilityReservationDto
{
    [JsonPropertyName("facilityId")]
    public Guid FacilityId { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("timeSlots")]
    public List<string> TimeSlots { get; set; } = new();

    [JsonPropertyName("trainerProfileId")]
    public Guid? TrainerProfileId { get; set; }

    [JsonPropertyName("numberOfUsers")]
    public int NumberOfUsers { get; set; } = 1;

    [JsonPropertyName("payForAllUsers")]
    public bool PayForAllUsers { get; set; } = true;

    /// <summary>
    /// Custom price override for this facility reservation (optional)
    /// </summary>
    [JsonPropertyName("customPrice")]
    public decimal? CustomPrice { get; set; }

    /// <summary>
    /// Notes specific to this facility reservation
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}