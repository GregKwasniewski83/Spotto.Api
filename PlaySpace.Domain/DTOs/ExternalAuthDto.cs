namespace PlaySpace.Domain.DTOs;

public class ExternalLoginRequest
{
    public required string IdToken { get; set; }
    public required string Provider { get; set; } // "google" or "apple"
    public DeviceInfo? DeviceInfo { get; set; }
}

public class DeviceInfo
{
    public string? DeviceId { get; set; }
    public string? Platform { get; set; } // "ios" or "android"
    public string? AppVersion { get; set; }
}

public class LinkExternalAccountRequest
{
    public required string IdToken { get; set; }
    public required string Provider { get; set; }
}

public class ExternalAuthDto
{
    public Guid Id { get; set; }
    public required string Provider { get; set; }
    public required string ExternalUserId { get; set; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExternalUserInfo
{
    public required string ExternalUserId { get; set; }
    public required string Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public bool EmailVerified { get; set; } = false;
    public string? AvatarUrl { get; set; }
}