using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public class BusinessProfile
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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // TPay registration fields
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
    
    // TPay merchant data (after registration)
    public string? TPayMerchantId { get; set; }
    public string? TPayAccountId { get; set; }
    public string? TPayPosId { get; set; }
    public string? TPayActivationLink { get; set; }
    public int? TPayVerificationStatus { get; set; }
    public DateTime? TPayRegisteredAt { get; set; }

    // KSeF (Polish e-Invoice System) integration
    public bool KSeFEnabled { get; set; } = false;
    public string? KSeFToken { get; set; }
    public string KSeFEnvironment { get; set; } = "Test"; // "Test" or "Production"
    public DateTime? KSeFRegisteredAt { get; set; }
    public DateTime? KSeFLastSyncAt { get; set; }

    // Parent-child business profile relationship
    // Allows child businesses to operate under a parent's TPay and/or NIP for invoicing
    public Guid? ParentBusinessProfileId { get; set; }
    public BusinessProfile? ParentBusinessProfile { get; set; }
    public List<BusinessProfile> ChildBusinessProfiles { get; set; } = new();

    /// <summary>
    /// If true, this business uses the parent's TPay integration for payments
    /// </summary>
    public bool UseParentTPay { get; set; } = false;

    /// <summary>
    /// If true, this business uses the parent's NIP and company details for KSeF invoices
    /// </summary>
    public bool UseParentNipForInvoices { get; set; } = false;

    public User? User { get; set; }
    public List<BusinessScheduleTemplate> ScheduleTemplates { get; set; } = new();
    public List<BusinessDateAvailability> DateAvailabilities { get; set; } = new();
    public List<Product> Products { get; set; } = new();
}

public enum ScheduleType
{
    Weekdays = 0,
    Saturday = 1, 
    Sunday = 2
}

public class BusinessScheduleTemplate
{
    public Guid Id { get; set; }
    public Guid BusinessProfileId { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public BusinessProfile? BusinessProfile { get; set; }
}