namespace PlaySpace.Domain.Models;

public class KSeFInvoice
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid BusinessProfileId { get; set; }
    public Guid? UserId { get; set; } // Null for guest purchases

    // Invoice identification
    public string? KSeFReferenceNumber { get; set; } // Reference number from KSeF after successful submission
    public string InvoiceNumber { get; set; } = string.Empty; // Internal invoice number (FA/...)
    public DateTime IssueDate { get; set; }

    // Seller (Business)
    public string SellerNIP { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string SellerAddress { get; set; } = string.Empty;
    public string SellerCity { get; set; } = string.Empty;
    public string SellerPostalCode { get; set; } = string.Empty;

    // Buyer (Customer)
    public string? BuyerNIP { get; set; } // Optional for individual customers
    public string BuyerName { get; set; } = string.Empty;
    public string? BuyerAddress { get; set; }
    public string? BuyerCity { get; set; }
    public string? BuyerPostalCode { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerPhone { get; set; }

    // Invoice details
    public decimal NetAmount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public int VATRate { get; set; } = 23; // Default 23% VAT for Poland

    // Invoice items (JSON serialized)
    public string InvoiceItems { get; set; } = string.Empty; // List of line items as JSON

    // KSeF Status
    public string Status { get; set; } = "Pending"; // Pending, Sent, Accepted, Rejected, Error
    public string? KSeFStatus { get; set; } // Status from KSeF API
    public string? KSeFErrorMessage { get; set; }
    public DateTime? KSeFSentAt { get; set; }
    public DateTime? KSeFAcceptedAt { get; set; }

    // Invoice XML/JSON
    public string? InvoiceXML { get; set; } // Generated FA XML for KSeF
    public string? InvoiceJSON { get; set; } // Optional JSON representation

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public Payment? Payment { get; set; }
    public Reservation? Reservation { get; set; }
    public BusinessProfile? BusinessProfile { get; set; }
    public User? User { get; set; }
}

public class KSeFInvoiceItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "szt"; // Default: pieces
    public decimal UnitPrice { get; set; }
    public decimal NetAmount { get; set; }
    public int VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal GrossAmount { get; set; }
}
