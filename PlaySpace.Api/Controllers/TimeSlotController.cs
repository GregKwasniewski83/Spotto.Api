using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/Facility/{facilityId}/timeslots")]
[Authorize]
public class TimeSlotController : ControllerBase
{
    private readonly ITimeSlotService _timeSlotService;

    public TimeSlotController(ITimeSlotService timeSlotService)
    {
        _timeSlotService = timeSlotService;
    }

    [HttpPut]
    public ActionResult UpdateFacilityTimeSlots(
        Guid facilityId,
        [FromBody] UpdateTimeSlotsDto updateDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            _timeSlotService.UpdateFacilityTimeSlots(facilityId, updateDto, userId);
            return Ok(new { message = "Time slots updated successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating time slots", error = ex.Message });
        }
    }

    [HttpGet]
    public ActionResult GetFacilityTimeSlots(
        Guid facilityId, 
        [FromQuery] string? date = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
            {
                // Get merged slots for specific date (all-time template + date overrides)
                var timeSlotsForDate = _timeSlotService.GetFacilityTimeSlotsForDate(facilityId, parsedDate);
                return Ok(new { timeSlots = timeSlotsForDate });
            }
            else
            {
                // Get structured slots (all-time templates + all date-specific exceptions)
                var timeSlots = _timeSlotService.GetFacilityTimeSlots(facilityId);
                return Ok(timeSlots);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving time slots", error = ex.Message });
        }
    }
}