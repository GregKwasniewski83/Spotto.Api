namespace PlaySpace.Domain.Models
{
    public class GlobalSettings
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // Static class for commonly used settings keys
    public static class SettingsKeys
    {
        public const string RefundFeePercentage = "refund_fee_percentage";
        public const string MaxRefundDaysAdvance = "max_refund_days_advance";
        public const string EnableRefunds = "enable_refunds";
    }
}