using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IPrivacySettingsService
{
    Task<PrivacySettingsResponseDto?> GetPrivacySettingsAsync(Guid userId);
    Task<PrivacySettingsResponseDto> CreateDefaultSettingsAsync(Guid userId);
    Task<PrivacySettingsResponseDto> CreatePrivacySettingsAsync(Guid userId, PrivacySettingsUpdateRequestDto request);
    Task<PrivacySettingsResponseDto> UpdatePrivacySettingsAsync(Guid userId, PrivacySettingsUpdateRequestDto request);
    Task<PrivacySettingsResponseDto?> UpdateSpecificSettingAsync(Guid userId, string settingName, bool value);
    Task<PrivacySettingsResponseDto> ResetToDefaultAsync(Guid userId);
}