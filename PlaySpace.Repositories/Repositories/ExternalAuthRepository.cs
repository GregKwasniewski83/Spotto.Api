using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class ExternalAuthRepository : IExternalAuthRepository
{
    private readonly PlaySpaceDbContext _context;

    public ExternalAuthRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<ExternalAuth?> GetByProviderAndExternalUserIdAsync(AuthProvider provider, string externalUserId)
    {
        return await _context.ExternalAuths
            .Include(ea => ea.User)
            .FirstOrDefaultAsync(ea => ea.Provider == provider && ea.ExternalUserId == externalUserId);
    }

    public async Task<ExternalAuth?> GetByProviderAndEmailAsync(AuthProvider provider, string email)
    {
        return await _context.ExternalAuths
            .Include(ea => ea.User)
            .FirstOrDefaultAsync(ea => ea.Provider == provider && ea.Email == email);
    }

    public async Task<List<ExternalAuth>> GetByUserIdAsync(Guid userId)
    {
        return await _context.ExternalAuths
            .Where(ea => ea.UserId == userId)
            .ToListAsync();
    }

    public async Task<ExternalAuth> CreateAsync(ExternalAuth externalAuth)
    {
        externalAuth.Id = Guid.NewGuid();
        externalAuth.CreatedAt = DateTime.UtcNow;
        externalAuth.UpdatedAt = DateTime.UtcNow;

        _context.ExternalAuths.Add(externalAuth);
        await _context.SaveChangesAsync();
        
        return externalAuth;
    }

    public async Task<ExternalAuth?> UpdateAsync(ExternalAuth externalAuth)
    {
        var existing = await _context.ExternalAuths.FindAsync(externalAuth.Id);
        if (existing == null) return null;

        existing.Email = externalAuth.Email;
        existing.DisplayName = externalAuth.DisplayName;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var externalAuth = await _context.ExternalAuths.FindAsync(id);
        if (externalAuth == null) return false;

        _context.ExternalAuths.Remove(externalAuth);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteByUserIdAndProviderAsync(Guid userId, AuthProvider provider)
    {
        var externalAuths = await _context.ExternalAuths
            .Where(ea => ea.UserId == userId && ea.Provider == provider)
            .ToListAsync();

        if (!externalAuths.Any()) return false;

        _context.ExternalAuths.RemoveRange(externalAuths);
        await _context.SaveChangesAsync();
        return true;
    }
}