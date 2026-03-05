namespace PlaySpace.Domain.Models;

public class PrivacySettings
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool Analytics { get; set; }
    public bool CrashReports { get; set; }
    public bool LocationTracking { get; set; }
    public bool DataSharing { get; set; }
    public bool MarketingEmails { get; set; }
    public bool PushNotifications { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;

    public User User { get; set; } = null!;
}