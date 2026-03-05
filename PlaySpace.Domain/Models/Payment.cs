namespace PlaySpace.Domain.Models
{
    public class Payment
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal Amount { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? Breakdown { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsRefunded { get; set; }
        public bool IsConsumed { get; set; } = false;

        // TPay integration fields
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

        // Push notification support
        public string? PushToken { get; set; }
        public string? NotificationId { get; set; }

        // Auto-reservation support
        public string? ReservationDetails { get; set; } // JSON serialized FacilityReservationDto

        // Product purchase support
        public string? ProductDetails { get; set; } // JSON serialized ProductPurchaseDetails

        // Context references for KSeF invoicing (allows invoice creation even without ReservationDetails/ProductDetails)
        public Guid? FacilityId { get; set; } // For facility reservation payments
        public Guid? TrainingId { get; set; } // For training payments
        public Guid? ProductId { get; set; } // For product purchase payments
    }
}
