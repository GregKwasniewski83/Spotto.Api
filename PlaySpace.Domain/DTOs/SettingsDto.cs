namespace PlaySpace.Domain.DTOs
{
    public class GlobalSettingsDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateSettingDto
    {
        public required string Key { get; set; }
        public required string Value { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateSettingDto
    {
        public required string Value { get; set; }
        public string? Description { get; set; }
    }

    public class RefundSettingsDto
    {
        public decimal RefundFeePercentage { get; set; }
        public int MaxRefundDaysAdvance { get; set; }
        public bool EnableRefunds { get; set; }
    }
}