using Microsoft.AspNetCore.Http;

namespace PlaySpace.Domain.DTOs;

public class ScheduleSlotDto
{
    public required string Id { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsBooked { get; set; }
}

public class BusinessProfileDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Nip { get; set; }
    public required string CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AvatarUrl { get; set; }
    public string? TermsAndConditionsUrl { get; set; }

    // Facility plan file
    public string? FacilityPlanUrl { get; set; }
    public string? FacilityPlanFileName { get; set; }
    public string? FacilityPlanFileType { get; set; }
    public List<ScheduleSlotDto> WeekdaysSchedule { get; set; } = new();
    public List<ScheduleSlotDto> SaturdaySchedule { get; set; } = new();
    public List<ScheduleSlotDto> SundaySchedule { get; set; } = new();
    public Dictionary<string, List<BusinessDateAvailabilitySlotDto>> DateSpecificAvailability { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // TPay registration fields
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; }
    public string? Regon { get; set; }
    public string? Krs { get; set; }
    public int? LegalForm { get; set; }
    public int? CategoryId { get; set; }
    public string? Mcc { get; set; }
    public string? Website { get; set; }
    public string? WebsiteDescription { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonSurname { get; set; }
    
    // TPay merchant data (after registration)
    public string? TPayMerchantId { get; set; }
    public string? TPayAccountId { get; set; }
    public string? TPayPosId { get; set; }
    public string? TPayActivationLink { get; set; }
    public int? TPayVerificationStatus { get; set; }
    public DateTime? TPayRegisteredAt { get; set; }

    // KSeF (Polish e-Invoice System) integration
    public bool KSeFEnabled { get; set; }
    public bool KSeFTokenConfigured { get; set; } // Flag to indicate if token is set (don't expose actual token)
    public string? KSeFEnvironment { get; set; }
    public DateTime? KSeFRegisteredAt { get; set; }
    public DateTime? KSeFLastSyncAt { get; set; }

    // Parent-child business profile relationship
    public Guid? ParentBusinessProfileId { get; set; }
    public string? ParentBusinessProfileName { get; set; }
    public bool UseParentTPay { get; set; }
    public bool UseParentNipForInvoices { get; set; }

    // Effective TPay/Invoice info (resolved from parent if applicable)
    public string? EffectiveTPayMerchantId { get; set; }
    public string? EffectiveNipForInvoices { get; set; }
    public string? EffectiveCompanyNameForInvoices { get; set; }

    // User favourite status (populated when user is authenticated)
    public bool IsFavouritedByCurrentUser { get; set; }
}

public class CreateBusinessProfileDto
{
    // NIP is optional when ParentBusinessProfileId is specified (child uses parent's NIP)
    // Required when creating a standalone business profile
    public string? Nip { get; set; }
    public required string CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AvatarUrl { get; set; }
    
    // Facility plan file (base64 data for upload)
    public string? FacilityPlanUrl { get; set; }
    public string? FacilityPlanFileName { get; set; }
    public string? FacilityPlanFileType { get; set; }
    public List<ScheduleSlotDto> WeekdaysSchedule { get; set; } = new();
    public List<ScheduleSlotDto> SaturdaySchedule { get; set; } = new();
    public List<ScheduleSlotDto> SundaySchedule { get; set; } = new();
    public Dictionary<string, List<BusinessDateAvailabilitySlotDto>> DateSpecificAvailability { get; set; } = new();
    
    // TPay registration fields (optional)
    // Note: These fields are only required if AutoRegisterWithTPay is true AND ParentBusinessProfileId is not specified.
    // When ParentBusinessProfileId is specified, the child business is expected to use the parent's TPay integration,
    // so TPay auto-registration is automatically skipped.
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; } = "PL";
    public string? Regon { get; set; }
    public string? Krs { get; set; }
    public int? LegalForm { get; set; }
    public int? CategoryId { get; set; }
    public string? Mcc { get; set; }
    public string? Website { get; set; }
    public string? WebsiteDescription { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonSurname { get; set; }
    public bool AutoRegisterWithTPay { get; set; } = true;

    // Parent-child relationship (optional)
    // If specified, an association request will be sent to the parent business for confirmation.
    // When parent association is confirmed, the child can use parent's TPay (UseParentTPay) and/or
    // parent's NIP for KSeF invoices (UseParentNipForInvoices) - these permissions are set by the parent.
    public Guid? ParentBusinessProfileId { get; set; }
}

public class UpdateBusinessProfileDto
{
    public required string Nip { get; set; }
    public required string CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AvatarUrl { get; set; }
    public string? TermsAndConditionsUrl { get; set; }

