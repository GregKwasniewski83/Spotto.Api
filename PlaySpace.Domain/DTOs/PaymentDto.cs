using System.Text.Json.Serialization;

namespace PlaySpace.Domain.DTOs
{
    public class PaymentDto
    {
        public Guid? Id { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? Breakdown { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRefunded { get; set; }
        public bool IsConsumed { get; set; } = false;
        
        // Customer information for TPay
        public string? CustomerEmail { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        
        // TPay response fields
        public string? TPayTransactionId { get; set; }
        public string? TPayPaymentUrl { get; set; }
        public string? TPayStatus { get; set; }
        public DateTime? TPayCompletedAt { get; set; }
        public string? TPayErrorMessage { get; set; }
        public string? PaymentMethod { get; set; }
        
        // TPay child transaction IDs (JSON array for marketplace transactions)
        public string? TPayChildTransactionIds { get; set; }
        
        // Refund tracking
        public decimal RefundedAmount { get; set; } = 0;
        public DateTime? RefundedAt { get; set; }
        public string? RefundTransactionId { get; set; }
        
        // URLs for redirects
        public string? ReturnUrl { get; set; }
        public string? ErrorUrl { get; set; }
        
        // Push notification fields
        public string? PushToken { get; set; }
        public string? NotificationId { get; set; }
        
        // Auto-reservation support
        public string? ReservationDetails { get; set; } // JSON serialized FacilityReservationDto
    }

    public class CreatePaymentDto
    {
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public required string Description { get; set; }
        public string? Breakdown { get; set; }
        
        // Facility context for marketplace transactions
        public Guid? FacilityId { get; set; } // Used to determine merchant ID from BusinessProfile
        
        // Merchant information for marketplace transactions (optional override)
        public string? MerchantId { get; set; } // TPay merchant ID from BusinessProfile
        
        // Customer information for TPay
        public required string CustomerEmail { get; set; }
        public required string CustomerName { get; set; }
        public required string CustomerPhone { get; set; }
        
        // URLs for redirects
        public required string ReturnUrl { get; set; }
        public required string ErrorUrl { get; set; }
        
        // Push notification support
        [JsonPropertyName("pushToken")]
        public string? PushToken { get; set; }

        // Auto-reservation details (optional) - supports both singular and plural formats
        [JsonPropertyName("facilityReservation")]
        public FacilityReservationDto? FacilityReservation { get; set; }

        // Frontend sends as array - we'll use the first item
        [JsonPropertyName("facilityReservations")]
        public List<FacilityReservationDto>? FacilityReservations { get; set; }

        // Helper property to get the reservation (from either format)
        [JsonIgnore]
        public FacilityReservationDto? ResolvedFacilityReservation =>
            FacilityReservation ?? FacilityReservations?.FirstOrDefault();
    }

    public class CreateSplitPaymentDto
    {
        public Guid UserId { get; set; }
        public required string Description { get; set; }
        public string? HiddenDescription { get; set; }
        public string Currency { get; set; } = "PLN";
        public string LanguageCode { get; set; } = "PL";
        public string? PreSelectedChannelId { get; set; }
        
        // Customer information for TPay
        public required string CustomerEmail { get; set; }
        public required string CustomerName { get; set; }
        public required string CustomerPhone { get; set; }
        public required string CustomerStreet { get; set; }
        public required string CustomerPostalCode { get; set; }
        public required string CustomerCity { get; set; }
        public string CustomerCountry { get; set; } = "PL";
        public string? CustomerHouseNo { get; set; }
        public string? CustomerFlatNo { get; set; }
        
        // Split payment details
        public List<SplitPaymentItem> PaymentItems { get; set; } = new();
        
        // URLs for redirects
        public required string ReturnUrl { get; set; }
        public required string ErrorUrl { get; set; }
    }

    public class SplitPaymentItem
    {
        public decimal Amount { get; set; }
        public required string Description { get; set; }
        public string? HiddenDescription { get; set; }
        public required string MerchantId { get; set; }
        public List<SplitPaymentProduct> Products { get; set; } = new();
    }

    public class SplitPaymentProduct
    {
        public required string Name { get; set; }
        public required string ExternalId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    // New DTOs for direct TPay marketplace API structure
    public class CreateMarketplaceTransactionDto
    {
        public string Currency { get; set; } = "PLN";
        public required string Description { get; set; }
        public string? HiddenDescription { get; set; }
        public string LanguageCode { get; set; } = "PL";
        public string? PreSelectedChannelId { get; set; }
        public MarketplacePosDto Pos { get; set; } = new() { Id = string.Empty };
        public MarketplaceBillingAddressDto BillingAddress { get; set; } = new() { Email = string.Empty, Name = string.Empty };
        public List<ChildTransactionDto> ChildTransactions { get; set; } = new();

        // Reservation details for slot locking during payment
        [JsonPropertyName("userId")]
        public Guid? UserId { get; set; }

        [JsonPropertyName("facilityReservation")]
        public FacilityReservationDto? FacilityReservation { get; set; }
    }

    public class MarketplacePosDto
    {
        public required string Id { get; set; }
    }

    public class MarketplaceBillingAddressDto
    {
        public required string Email { get; set; }
        public required string Name { get; set; }
        public string? Phone { get; set; }
        public string? Street { get; set; }
        public string? PostalCode { get; set; }
        public string? City { get; set; }
        public string Country { get; set; } = "PL";
        public string? HouseNo { get; set; }
        public string? FlatNo { get; set; }
    }

    public class ChildTransactionDto
    {
        public decimal Amount { get; set; }
        public required string Description { get; set; }
        public string? HiddenDescription { get; set; }
        public MarketplaceMerchantDto Merchant { get; set; } = new() { Id = string.Empty };
        public List<MarketplaceProductDto> Products { get; set; } = new();
    }

    public class MarketplaceMerchantDto
    {
        public required string Id { get; set; }
    }

    public class MarketplaceProductDto
    {
        public required string Name { get; set; }
        public required string ExternalId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class MarketplaceTransactionResponseDto
    {
        public required string TransactionId { get; set; }
        public required string Title { get; set; }
        public required string PaymentUrl { get; set; }
    }

    public class CancelPaymentRequest
    {
        public required Guid UserId { get; set; }
    }
}
