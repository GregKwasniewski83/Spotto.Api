using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces
{
    public interface IGlobalSettingsRepository
    {
        Task<GlobalSettings?> GetByKeyAsync(string key);
        Task<List<GlobalSettings>> GetAllAsync();
        Task<GlobalSettings> CreateAsync(GlobalSettings setting);
        Task<GlobalSettings> UpdateAsync(GlobalSettings setting);
        Task<bool> DeleteAsync(string key);
        Task<GlobalSettings> UpsertAsync(string key, string value, string? description = null);
    }
}