public class UserPreferences
{
    public Guid Id { get; set; }
    public List<string> ActivityPreferences { get; set; }
    public bool ReceiveNotifications { get; set; }
    public string PrivacyLevel { get; set; }
    // ...other fields...
}
