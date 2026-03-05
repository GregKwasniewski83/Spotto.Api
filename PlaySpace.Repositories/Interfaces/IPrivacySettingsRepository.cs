using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IPrivacySettingsRepository
{
    Task<PrivacySettings?> GetByUserIdAsync(Guid userId);
    Task<PrivacySettings> CreateAsync(PrivacySettings privacySettings);
    Task<PrivacySettings> UpdateAsync(PrivacySettings privacySettings);
    Task DeleteAsync(Guid id);
}