namespace PlaySpace.Domain.DTOs;

// Request to initiate purchase
public class CreateProductPurchaseDto
{
    public required string ProductId { get; set; }
    public string? PaymentMethodId { get; set; }
    public int Quantity { get; set; } = 1;

    // Payment customer info
    public required string CustomerEmail { get; set; }
    public required string CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? PushToken { get; set; }
}

// Purchase response
public class ProductPurchaseResponseDto
{
    public required string PurchaseId { get; set; }
    public required string ProductId { get; set; }
    public required string UserId { get; set; }
    public required string PurchaseDate { get; set; }
    public required string ExpiryDate { get; set; }
    public int RemainingUsage { get; set; }
    public int TotalUsage { get; set; }
    public decimal Price { get; set; }
    public decimal? GrossPrice { get; set; }
    public required string PaymentId { get; set; }
    public required string Status { get; set; }

    // Product snapshot info
    public string? ProductTitle { get; set; }
    public string? ProductDescription { get; set; }
    public bool PayableInApp { get; set; }
    public bool PayableWithTrainer { get; set; }
    public string? BusinessName { get; set; }
    public required string BusinessProfileId { get; set; }

    // Facility restrictions
    public bool AppliesToAllFacilities { get; set; }
    public List<string>? FacilityIds { get; set; }
}

// Use product request
public class UseProductDto
{
    public string? FacilityId { get; set; }
    public string? Notes { get; set; }
}

// Use product response
public class ProductUsageResponseDto
{
    public required string PurchaseId { get; set; }
    public int RemainingUsage { get; set; }
    public required string UsageDate { get; set; }
    public required string Status { get; set; }
}

// User's purchases list
public class UserProductsResponseDto
{
    public ProductPurchaseDetailDto[] Purchases { get; set; } = Array.Empty<ProductPurchaseDetailDto>();
}

public class ProductPurchaseDetailDto
{
    public required string PurchaseId { get; set; }
    public required string ProductId { get; set; }
    public required string ProductTitle { get; set; }
    public string? ProductSubtitle { get; set; }
    public required string ProductDescription { get; set; }
    public required string BusinessName { get; set; }
    public required string BusinessProfileId { get; set; }
    public required string PurchaseDate { get; set; }
    public required string ExpiryDate { get; set; }
    public int RemainingUsage { get; set; }
    public int TotalUsage { get; set; }
    public decimal Price { get; set; }
    public decimal? GrossPrice { get; set; }
    public bool PayableInApp { get; set; }
    public bool PayableWithTrainer { get; set; }
    public required string Status { get; set; }

    // Facility restrictions
    public bool AppliesToAllFacilities { get; set; }
    public List<string>? FacilityIds { get; set; }
}

// For webhook processing
public class ProductPurchaseDetails
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

// Agent - Filter criteria for product purchases
public class AgentProductPurchaseFilterDto
{
    public string? Status { get; set; }  // 'active', 'depleted', 'expired', or null for all
    public DateTime? ExpiryDateFrom { get; set; }
    public DateTime? ExpiryDateTo { get; set; }
    public DateTime? PurchaseDateFrom { get; set; }
    public DateTime? PurchaseDateTo { get; set; }
    public string? CustomerEmail { get; set; }  // partial match
    public string? CustomerName { get; set; }   // partial match
    public Guid? ProductId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// Agent - Product purchase response with customer info
public class AgentProductPurchaseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductSubtitle { get; set; }

    // Customer info
    public Guid UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }

    // Purchase details
    public DateTime PurchaseDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public int TotalUsage { get; set; }
    public int RemainingUsage { get; set; }
    public decimal Price { get; set; }
    public decimal? GrossPrice { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Agent - Paginated list of product purchases
public class AgentProductPurchaseListDto
{
    public List<AgentProductPurchaseDto> Purchases { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

// Agent - Extend product purchase expiry request
public class ExtendProductPurchaseDto
{
    public DateTime NewExpiryDate { get; set; }
    public string? Notes { get; set; }  // Optional reason for extension
}
