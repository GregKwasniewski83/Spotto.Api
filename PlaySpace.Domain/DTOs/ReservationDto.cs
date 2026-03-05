using System.Text.Json.Serialization;

namespace PlaySpace.Domain.DTOs;

public class CreateReservationDto
{
    public Guid FacilityId { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public Guid? TrainerProfileId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? PurchaseId { get; set; }
    public int NumberOfUsers { get; set; } = 1;
    public bool PayForAllUsers { get; set; } = true;
}

public class AdminCreateReservationDto
{
    public Guid FacilityId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public Guid? TrainerProfileId { get; set; }
    public decimal? CustomPrice { get; set; }
    public string? Notes { get; set; }
    public int NumberOfUsers { get; set; } = 1;
    public bool PayForAllUsers { get; set; } = true;

    // Guest information for non-registered customers
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }
}

public class UpdateReservationDto
{
    public Guid? TrainerProfileId { get; set; }
}

public class AgentCancelReservationDto
{
    public string? Notes { get; set; }
}

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid? GroupId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public decimal RemainingPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string? FacilityName { get; set; }
    public string? BusinessProfileName { get; set; }
    public string? BusinessEmail { get; set; }
    public string? BusinessPhoneNumber { get; set; }
    public Guid? TrainerProfileId { get; set; }
    public decimal? TrainerPrice { get; set; }
    public string? TrainerDisplayName { get; set; }
    public string? TrainerEmail { get; set; }
    public string? TrainerPhoneNumber { get; set; }
    public string? BusinessAvatarUrl { get; set; }
    public string? TrainerAvatarUrl { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? ProductPurchaseId { get; set; }
    public Guid? TrainingId { get; set; }
    public string? Notes { get; set; }

    // Guest information for non-registered customers
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }

    // Tracks who created the reservation
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }

    // Tracks who cancelled the reservation (for agent cancellations)
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationNotes { get; set; }

    // Slot details for partial cancellation
    public List<SlotDetailDto>? Slots { get; set; }

    // Payment status indicators
    public bool IsUnpaid { get; set; }  // True if reservation has no payment and no product purchase
    public bool CanPayOnline { get; set; }  // True if user can pay for this reservation online

    // Multi-user booking support
    public int NumberOfUsers { get; set; } = 1;
    [JsonPropertyName("payForAllUsers")]
    public bool PaidForAllUsers { get; set; } = true;
}

// DTO for initiating payment for an existing reservation
public class PayForReservationDto
{
    public required string CustomerEmail { get; set; }
    public required string CustomerName { get; set; }
    public required string CustomerPhone { get; set; }
    public required string ReturnUrl { get; set; }
    public required string ErrorUrl { get; set; }
    public string? PushToken { get; set; }
}

public class PayForReservationResponseDto
{
    public Guid PaymentId { get; set; }
    public Guid ReservationId { get; set; }
    public decimal Amount { get; set; }
    public required string PaymentUrl { get; set; }
    public string? TransactionId { get; set; }
}

// Agent applies product to reservation
public class ApplyProductToReservationDto
{
    public required string PurchaseIdPrefix { get; set; }  // First 8 characters of purchase ID
    public required string UserEmail { get; set; }  // Customer's email
}

public class ApplyProductResultDto
{
    public Guid ReservationId { get; set; }
    public Guid ProductPurchaseId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int RemainingUsage { get; set; }
    public int TotalUsage { get; set; }
    public string PurchaseStatus { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

// Agent reschedules reservation
public class RescheduleReservationDto
{
    public DateTime NewDate { get; set; }
    public List<string> NewTimeSlots { get; set; } = new();
    public Guid? NewFacilityId { get; set; }  // Optional, must be same business
    public string? Notes { get; set; }  // Reason for rescheduling
}

public class RescheduleResultDto
{
    public Guid ReservationId { get; set; }
    public DateTime OldDate { get; set; }
    public DateTime NewDate { get; set; }
    public List<string> OldTimeSlots { get; set; } = new();
    public List<string> NewTimeSlots { get; set; } = new();
    public Guid OldFacilityId { get; set; }
    public Guid NewFacilityId { get; set; }
    public string? OldFacilityName { get; set; }
    public string? NewFacilityName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// Update reservation notes
public class UpdateReservationNotesDto
{
    public required string Notes { get; set; }
}