using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Services.Interfaces;
using PlaySpace.Domain.Attributes;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacilityController : ControllerBase
{
    private readonly IFacilityService _facilityService;
    private readonly ITimeSlotService _timeSlotService;

    public FacilityController(IFacilityService facilityService, ITimeSlotService timeSlotService)
    {
        _facilityService = facilityService;
        _timeSlotService = timeSlotService;
    }

    [HttpGet]
    public ActionResult<List<Facility>> GetFacilities([FromQuery] SearchFiltersDto filters)
    {
        var facilities = _facilityService.GetFacilities(filters);
        return Ok(facilities);
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public ActionResult<List<FacilitySearchResultDto>> SearchFacilities([FromQuery] SearchFiltersDto filters)
    {
        var results = _facilityService.SearchFacilities(filters);
        return Ok(results);
    }

    [HttpPost]
    [RequireRole("Business")]
    public ActionResult<FacilityDto> AddFacility([FromBody] CreateFacilityDto facilityDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var createdFacility = _facilityService.CreateFacility(facilityDto, userId);
        return CreatedAtAction(nameof(GetFacility), new { id = createdFacility.Id }, createdFacility);
    }

    [HttpGet("{id}")]
    public ActionResult<FacilityDto> GetFacility(Guid id)
    {
        var facility = _facilityService.GetFacility(id);
        if (facility == null) return NotFound();
        return Ok(facility);
    }

    [HttpGet("my-facilities")]
    public ActionResult<List<FacilityDto>> GetUserFacilities()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var facilities = _facilityService.GetUserFacilities(userId);
        return Ok(facilities);
    }

    [HttpPut("{id}")]
    public ActionResult<FacilityDto> UpdateFacility(Guid id, [FromBody] UpdateFacilityDto facilityDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var updatedFacility = _facilityService.UpdateFacility(id, facilityDto, userId);
            if (updatedFacility == null)
            {
                return NotFound("Facility not found");
            }

            return Ok(updatedFacility);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public ActionResult DeleteFacility(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var deleted = _facilityService.DeleteFacility(id, userId);
            if (!deleted)
            {
                return NotFound("Facility not found");
            }
            
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpGet("{id}/available-slots")]
    [AllowAnonymous]
    public async Task<ActionResult<List<TimeSlotItemDto>>> GetAvailableTimeSlots(Guid id, [FromQuery] DateTime date)
    {
        try
        {
            // Validate facility exists
            var facility = _facilityService.GetFacility(id);
            if (facility == null)
            {
                return NotFound("Facility not found");
            }

            // All pending slots are excluded to prevent double booking
            var availableSlots = await _timeSlotService.GetAvailableTimeSlotsAsync(id, date);
            return Ok(availableSlots);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while retrieving available time slots");
        }
    }

    [HttpPut("{id}/schedule")]
    public ActionResult UpdateFacilitySchedule(Guid id, [FromBody] UpdateFacilityAvailabilityDto availabilityDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var facilityAvailability = new FacilityAvailabilityDto
            {
                Weekdays = availabilityDto.Weekdays,
                Saturday = availabilityDto.Saturday,
                Sunday = availabilityDto.Sunday,
                SpecificDates = availabilityDto.SpecificDates
            };

            _facilityService.UpdateFacilityScheduleTemplates(id, facilityAvailability, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating facility schedule", error = ex.Message });
        }
    }

    [HttpGet("{id}/timeslots/{date}")]
    [AllowAnonymous]
    public ActionResult<FacilityDateTimeSlotsDto> GetFacilityTimeSlotsForDate(Guid id, DateTime date)
    {
        try
        {
            var timeSlots = _facilityService.GetFacilityTimeSlotsForDate(id, date);
            if (timeSlots == null)
            {
                return NotFound("Facility not found");
            }

            return Ok(timeSlots);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving facility time slots", error = ex.Message });
        }
    }

    [HttpGet("{id}/bookings/{date}")]
    public ActionResult<FacilityDateTimeSlotsWithBookingsDto> GetFacilityTimeSlotsWithBookings(Guid id, DateTime date)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var result = _facilityService.GetFacilityTimeSlotsWithBookings(id, date, userId);
            if (result == null)
            {
                return NotFound("Facility not found");
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving facility bookings", error = ex.Message });
        }
    }
}
