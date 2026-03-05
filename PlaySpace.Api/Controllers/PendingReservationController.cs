using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PendingReservationController : ControllerBase
{
    private readonly IPendingTimeSlotReservationService _pendingReservationService;

    public PendingReservationController(IPendingTimeSlotReservationService pendingReservationService)
    {
        _pendingReservationService = pendingReservationService;
    }

    [HttpPost]
    public async Task<ActionResult<PendingReservationDto>> CreatePendingReservation([FromBody] CreatePendingReservationDto pendingReservationDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var pendingReservation = await _pendingReservationService.CreatePendingReservationAsync(pendingReservationDto, userId);
            return Ok(pendingReservation);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while creating the pending reservation");
        }
    }

    [HttpGet("{facilityId}/{date}")]
    public async Task<ActionResult<PendingReservationDto>> GetUserPendingReservation(Guid facilityId, DateTime date)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var pendingReservation = await _pendingReservationService.GetUserPendingReservationAsync(facilityId, date, userId);
            if (pendingReservation == null)
            {
                return NotFound("No pending reservation found");
            }

            return Ok(pendingReservation);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while retrieving the pending reservation");
        }
    }

    [HttpPut("{pendingReservationId}/extend")]
    public async Task<ActionResult> ExtendPendingReservation(Guid pendingReservationId, [FromBody] ExtendPendingReservationDto extendDto)
    {
        try
        {
            var success = await _pendingReservationService.ExtendPendingReservationAsync(pendingReservationId, extendDto.AdditionalMinutes);
            if (!success)
            {
                return NotFound("Pending reservation not found or already expired");
            }

            return Ok(new { message = "Pending reservation extended successfully" });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while extending the pending reservation");
        }
    }

    [HttpDelete("{facilityId}/{date}")]
    public async Task<ActionResult> ReleasePendingReservation(Guid facilityId, DateTime date)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var success = await _pendingReservationService.ReleasePendingReservationAsync(facilityId, date, userId);
            if (!success)
            {
                return NotFound("No pending reservation found to release");
            }

            return Ok(new { message = "Pending reservation released successfully" });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while releasing the pending reservation");
        }
    }

    [HttpPost("cleanup")]
    public async Task<ActionResult> CleanupExpiredReservations()
    {
        try
        {
            var cleanedCount = await _pendingReservationService.CleanupExpiredPendingReservationsAsync();
            return Ok(new { message = $"Cleaned up {cleanedCount} expired pending reservations" });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while cleaning up expired reservations");
        }
    }
}

public class ExtendPendingReservationDto
{
    public int AdditionalMinutes { get; set; } = 15;
}