    // Facility plan file (base64 data for upload)
    public string? FacilityPlanUrl { get; set; }
    public string? FacilityPlanFileName { get; set; }
    public string? FacilityPlanFileType { get; set; }
    public List<ScheduleSlotDto> WeekdaysSchedule { get; set; } = new();
    public List<ScheduleSlotDto> SaturdaySchedule { get; set; } = new();
    public List<ScheduleSlotDto> SundaySchedule { get; set; } = new();
    public Dictionary<string, List<BusinessDateAvailabilitySlotDto>> DateSpecificAvailability { get; set; } = new();

    // TPay registration fields (optional)
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; } = "PL";
    public string? Regon { get; set; }
    public string? Krs { get; set; }
    public int? LegalForm { get; set; }
    public int? CategoryId { get; set; }
    public string? Mcc { get; set; }
    public string? Website { get; set; }
    public string? WebsiteDescription { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonSurname { get; set; }

    // Note: Parent-child relationship is managed via BusinessParentChildAssociation
    // and cannot be changed through profile update
}

public class CreateBusinessProfileMultipartDto
{
    // NIP is optional when ParentBusinessProfileId is specified (child uses parent's NIP)
    public string? Nip { get; set; }
    public required string CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AvatarUrl { get; set; }
    
    // Facility plan file upload
    public IFormFile? FacilityPlan { get; set; }
    public string? WeekdaysSchedule { get; set; } // JSON string
    public string? SaturdaySchedule { get; set; } // JSON string
    public string? SundaySchedule { get; set; } // JSON string
    public string? DateSpecificAvailability { get; set; } // JSON string
    
    // TPay registration fields (optional)
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; } = "PL";
    public string? Regon { get; set; }
    public string? Krs { get; set; }
    public int? LegalForm { get; set; }
    public int? CategoryId { get; set; }
    public string? Mcc { get; set; }
    public string? Website { get; set; }
    public string? WebsiteDescription { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonSurname { get; set; }
    public bool AutoRegisterWithTPay { get; set; } = true;

    // Parent-child relationship (optional)
    // If specified, an association request will be sent to the parent business for confirmation.
    public Guid? ParentBusinessProfileId { get; set; }
}

public class UpdateBusinessProfileMultipartDto
{
    public required string Nip { get; set; }
    public required string CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public required string Address { get; set; }
    public required string City { get; set; }
    public required string PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AvatarUrl { get; set; }
    public string? TermsAndConditionsUrl { get; set; }

    // Facility plan file upload
    public IFormFile? FacilityPlan { get; set; }
    public string? WeekdaysSchedule { get; set; } // JSON string
    public string? SaturdaySchedule { get; set; } // JSON string
    public string? SundaySchedule { get; set; } // JSON string
    public string? DateSpecificAvailability { get; set; } // JSON string
    
    // TPay registration fields (optional)
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PhoneCountry { get; set; } = "PL";
    public string? Regon { get; set; }
    public string? Krs { get; set; }
    public int? LegalForm { get; set; }
    public int? CategoryId { get; set; }
    public string? Mcc { get; set; }
    public string? Website { get; set; }
    public string? WebsiteDescription { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonSurname { get; set; }
}

public class FacilityPlanUploadResult
{
    public required string Url { get; set; }
    public required string FileName { get; set; }
    public required string FileType { get; set; }
}

// Agent Dashboard DTOs
public class BusinessProfileWithFacilitiesDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string CompanyName { get; set; }
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public List<FacilityWithTimeSlotsDto> Facilities { get; set; } = new();
    public BusinessScheduleInfoDto ScheduleInfo { get; set; } = new();
}

public class FacilityWithTimeSlotsDto
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
    public required string City { get; set; }
    public GetTimeSlotsResponseDto TimeSlots { get; set; } = new();
}

public class BusinessScheduleInfoDto
{
    public List<ScheduleSlotDto> Weekdays { get; set; } = new();
    public List<ScheduleSlotDto> Saturday { get; set; } = new();
    public List<ScheduleSlotDto> Sunday { get; set; } = new();
    public Dictionary<string, List<BusinessDateAvailabilitySlotDto>> DateSpecificAvailability { get; set; } = new();
}

// Business profile detail with products and facilities (for FE navigation)
public class BusinessProfileDetailDto
{
    public required string Id { get; set; }
    public required string BusinessName { get; set; }
    public string? Nip { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? Description { get; set; }
    public FacilityDto[] Facilities { get; set; } = Array.Empty<FacilityDto>();
    public ProductResponseDto[] Products { get; set; } = Array.Empty<ProductResponseDto>();
}

/// <summary>
/// Public-facing business profile DTO for anonymous access
/// </summary>
public class BusinessProfilePublicDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public List<string> FacilityTypes { get; set; } = new();
}