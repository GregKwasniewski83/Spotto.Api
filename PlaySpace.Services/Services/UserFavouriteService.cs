using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class UserFavouriteService : IUserFavouriteService
{
    private readonly IUserFavouriteRepository _favouriteRepository;
    private readonly IBusinessProfileService _businessProfileService;

    public UserFavouriteService(
        IUserFavouriteRepository favouriteRepository,
        IBusinessProfileService businessProfileService)
    {
        _favouriteRepository = favouriteRepository;
        _businessProfileService = businessProfileService;
    }

    public async Task<bool> ToggleFavouriteAsync(Guid userId, Guid businessProfileId)
    {
        var isFavourite = await _favouriteRepository.IsFavouriteAsync(userId, businessProfileId);

        if (isFavourite)
        {
            await _favouriteRepository.RemoveFavouriteAsync(userId, businessProfileId);
            return false; // Removed from favourites
        }
        else
        {
            await _favouriteRepository.AddFavouriteAsync(userId, businessProfileId);
            return true; // Added to favourites
        }
    }

    public async Task<bool> IsFavouriteAsync(Guid userId, Guid businessProfileId)
    {
        return await _favouriteRepository.IsFavouriteAsync(userId, businessProfileId);
    }

    public async Task<List<BusinessProfileDto>> GetUserFavouritesAsync(Guid userId)
    {
        var favourites = await _favouriteRepository.GetUserFavouritesAsync(userId);
        var result = new List<BusinessProfileDto>();

        foreach (var favourite in favourites)
        {
            var profile = _businessProfileService.GetBusinessProfileById(favourite.BusinessProfileId);
            if (profile != null)
            {
                profile.IsFavouritedByCurrentUser = true;
                result.Add(profile);
            }
        }

        return result;
    }

    public async Task<HashSet<Guid>> GetUserFavouriteIdsAsync(Guid userId)
    {
        return await _favouriteRepository.GetUserFavouriteIdsAsync(userId);
    }
}
