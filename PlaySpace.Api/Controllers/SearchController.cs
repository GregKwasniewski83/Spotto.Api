using Microsoft.AspNetCore.Mvc;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ICityLookupService _cityLookupService;

    public SearchController(ISearchService searchService, ICityLookupService cityLookupService)
    {
        _searchService = searchService;
        _cityLookupService = cityLookupService;
    }

    [HttpGet("businesses")]
    public ActionResult<SearchResponseDto> SearchBusinesses(
        [FromQuery] string? location = null,
        [FromQuery] string? date = null,
        [FromQuery] string? time = null,
        [FromQuery] string? facilityType = null)
    {
        try
        {
            var searchCriteria = new SearchCriteriaDto
            {
                Location = location,
                Date = !string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate) ? parsedDate : null,
                Time = time,
                FacilityType = facilityType
            };

            var results = _searchService.SearchBusinesses(searchCriteria);
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while searching", error = ex.Message });
        }
    }

    [HttpGet("businesses/by-location")]
    public ActionResult<SearchResponseDto> SearchBusinessesByLocation(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radius = 10.0,
        [FromQuery] string? date = null,
        [FromQuery] string? facilityType = null)
    {
        try
        {
            var searchCriteria = new LocationSearchCriteriaDto
            {
                Latitude = latitude,
                Longitude = longitude,
                Radius = radius,
                Date = !string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate) ? parsedDate : null,
                FacilityType = facilityType
            };

            var results = _searchService.SearchBusinessesByLocation(searchCriteria);
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while searching by location", error = ex.Message });
        }
    }

    [HttpGet("cities")]
    public async Task<ActionResult<List<CityDto>>> SearchCities(
        [FromQuery] string query,
        [FromQuery] int limit = 10)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            {
                return BadRequest(new { message = "Query must be at least 3 characters long" });
            }

            if (limit <= 0 || limit > 50)
            {
                return BadRequest(new { message = "Limit must be between 1 and 50" });
            }

            var cities = await _cityLookupService.SearchCitiesAsync(query, limit);
            return Ok(cities);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while searching cities", error = ex.Message });
        }
    }

    [HttpGet("cities/{id}")]
    public async Task<ActionResult<CityDto>> GetCityById(string id)
    {
        try
        {
            var city = await _cityLookupService.GetCityByIdAsync(id);
            if (city == null)
            {
                return NotFound(new { message = "City not found" });
            }

            return Ok(city);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving city", error = ex.Message });
        }
    }

    /// <summary>
    /// Search for potential parent business profiles.
    /// Used when registering a new business that wants to operate under a parent.
    /// </summary>
    [HttpGet("parent-businesses")]
    public ActionResult<List<ParentBusinessSearchResultDto>> SearchParentBusinesses(
        [FromQuery] string? query = null,
        [FromQuery] string? city = null,
        [FromQuery] bool? hasTpay = null,
        [FromQuery] int limit = 20)
    {
        try
        {
            if (limit <= 0 || limit > 50)
            {
                limit = 20;
            }

            var results = _searchService.SearchParentBusinesses(query, city, hasTpay, limit);
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while searching parent businesses", error = ex.Message });
        }
    }
}