namespace PlaySpace.Domain.Models;

public class ProductUsageLog
{
    public Guid Id { get; set; }
    public Guid ProductPurchaseId { get; set; }
    public Guid UserId { get; set; }
    public Guid? FacilityId { get; set; }

    public DateTime UsageDate { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public ProductPurchase? ProductPurchase { get; set; }
    public User? User { get; set; }
    public Facility? Facility { get; set; }
}
