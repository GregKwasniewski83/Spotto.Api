namespace PlaySpace.Domain.Models;

public class ProductPurchase
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentId { get; set; }

    public DateTime PurchaseDate { get; set; }
    public DateTime ExpiryDate { get; set; }

    // Usage tracking
    public int TotalUsage { get; set; }        // Snapshot from Product.Usage at purchase
    public int RemainingUsage { get; set; }    // Decrements on each use
    public decimal Price { get; set; }         // Amount paid (gross price, same as payment amount)
    public decimal? GrossPrice { get; set; }   // Snapshot from Product.GrossPrice
    public int VatRate { get; set; } = 23;     // Snapshot from Product.VatRate

    // Product details snapshot (what was purchased)
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductSubtitle { get; set; }
    public string ProductDescription { get; set; } = string.Empty;
    public ProductPeriod ProductPeriod { get; set; }
    public int ProductNumOfPeriods { get; set; }
    public bool ProductPayableInApp { get; set; }  // Snapshot from Product.PayableInApp
    public bool ProductPayableWithTrainer { get; set; }  // Snapshot from Product.PayableWithTrainer
    public string BusinessName { get; set; } = string.Empty;  // Business name at purchase time
    public Guid BusinessProfileId { get; set; }  // Business profile ID at purchase time

    // Facility restrictions snapshot
    public bool AppliesToAllFacilities { get; set; } = true;
    public string? FacilityIds { get; set; }  // JSON array of Guid strings

    public string Status { get; set; } = "active";  // 'active', 'depleted', 'expired'

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Product? Product { get; set; }
    public User? User { get; set; }
    public Payment? Payment { get; set; }
    public List<ProductUsageLog> UsageLogs { get; set; } = new();
}
