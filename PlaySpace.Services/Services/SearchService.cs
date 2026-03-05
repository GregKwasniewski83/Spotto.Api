using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class SearchService : ISearchService
{
    private readonly ISearchRepository _searchRepository;

    public SearchService(ISearchRepository searchRepository)
    {
        _searchRepository = searchRepository;
    }

    public SearchResponseDto SearchBusinesses(SearchCriteriaDto criteria)
    {
        return _searchRepository.SearchBusinesses(criteria);
    }

    public SearchResponseDto SearchBusinessesByLocation(LocationSearchCriteriaDto criteria)
    {
        return _searchRepository.SearchBusinessesByLocation(criteria);
    }

    public List<ParentBusinessSearchResultDto> SearchParentBusinesses(string? query, string? city, bool? hasTpay, int limit)
    {
        return _searchRepository.SearchParentBusinesses(query, city, hasTpay, limit);
    }
}