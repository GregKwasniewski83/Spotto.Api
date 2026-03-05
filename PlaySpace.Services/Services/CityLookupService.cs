using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Text.Json;

namespace PlaySpace.Services.Services;

public class CityLookupService : ICityLookupService
{
    private readonly List<CityDto> _cities;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public CityLookupService(string dataFilePath)
    {
        _cities = LoadCitiesFromFile(dataFilePath);
    }

    public async Task<List<CityDto>> SearchCitiesAsync(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            return new List<CityDto>();

        await _semaphore.WaitAsync();
        try
        {
            var results = _cities
                .Where(c => (
                            c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            c.Province.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1) // Prioritize starts with
                .ThenBy(c => c.Name)
                .Take(limit)
                .ToList();

            return results;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<CityDto?> GetCityByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        await _semaphore.WaitAsync();
        try
        {
            return _cities.FirstOrDefault(c => c.Id == id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private List<CityDto> LoadCitiesFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Cities data file not found at: {filePath}");
            }

            var json = File.ReadAllText(filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var cities = JsonSerializer.Deserialize<List<CityDto>>(json, options);
            return cities ?? new List<CityDto>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load cities data: {ex.Message}", ex);
        }
    }
}