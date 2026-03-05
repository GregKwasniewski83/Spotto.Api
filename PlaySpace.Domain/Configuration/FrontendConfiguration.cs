namespace PlaySpace.Domain.Configuration;

/// <summary>
/// Frontend and mobile app configuration options.
/// </summary>
public class FrontendConfiguration
{
    public string WebAppUrl { get; set; } = string.Empty;
    public string DeepLinkScheme { get; set; } = "spottospace";
}
