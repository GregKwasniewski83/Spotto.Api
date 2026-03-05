using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories
{
    public class GlobalSettingsRepository : IGlobalSettingsRepository
    {
        private readonly PlaySpaceDbContext _context;

        public GlobalSettingsRepository(PlaySpaceDbContext context)
        {
            _context = context;
        }

        public async Task<GlobalSettings?> GetByKeyAsync(string key)
        {
            return await _context.GlobalSettings
                .FirstOrDefaultAsync(s => s.Key == key);
        }

        public async Task<List<GlobalSettings>> GetAllAsync()
        {
            return await _context.GlobalSettings
                .OrderBy(s => s.Key)
                .ToListAsync();
        }

        public async Task<GlobalSettings> CreateAsync(GlobalSettings setting)
        {
            setting.Id = Guid.NewGuid();
            setting.CreatedAt = DateTime.UtcNow;
            setting.UpdatedAt = DateTime.UtcNow;

            _context.GlobalSettings.Add(setting);
            await _context.SaveChangesAsync();
            return setting;
        }

        public async Task<GlobalSettings> UpdateAsync(GlobalSettings setting)
        {
            setting.UpdatedAt = DateTime.UtcNow;
            _context.GlobalSettings.Update(setting);
            await _context.SaveChangesAsync();
            return setting;
        }

        public async Task<bool> DeleteAsync(string key)
        {
            var setting = await GetByKeyAsync(key);
            if (setting == null)
                return false;

            _context.GlobalSettings.Remove(setting);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<GlobalSettings> UpsertAsync(string key, string value, string? description = null)
        {
            var existing = await GetByKeyAsync(key);
            
            if (existing != null)
            {
                existing.Value = value;
                if (description != null)
                    existing.Description = description;
                return await UpdateAsync(existing);
            }
            else
            {
                var newSetting = new GlobalSettings
                {
                    Key = key,
                    Value = value,
                    Description = description
                };
                return await CreateAsync(newSetting);
            }
        }
    }
}