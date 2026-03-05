using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IUserFavouriteRepository
{
    Task<UserFavouriteBusinessProfile?> AddFavouriteAsync(Guid userId, Guid businessProfileId);
    Task<bool> RemoveFavouriteAsync(Guid userId, Guid businessProfileId);
    Task<bool> IsFavouriteAsync(Guid userId, Guid businessProfileId);
    Task<List<UserFavouriteBusinessProfile>> GetUserFavouritesAsync(Guid userId);
    Task<int> GetFavouriteCountAsync(Guid businessProfileId);
    Task<HashSet<Guid>> GetUserFavouriteIdsAsync(Guid userId);
}
