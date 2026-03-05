using PlaySpace.Domain.Models;

public class Facility
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
    public string? Country { get; set; } = "Polska";
    public string? AddressLine2 { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public User? User { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
    public List<FacilityScheduleTemplate> ScheduleTemplates { get; set; } = new();
    public List<FacilityDateAvailability> DateAvailabilities { get; set; } = new();
}

public class FacilityScheduleTemplate
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public ScheduleType ScheduleType { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public Facility? Facility { get; set; }
}

public class FacilityDateAvailability
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public DateTime Date { get; set; }
    public required string Time { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public Facility? Facility { get; set; }
}
