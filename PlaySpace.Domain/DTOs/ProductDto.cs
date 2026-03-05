namespace PlaySpace.Domain.DTOs;

public class CreateProductDto
{
    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }  // Net price
    public int VatRate { get; set; } = 23;  // VAT rate percentage
    public decimal? GrossPrice { get; set; }  // Gross price (Price + VAT)
    public int Usage { get; set; }
    public required string Period { get; set; }
    public int NumOfPeriods { get; set; }
    public bool PayableInApp { get; set; } = true;
    public bool PayableWithTrainer { get; set; } = false;
    public bool AppliesToAllFacilities { get; set; } = true;
    public List<string>? FacilityIds { get; set; }  // List of facility GUIDs
}

public class UpdateProductDto
{
    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }  // Net price
    public int VatRate { get; set; } = 23;  // VAT rate percentage
    public decimal? GrossPrice { get; set; }  // Gross price (Price + VAT)
    public int Usage { get; set; }
    public required string Period { get; set; }
    public int NumOfPeriods { get; set; }
    public bool PayableInApp { get; set; } = true;
    public bool PayableWithTrainer { get; set; } = false;
    public bool AppliesToAllFacilities { get; set; } = true;
    public List<string>? FacilityIds { get; set; }  // List of facility GUIDs
}

public class ProductResponseDto
{
    public required string Id { get; set; }
    public required string BusinessProfileId { get; set; }
    public string? BusinessName { get; set; }  // Business name for display
    public string? BusinessCity { get; set; }  // Business city for display
    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public required string Description { get; set; }
    public decimal Price { get; set; }  // Net price
    public int VatRate { get; set; }  // VAT rate percentage
    public decimal? GrossPrice { get; set; }  // Gross price (Price + VAT)
    public int Usage { get; set; }
    public required string Period { get; set; }
    public int NumOfPeriods { get; set; }
    public bool PayableInApp { get; set; }
    public bool PayableWithTrainer { get; set; }
    public required string StartDate { get; set; }
    public required string EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool AppliesToAllFacilities { get; set; }
    public List<string>? FacilityIds { get; set; }
    public required string CreatedAt { get; set; }
    public required string UpdatedAt { get; set; }

    // Deep linking URLs for sharing
    public string? PublicLinkUrl { get; set; }  // Web app URL for sharing
    public string? DeepLinkUrl { get; set; }    // Mobile app deep link
}

public class ProductSearchDto
{
    // Search & Text Filtering
    public string? Search { get; set; }

    // Business Filtering
    public string? BusinessName { get; set; }
    public string? City { get; set; }  // Filter by business city

    // Price Filtering
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    // Product Attributes
    public string? Period { get; set; }  // Days|Weeks|Months|Years|Lifetime
    public int? MinUsage { get; set; }
    public int? MaxUsage { get; set; }

    // Sorting
    public string? SortBy { get; set; }  // price|createdAt|title|usage
    public string? SortOrder { get; set; }  // asc|desc (default: asc)

    // Pagination
    public int? Page { get; set; }  // default: 1
    public int? Limit { get; set; }  // default: 20, max: 100
    public int? Offset { get; set; }
}

public class ProductSearchResponseDto
{
    public List<ProductResponseDto> Products { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public int TotalPages { get; set; }
}
