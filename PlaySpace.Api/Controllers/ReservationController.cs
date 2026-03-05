using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.Attributes;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.Exceptions;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservationController : ControllerBase
{
    private readonly IReservationService _reservationService;
    private readonly ILogger<ReservationController> _logger;

    public ReservationController(IReservationService reservationService, ILogger<ReservationController> logger)
    {
        _reservationService = reservationService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ReservationDto>> CreateReservation([FromBody] CreateReservationDto reservationDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            // Check if time slots are available (excluding current user's pending reservations)
            var isAvailable = await _reservationService.IsTimeSlotAvailableAsync(
                reservationDto.TimeSlots,
                reservationDto.FacilityId,
                reservationDto.Date,
                userId);

            if (!isAvailable)
            {
                return BadRequest("One or more time slots are not available");
            }

            // Check if trainer is available (if specified)
            if (reservationDto.TrainerProfileId.HasValue)
            {
                var isTrainerAvailable = await _reservationService.IsTrainerAvailableAsync(
                    reservationDto.TrainerProfileId.Value,
                    reservationDto.TimeSlots,
                    reservationDto.Date);

                if (!isTrainerAvailable)
                {
                    return BadRequest("Trainer is not available for the selected time slots");
                }
            }

            var reservation = await _reservationService.CreateReservationAsync(reservationDto, userId);
            return Ok(reservation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while creating the reservation");
        }
    }

    [HttpPost("admin")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<ReservationDto>> CreateAdminReservation([FromBody] AdminCreateReservationDto reservationDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var createdById))
            {
                _logger.LogWarning("Admin reservation creation failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Creating admin reservation for facility {FacilityId} by user {CreatedById}", reservationDto.FacilityId, createdById);

            var reservation = await _reservationService.CreateAdminReservationAsync(reservationDto, createdById);

            _logger.LogInformation("Admin reservation {ReservationId} created successfully", reservation.Id);
            return Ok(reservation);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Admin reservation validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Admin reservation operation failed: {Message}", ex.Message);
            return BadRequest(new { error = "OPERATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin reservation creation failed: {Message}. Inner exception: {InnerException}",
                ex.Message, ex.InnerException?.Message ?? "None");

            var errorDetails = new
            {
                error = "INTERNAL_ERROR",
                message = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace?.Split('\n').Take(5).ToArray()
            };

            return StatusCode(500, errorDetails);
        }
    }

    [HttpPost("admin/group")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<GroupReservationResponseDto>> CreateAdminGroupReservation([FromBody] CreateAdminGroupReservationDto groupDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var createdById))
            {
                _logger.LogWarning("Admin group reservation creation failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Creating admin group reservation with {FacilityCount} facilities by user {CreatedById}",
                groupDto.FacilityReservations.Count, createdById);

            var groupReservation = await _reservationService.CreateAdminGroupReservationAsync(groupDto, createdById);

            _logger.LogInformation("Admin group reservation {GroupId} created successfully with {ReservationCount} reservations",
                groupReservation.GroupId, groupReservation.Reservations.Count);

            return Ok(groupReservation);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Admin group reservation validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Admin group reservation operation failed: {Message}", ex.Message);
            return BadRequest(new { error = "OPERATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin group reservation creation failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public ActionResult<ReservationDto> GetReservation(Guid id)
    {
        try
        {
            var reservation = _reservationService.GetReservation(id);
            if (reservation == null)
            {
                return NotFound("Reservation not found");
            }
            return Ok(reservation);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while retrieving the reservation");
        }
    }

    [HttpGet("user")]
    public ActionResult<List<ReservationDto>> GetUserReservations()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var reservations = _reservationService.GetUserReservations(userId);
            return Ok(reservations);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while retrieving reservations");
        }
    }

    /// <summary>
    /// Get unpaid reservations for the current user (created by agents but not yet paid)
    /// </summary>
    [HttpGet("user/unpaid")]
    public ActionResult<List<ReservationDto>> GetUnpaidReservations()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var reservations = _reservationService.GetUnpaidReservationsForUser(userId);
            return Ok(reservations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving unpaid reservations", error = ex.Message });
        }
    }

    /// <summary>
    /// Initiate payment for an existing unpaid reservation
    /// </summary>
    [HttpPost("{id}/pay")]
    public async Task<ActionResult<PayForReservationResponseDto>> PayForReservation(Guid id, [FromBody] PayForReservationDto paymentDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var result = await _reservationService.PayForReservationAsync(id, paymentDto, userId);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing payment", error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ReservationDto>> UpdateReservation(Guid id, [FromBody] UpdateReservationDto updateDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            // Get existing reservation to check time slots and date
            var existingReservation = _reservationService.GetReservation(id);
            if (existingReservation == null || existingReservation.UserId != userId)
            {
                return NotFound("Reservation not found or access denied");
            }

            // Check if trainer is available (if specified)
            if (updateDto.TrainerProfileId.HasValue)
            {
                var isTrainerAvailable = await _reservationService.IsTrainerAvailableAsync(
                    updateDto.TrainerProfileId.Value,
                    existingReservation.TimeSlots,
                    existingReservation.Date);

                if (!isTrainerAvailable)
                {
                    return BadRequest("Trainer is not available for the selected time slots");
                }
            }

            var updatedReservation = _reservationService.UpdateReservation(id, updateDto, userId);
            if (updatedReservation == null)
            {
                return NotFound("Reservation not found or cannot be updated");
            }

            return Ok(updatedReservation);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while updating the reservation");
        }
    }

    [HttpPost("availability")]
    public async Task<ActionResult<bool>> CheckAvailability([FromBody] CreateReservationDto reservationDto)
    {
        try
        {
            // Get user ID if authenticated (optional for availability check)
            Guid? userId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim != null && Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                userId = parsedUserId;
            }

            var isAvailable = await _reservationService.IsTimeSlotAvailableAsync(
                reservationDto.TimeSlots, 
                reservationDto.FacilityId, 
                reservationDto.Date,
                userId); // Exclude current user's pending reservations
            
            return Ok(new { available = isAvailable });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while checking availability");
        }
    }

    [HttpDelete("{id}")]
    public ActionResult CancelReservation(Guid id)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var success = _reservationService.CancelReservation(id, userId);
            if (!success)
            {
                return NotFound("Reservation not found or cannot be cancelled");
            }

            return Ok(new { message = "Reservation cancelled successfully" });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while cancelling the reservation");
        }
    }

    [HttpPost("{id}/cancel-with-refund")]
    public async Task<ActionResult<ReservationDto>> CancelReservationWithRefund(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized("User not authenticated");
        }

        var cancelledReservation = await _reservationService.CancelReservationWithRefundAsync(id, userId);
        return Ok(cancelledReservation);
    }

    [HttpPost("{id}/agent-cancel")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<ReservationDto>> AgentCancelReservation(Guid id, [FromBody] AgentCancelReservationDto cancelDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var agentUserId))
            {
                _logger.LogWarning("Agent cancellation failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Agent {AgentUserId} attempting to cancel reservation {ReservationId}", agentUserId, id);

            var cancelledReservation = await _reservationService.CancelReservationByAgentAsync(id, agentUserId, cancelDto);

            _logger.LogInformation("Reservation {ReservationId} cancelled successfully by agent {AgentUserId}", id, agentUserId);
            return Ok(cancelledReservation);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent cancellation failed: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning(ex, "Agent cancellation forbidden: {Message}", ex.Message);
            return StatusCode(403, new { error = "FORBIDDEN", message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Agent cancellation failed business rule: {Message}", ex.Message);
            return BadRequest(new { error = "BUSINESS_RULE_VIOLATION", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent cancellation failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while cancelling the reservation" });
        }
    }

    [HttpGet("user/upcoming")]
    public ActionResult<List<ReservationDto>> GetUserUpcomingReservations()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var reservations = _reservationService.GetUserUpcomingReservations(userId);
            return Ok(reservations);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while retrieving upcoming reservations");
        }
    }

    [HttpPost("group")]
    public async Task<ActionResult<GroupReservationResponseDto>> CreateGroupReservation([FromBody] CreateGroupReservationDto groupReservationDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var groupReservation = await _reservationService.CreateGroupReservationAsync(groupReservationDto, userId);
            return Ok(groupReservation);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "ArgumentException in CreateGroupReservation");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "InvalidOperationException in CreateGroupReservation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in CreateGroupReservation");
            return StatusCode(500, new { error = "An error occurred while creating the group reservation" });
        }
    }

    [HttpGet("group/{groupId}")]
    public ActionResult<GroupReservationResponseDto> GetGroupReservation(Guid groupId)
    {
        try
        {
            var groupReservation = _reservationService.GetGroupReservation(groupId);
            if (groupReservation == null)
            {
                return NotFound("Group reservation not found");
            }
            return Ok(groupReservation);
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while retrieving the group reservation");
        }
    }

    [HttpDelete("group/{groupId}")]
    public ActionResult CancelGroupReservation(Guid groupId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized("User not authenticated");
            }

            var success = _reservationService.CancelGroupReservation(groupId, userId);
            if (!success)
            {
                return NotFound("Group reservation not found or cannot be cancelled");
            }

            return Ok(new { message = "Group reservation cancelled successfully" });
        }
        catch (Exception)
        {
            return StatusCode(500, "An error occurred while cancelling the group reservation");
        }
    }

    // NEW: Cancel specific slots from a reservation (partial cancellation)
    [HttpPost("{id}/cancel-slots")]
    [Authorize]
    public async Task<IActionResult> CancelSpecificSlots(Guid id, [FromBody] CancelSlotsDto cancelSlotsDto)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var result = await _reservationService.CancelSpecificSlotsAsync(id, cancelSlotsDto.SlotIds, userId);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            return Forbid(ex.Message);
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while cancelling slots: {ex.Message}");
        }
    }

    // NEW: Get reservation with detailed slot information
    [HttpGet("{id}/slots")]
    public async Task<IActionResult> GetReservationWithSlots(Guid id)
    {
        try
        {
            var result = await _reservationService.GetReservationWithSlotsAsync(id);
            if (result == null)
            {
                return NotFound($"Reservation with ID {id} not found");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while retrieving reservation: {ex.Message}");
        }
    }

    // NEW: Toggle reservation payment status for offline payments (agents/business owners only)
    [HttpPatch("{id}/toggle-payment")]
    public async Task<IActionResult> ToggleReservationPayment(Guid id)
    {
        try
        {
            // Get user ID from JWT token
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("Invalid user token");
            }

            var result = await _reservationService.ToggleReservationPaymentAsync(id, userId);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while marking reservation as paid: {ex.Message}");
        }
    }

    // NEW: Apply product purchase to reservation (for agents/business owners)
    [HttpPost("{id}/apply-product")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<ApplyProductResultDto>> ApplyProductToReservation(Guid id, [FromBody] ApplyProductToReservationDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var agentUserId))
            {
                _logger.LogWarning("Apply product failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Agent {AgentUserId} applying product to reservation {ReservationId}", agentUserId, id);

            var result = await _reservationService.ApplyProductToReservationAsync(id, dto, agentUserId);

            _logger.LogInformation("Product applied successfully to reservation {ReservationId}", id);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Apply product failed: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning(ex, "Apply product forbidden: {Message}", ex.Message);
            return StatusCode(403, new { error = "FORBIDDEN", message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Apply product business rule violation: {Message}", ex.Message);
            return BadRequest(new { error = "BUSINESS_RULE", message = ex.Message });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Apply product validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply product failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while applying product to reservation" });
        }
    }

    // NEW: Reschedule reservation (for agents/business owners)
    [HttpPost("{id}/reschedule")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<RescheduleResultDto>> RescheduleReservation(Guid id, [FromBody] RescheduleReservationDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var agentUserId))
            {
                _logger.LogWarning("Reschedule failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Agent {AgentUserId} rescheduling reservation {ReservationId}", agentUserId, id);

            var result = await _reservationService.RescheduleReservationAsync(id, dto, agentUserId);

            _logger.LogInformation("Reservation {ReservationId} rescheduled successfully", id);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Reschedule failed: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning(ex, "Reschedule forbidden: {Message}", ex.Message);
            return StatusCode(403, new { error = "FORBIDDEN", message = ex.Message });
        }
        catch (ConflictException ex)
        {
            _logger.LogWarning(ex, "Reschedule conflict: {Message}", ex.Message);
            return Conflict(new { error = "CONFLICT", message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning(ex, "Reschedule business rule violation: {Message}", ex.Message);
            return BadRequest(new { error = "BUSINESS_RULE", message = ex.Message });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Reschedule validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reschedule failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while rescheduling reservation" });
        }
    }

    // NEW: Update reservation notes (for agents/business owners)
    [HttpPatch("{id}/notes")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<ReservationDto>> UpdateReservationNotes(Guid id, [FromBody] UpdateReservationNotesDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("Update notes failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("User {UserId} updating notes for reservation {ReservationId}", userId, id);

            var result = await _reservationService.UpdateReservationNotesAsync(id, dto, userId);

            _logger.LogInformation("Notes updated for reservation {ReservationId}", id);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Update notes failed: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning(ex, "Update notes forbidden: {Message}", ex.Message);
            return StatusCode(403, new { error = "FORBIDDEN", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update notes failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while updating reservation notes" });
        }
    }
}
