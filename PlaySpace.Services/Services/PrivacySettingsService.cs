using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class PrivacySettingsService : IPrivacySettingsService
{
    private readonly IPrivacySettingsRepository _privacySettingsRepository;

    public PrivacySettingsService(IPrivacySettingsRepository privacySettingsRepository)
    {
        _privacySettingsRepository = privacySettingsRepository;
    }

    public async Task<PrivacySettingsResponseDto?> GetPrivacySettingsAsync(Guid userId)
    {
        var settings = await _privacySettingsRepository.GetByUserIdAsync(userId);
        return settings == null ? null : MapToResponseDto(settings);
    }

    public async Task<PrivacySettingsResponseDto> CreateDefaultSettingsAsync(Guid userId)
    {
        var defaultSettings = new PrivacySettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Analytics = false,
            CrashReports = true,
            LocationTracking = false,
            DataSharing = false,
            MarketingEmails = false,
            PushNotifications = true,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };

        var created = await _privacySettingsRepository.CreateAsync(defaultSettings);
        return MapToResponseDto(created);
    }

    public async Task<PrivacySettingsResponseDto> CreatePrivacySettingsAsync(Guid userId, PrivacySettingsUpdateRequestDto request)
    {
        // Check if privacy settings already exist
        var existingSettings = await _privacySettingsRepository.GetByUserIdAsync(userId);
        if (existingSettings != null)
        {
            throw new InvalidOperationException("Privacy settings already exist for this user");
        }

        // Create privacy settings with user's choices, using defaults for unspecified values
        var newSettings = new PrivacySettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Analytics = request.Analytics ?? false,
            CrashReports = request.CrashReports ?? true,
            LocationTracking = request.LocationTracking ?? false,
            DataSharing = request.DataSharing ?? false,
            MarketingEmails = request.MarketingEmails ?? false,
            PushNotifications = request.PushNotifications ?? true,
            UpdatedAt = DateTime.UtcNow,
            Version = 1
        };

        var created = await _privacySettingsRepository.CreateAsync(newSettings);
        return MapToResponseDto(created);
    }

    public async Task<PrivacySettingsResponseDto> UpdatePrivacySettingsAsync(Guid userId, PrivacySettingsUpdateRequestDto request)
    {
        var settings = await _privacySettingsRepository.GetByUserIdAsync(userId);

        if (settings == null)
        {
            await CreateDefaultSettingsAsync(userId);
            settings = await _privacySettingsRepository.GetByUserIdAsync(userId);
        }

        if (request.Analytics.HasValue)
            settings.Analytics = request.Analytics.Value;
        
        if (request.CrashReports.HasValue)
            settings.CrashReports = request.CrashReports.Value;
        
        if (request.LocationTracking.HasValue)
            settings.LocationTracking = request.LocationTracking.Value;
        
        if (request.DataSharing.HasValue)
            settings.DataSharing = request.DataSharing.Value;
        
        if (request.MarketingEmails.HasValue)
            settings.MarketingEmails = request.MarketingEmails.Value;
        
        if (request.PushNotifications.HasValue)
            settings.PushNotifications = request.PushNotifications.Value;

        settings.UpdatedAt = DateTime.UtcNow;
        settings.Version++;

        var updated = await _privacySettingsRepository.UpdateAsync(settings);
        return MapToResponseDto(updated);
    }

    public async Task<PrivacySettingsResponseDto?> UpdateSpecificSettingAsync(Guid userId, string settingName, bool value)
    {
        var settings = await _privacySettingsRepository.GetByUserIdAsync(userId);

        if (settings == null)
        {
            await CreateDefaultSettingsAsync(userId);
            settings = await _privacySettingsRepository.GetByUserIdAsync(userId);
        }

        switch (settingName.ToLowerInvariant())
        {
            case "analytics":
                settings.Analytics = value;
                break;
            case "crashreports":
                settings.CrashReports = value;
                break;
            case "locationtracking":
                settings.LocationTracking = value;
                break;
            case "datasharing":
                settings.DataSharing = value;
                break;
            case "marketingemails":
                settings.MarketingEmails = value;
                break;
            case "pushnotifications":
                settings.PushNotifications = value;
                break;
            default:
                throw new ArgumentException($"Invalid setting name: {settingName}");
        }

        settings.UpdatedAt = DateTime.UtcNow;
        settings.Version++;

        var updated = await _privacySettingsRepository.UpdateAsync(settings);
        return MapToResponseDto(updated);
    }

    public async Task<PrivacySettingsResponseDto> ResetToDefaultAsync(Guid userId)
    {
        var settings = await _privacySettingsRepository.GetByUserIdAsync(userId);

        if (settings == null)
        {
            return await CreateDefaultSettingsAsync(userId);
        }

        settings.Analytics = false;
        settings.CrashReports = true;
        settings.LocationTracking = false;
        settings.DataSharing = false;
        settings.MarketingEmails = false;
        settings.PushNotifications = true;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.Version++;

        var updated = await _privacySettingsRepository.UpdateAsync(settings);
        return MapToResponseDto(updated);
    }

    private static PrivacySettingsResponseDto MapToResponseDto(PrivacySettings entity)
    {
        return new PrivacySettingsResponseDto
        {
            UserId = entity.UserId,
            Settings = new PrivacySettingsDto
            {
                Analytics = entity.Analytics,
                CrashReports = entity.CrashReports,
                LocationTracking = entity.LocationTracking,
                DataSharing = entity.DataSharing,
                MarketingEmails = entity.MarketingEmails,
                PushNotifications = entity.PushNotifications
            },
            UpdatedAt = entity.UpdatedAt,
            Version = entity.Version
        };
    }
}