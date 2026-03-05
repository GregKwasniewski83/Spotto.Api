using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface ISearchRepository
{
    SearchResponseDto SearchBusinesses(SearchCriteriaDto criteria);
    SearchResponseDto SearchBusinessesByLocation(LocationSearchCriteriaDto criteria);
    List<ParentBusinessSearchResultDto> SearchParentBusinesses(string? query, string? city, bool? hasTpay, int limit);
}