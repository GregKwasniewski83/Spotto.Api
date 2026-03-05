using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class UserFavouriteRepository : IUserFavouriteRepository
{
    private readonly PlaySpaceDbContext _context;

    public UserFavouriteRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<UserFavouriteBusinessProfile?> AddFavouriteAsync(Guid userId, Guid businessProfileId)
    {
        // Check if already favourited
        var existing = await _context.UserFavouriteBusinessProfiles
            .FirstOrDefaultAsync(f => f.UserId == userId && f.BusinessProfileId == businessProfileId);

        if (existing != null)
            return null; // Already favourited

        var favourite = new UserFavouriteBusinessProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessProfileId = businessProfileId,
            AddedAt = DateTime.UtcNow
        };

        _context.UserFavouriteBusinessProfiles.Add(favourite);
        await _context.SaveChangesAsync();

        return favourite;
    }

    public async Task<bool> RemoveFavouriteAsync(Guid userId, Guid businessProfileId)
    {
        var favourite = await _context.UserFavouriteBusinessProfiles
            .FirstOrDefaultAsync(f => f.UserId == userId && f.BusinessProfileId == businessProfileId);

        if (favourite == null)
            return false;

        _context.UserFavouriteBusinessProfiles.Remove(favourite);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> IsFavouriteAsync(Guid userId, Guid businessProfileId)
    {
        return await _context.UserFavouriteBusinessProfiles
            .AnyAsync(f => f.UserId == userId && f.BusinessProfileId == businessProfileId);
    }

    public async Task<List<UserFavouriteBusinessProfile>> GetUserFavouritesAsync(Guid userId)
    {
        return await _context.UserFavouriteBusinessProfiles
            .Include(f => f.BusinessProfile)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.AddedAt)
            .ToListAsync();
    }

    public async Task<int> GetFavouriteCountAsync(Guid businessProfileId)
    {
        return await _context.UserFavouriteBusinessProfiles
            .CountAsync(f => f.BusinessProfileId == businessProfileId);
    }

    public async Task<HashSet<Guid>> GetUserFavouriteIdsAsync(Guid userId)
    {
        var ids = await _context.UserFavouriteBusinessProfiles
            .Where(f => f.UserId == userId)
            .Select(f => f.BusinessProfileId)
            .ToListAsync();

        return ids.ToHashSet();
    }
}
