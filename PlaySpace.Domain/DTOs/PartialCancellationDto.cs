using System.Text.Json.Serialization;

namespace PlaySpace.Domain.DTOs;

public class CancelSlotsDto
{
    public List<Guid> SlotIds { get; set; } = new();
}

public class PartialCancellationResponseDto
{
    public Guid ReservationId { get; set; }
    public List<SlotDto> CancelledSlots { get; set; } = new();
    public List<SlotDto> RemainingSlots { get; set; } = new();
    public decimal OriginalTotal { get; set; }
    public decimal RemainingTotal { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal RefundFee { get; set; }
    public decimal RefundPercentage { get; set; }
    public string NewStatus { get; set; } = string.Empty;
}

public class SlotDto
{
    public Guid Id { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ReservationWithSlotsDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Date { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal RemainingPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid? TrainerProfileId { get; set; }
    public decimal? TrainerPrice { get; set; }
    public string? TrainerDisplayName { get; set; }
    
    // Additional reservation details
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Notes { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? ProductPurchaseId { get; set; }
    public string? PaymentStatus { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }

    // Multi-user booking info
    public int NumberOfUsers { get; set; } = 1;
    [JsonPropertyName("payForAllUsers")]
    public bool PaidForAllUsers { get; set; } = true;

    public List<SlotDetailDto> Slots { get; set; } = new();

    public int TotalSlots => Slots.Count;
    public int ActiveSlots => Slots.Count(s => s.Status == "Active");
    public int CancelledSlots => Slots.Count(s => s.Status == "Cancelled");
}

public class SlotDetailDto
{
    public Guid Id { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public decimal SlotPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
