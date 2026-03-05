namespace PlaySpace.Domain.Configuration;

/// <summary>
/// Platform-wide KSeF configuration options.
/// Individual businesses configure their own credentials (NIP, Token, Environment) in their BusinessProfile.
/// </summary>
public class KSeFOptions
{
    public string TestApiUrl { get; set; } = "https://ksef-test.mf.gov.pl/api/v2/";
    public string ProductionApiUrl { get; set; } = "https://ksef.mf.gov.pl/api/v2/";
    public string PublicKeyUrl { get; set; } = "https://ksef-test.mf.gov.pl/api/v2/security/public-key-certificates";
    public bool EnableAutoInvoicing { get; set; } = true;
    public string InvoicePrefix { get; set; } = "FA";
    public int DefaultVATRate { get; set; } = 23;
    public int SessionExpirationMinutes { get; set; } = 60;
}
