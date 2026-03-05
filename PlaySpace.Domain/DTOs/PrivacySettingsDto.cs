using System.ComponentModel.DataAnnotations;

namespace PlaySpace.Domain.DTOs;

public class PrivacySettingsDto
{
    public bool Analytics { get; set; }
    public bool CrashReports { get; set; }
    public bool LocationTracking { get; set; }
    public bool DataSharing { get; set; }
    public bool MarketingEmails { get; set; }
    public bool PushNotifications { get; set; }
}

public class PrivacySettingsResponseDto
{
    public Guid UserId { get; set; }
    public PrivacySettingsDto Settings { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}

public class PrivacySettingsUpdateRequestDto
{
    public bool? Analytics { get; set; }
    public bool? CrashReports { get; set; }
    public bool? LocationTracking { get; set; }
    public bool? DataSharing { get; set; }
    public bool? MarketingEmails { get; set; }
    public bool? PushNotifications { get; set; }

    public bool HasAnyUpdate()
    {
        return Analytics.HasValue ||
               CrashReports.HasValue ||
               LocationTracking.HasValue ||
               DataSharing.HasValue ||
               MarketingEmails.HasValue ||
               PushNotifications.HasValue;
    }
}

public class PrivacySettingUpdateDto
{
    [Required]
    public bool Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}