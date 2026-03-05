namespace PlaySpace.Domain.Models;

public enum ProductPeriod
{
    Days = 0,      // Measured in days
    Weeks = 1,     // Measured in weeks
    Months = 2,    // Measured in months
    Years = 3,     // Measured in years
    Lifetime = 4   // No expiration
}

public class Product
{
    public Guid Id { get; set; }
    public Guid BusinessProfileId { get; set; }
    public Guid? UserId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }  // Net price
    public int VatRate { get; set; } = 23;  // VAT rate percentage (default 23%)
    public decimal? GrossPrice { get; set; }  // Gross price (Price + VAT)

    public int Usage { get; set; }  // Total number of uses (e.g., 10 sessions)
    public ProductPeriod Period { get; set; }
    public int NumOfPeriods { get; set; }
    public bool PayableInApp { get; set; } = true;
    public bool PayableWithTrainer { get; set; } = false;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Facility restrictions
    public bool AppliesToAllFacilities { get; set; } = true;
    public string? FacilityIds { get; set; }  // JSON array of Guid strings, null if AppliesToAllFacilities is true

    // Navigation properties
    public BusinessProfile? BusinessProfile { get; set; }
    public User? User { get; set; }
}
