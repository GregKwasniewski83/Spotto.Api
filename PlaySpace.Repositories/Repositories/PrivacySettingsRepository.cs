using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories;

public class PrivacySettingsRepository : IPrivacySettingsRepository
{
    private readonly PlaySpaceDbContext _context;

    public PrivacySettingsRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<PrivacySettings?> GetByUserIdAsync(Guid userId)
    {
        return await _context.PrivacySettings
            .FirstOrDefaultAsync(ps => ps.UserId == userId);
    }

    public async Task<PrivacySettings> CreateAsync(PrivacySettings privacySettings)
    {
        _context.PrivacySettings.Add(privacySettings);
        await _context.SaveChangesAsync();
        return privacySettings;
    }

    public async Task<PrivacySettings> UpdateAsync(PrivacySettings privacySettings)
    {
        _context.PrivacySettings.Update(privacySettings);
        await _context.SaveChangesAsync();
        return privacySettings;
    }

    public async Task DeleteAsync(Guid id)
    {
        var privacySettings = await _context.PrivacySettings.FindAsync(id);
        if (privacySettings != null)
        {
            _context.PrivacySettings.Remove(privacySettings);
            await _context.SaveChangesAsync();
        }
    }
}