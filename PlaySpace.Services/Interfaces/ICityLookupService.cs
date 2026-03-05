using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface ICityLookupService
{
    Task<List<CityDto>> SearchCitiesAsync(string query, int limit = 10);
    Task<CityDto?> GetCityByIdAsync(string id);
}