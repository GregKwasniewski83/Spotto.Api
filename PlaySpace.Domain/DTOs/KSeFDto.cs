namespace PlaySpace.Domain.DTOs;

/// <summary>
/// DTO for configuring KSeF credentials for a business
/// </summary>
public class ConfigureKSeFDto
{
    public required string Token { get; set; }
    public required string Environment { get; set; } // "Test" or "Production"
}

/// <summary>
/// DTO for enabling/disabling KSeF integration
/// </summary>
public class UpdateKSeFStatusDto
{
    public bool Enabled { get; set; }
}

/// <summary>
/// Response DTO for KSeF configuration status
/// </summary>
public class KSeFConfigurationDto
{
    public bool IsConfigured { get; set; }
    public bool IsEnabled { get; set; }
    public string? Environment { get; set; }
    public DateTime? RegisteredAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? StatusMessage { get; set; }
}

/// <summary>
/// DTO for testing KSeF connection
/// </summary>
public class KSeFConnectionTestDto
{
    public bool IsSuccessful { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; }
}
