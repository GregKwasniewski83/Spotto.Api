namespace PlaySpace.Domain.DTOs;

public class FacilityDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public int? MaxUsers { get; set; }
    public bool PricePerUser { get; set; } = false;
    public decimal PricePerHour { get; set; }
    public decimal? GrossPricePerHour { get; set; }
    public int VatRate { get; set; } = 23;
    public int MinBookingSlots { get; set; } = 1;
    public Guid UserId { get; set; }
    public Guid? BusinessProfileId { get; set; }
    
    // Address Information
    public required string Street { get; set; }
    public required string City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? AddressLine2 { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public FacilityAvailabilityDto? Availability { get; set; }
}

public class FacilitySearchResultDto : FacilityDto
{
    public bool HasAvailability { get; set; }
    public int AvailableSlots { get; set; }
    public List<string> AvailableTimes { get; set; } = new();
}

public class CreateFacilityDto
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public int? MaxUsers { get; set; }
    public bool PricePerUser { get; set; } = false;
    public decimal PricePerHour { get; set; }
    public decimal? GrossPricePerHour { get; set; }
    public int VatRate { get; set; } = 23;
    public int MinBookingSlots { get; set; } = 1;
    public Guid? BusinessProfileId { get; set; } // Link to business profile for marketplace transactions
    
    // Address Information
    public required string Street { get; set; }
    public required string City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? AddressLine2 { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public FacilityAvailabilityDto? Availability { get; set; }
}

public class UpdateFacilityDto
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string? Description { get; set; }
    public int Capacity { get; set; }
    public int? MaxUsers { get; set; }
    public bool PricePerUser { get; set; } = false;
    public decimal PricePerHour { get; set; }
    public decimal? GrossPricePerHour { get; set; }
    public int VatRate { get; set; } = 23;
    public int MinBookingSlots { get; set; } = 1;
    public Guid? BusinessProfileId { get; set; }
    
    // Address Information
    public required string Street { get; set; }
    public required string City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? AddressLine2 { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public FacilityAvailabilityDto? Availability { get; set; }
}

public class FacilityAvailabilityDto
{
    public List<TimeSlotItemDto> Weekdays { get; set; } = new();
    public List<TimeSlotItemDto> Saturday { get; set; } = new();
    public List<TimeSlotItemDto> Sunday { get; set; } = new();
    public Dictionary<string, List<TimeSlotItemDto>> SpecificDates { get; set; } = new();
}

public class UpdateFacilityAvailabilityDto
{
    public List<TimeSlotItemDto> Weekdays { get; set; } = new();
    public List<TimeSlotItemDto> Saturday { get; set; } = new();
    public List<TimeSlotItemDto> Sunday { get; set; } = new();
    public Dictionary<string, List<TimeSlotItemDto>> SpecificDates { get; set; } = new();
}

public class FacilityDateTimeSlotsDto
{
    public DateTime Date { get; set; }
    public List<TimeSlotItemDto> TimeSlots { get; set; } = new();
    public bool IsFromFacilityTemplate { get; set; }
    public bool IsFromBusinessTemplate { get; set; }
    public string? TemplateType { get; set; } // "weekdays", "saturday", "sunday" if from template
}

// Agent Dashboard DTO for facility with date-specific timeslots and bookings
public class FacilityDateTimeSlotsWithBookingsDto
{
    public Guid FacilityId { get; set; }
    public required string FacilityName { get; set; }
    public DateTime Date { get; set; }
    public List<TimeSlotItemDto> TimeSlots { get; set; } = new();
    public List<ReservationSummaryDto> Reservations { get; set; } = new();
    public bool IsFromFacilityTemplate { get; set; }
    public bool IsFromBusinessTemplate { get; set; }
    public string? TemplateType { get; set; }
}

public class ReservationSummaryDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }
    public List<string> TimeSlots { get; set; } = new();
    public List<ReservationSlotDetailDto> DetailedTimeSlots { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public decimal RemainingPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? TrainerProfileId { get; set; }
    public string? TrainerDisplayName { get; set; }
    public Guid? PaymentId { get; set; }
    public PaymentDetailsDto? PaymentDetails { get; set; }
    public Guid? ProductPurchaseId { get; set; }
    public ProductPurchaseSummaryDto? ProductPurchaseDetails { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductPurchaseSummaryDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductSubtitle { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public Guid BusinessProfileId { get; set; }
    public int TotalUsage { get; set; }
    public int RemainingUsage { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public bool AppliesToAllFacilities { get; set; }
    public List<string>? FacilityIds { get; set; }
}

public class ReservationSlotDetailDto
{
    public Guid Id { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public decimal SlotPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class PaymentDetailsDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string? Status { get; set; }
    public string? TPayStatus { get; set; }
    public DateTime? TPayCompletedAt { get; set; }
    public bool IsRefunded { get; set; }
    public decimal RefundedAmount { get; set; }
    public DateTime? RefundedAt { get; set; }
    public bool IsPaid { get; set; }
    public string? PaymentMethod { get; set; }
}