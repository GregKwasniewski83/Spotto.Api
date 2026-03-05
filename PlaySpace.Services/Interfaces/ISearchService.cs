using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface ISearchService
{
    SearchResponseDto SearchBusinesses(SearchCriteriaDto criteria);
    SearchResponseDto SearchBusinessesByLocation(LocationSearchCriteriaDto criteria);

    /// <summary>
    /// Search for potential parent business profiles.
    /// Used when registering a new business that wants to operate under a parent.
    /// </summary>
    List<ParentBusinessSearchResultDto> SearchParentBusinesses(string? query, string? city, bool? hasTpay, int limit);
}