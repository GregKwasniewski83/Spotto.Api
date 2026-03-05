using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IUserFavouriteService
{
    /// <summary>
    /// Toggle favourite status for a business profile.
    /// Returns true if added to favourites, false if removed.
    /// </summary>
    Task<bool> ToggleFavouriteAsync(Guid userId, Guid businessProfileId);

    /// <summary>
    /// Check if a business profile is favourited by the user.
    /// </summary>
    Task<bool> IsFavouriteAsync(Guid userId, Guid businessProfileId);

    /// <summary>
    /// Get all business profiles favourited by the user.
    /// </summary>
    Task<List<BusinessProfileDto>> GetUserFavouritesAsync(Guid userId);

    /// <summary>
    /// Get the favourite IDs for a user (for batch checking).
    /// </summary>
    Task<HashSet<Guid>> GetUserFavouriteIdsAsync(Guid userId);
}
