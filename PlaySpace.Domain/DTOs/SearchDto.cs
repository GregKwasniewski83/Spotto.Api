namespace PlaySpace.Domain.DTOs;

public class SearchCriteriaDto
{
    public string? Location { get; set; }
    public DateTime? Date { get; set; }
    public string? Time { get; set; }
    public string? FacilityType { get; set; }
}

public class LocationSearchCriteriaDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; } = 10.0; // Default 10km radius
    public DateTime? Date { get; set; }
    public string? FacilityType { get; set; }
}

public class BusinessSearchResultDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AvatarUrl { get; set; }
    
    // Facility plan fields
    public string? FacilityPlanUrl { get; set; }
    public string? FacilityPlanFileName { get; set; }
    public string? FacilityPlanFileType { get; set; }
    
    public int AvailableFacilitiesCount { get; set; }
    public int TotalFacilitiesCount { get; set; }
    public double? Distance { get; set; } // Distance in kilometers for location-based searches
}

public class SearchResponseDto
{
    public List<BusinessSearchResultDto> Results { get; set; } = new();
    public int TotalBusinesses { get; set; }
}

/// <summary>
/// DTO for parent business search results.
/// Used when searching for potential parent businesses during registration.
/// </summary>
public class ParentBusinessSearchResultDto
{
    public Guid Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Nip { get; set; }

    /// <summary>
    /// Indicates if this business has TPay configured.
    /// If true, child businesses can use parent's TPay for payments.
    /// </summary>
    public bool HasTPay { get; set; }
}