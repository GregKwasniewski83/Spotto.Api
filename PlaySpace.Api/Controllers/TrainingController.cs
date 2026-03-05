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
public class TrainingController : ControllerBase
{
    private readonly ITrainingService _trainingService;
    private readonly ITrainerProfileService _trainerProfileService;

    public TrainingController(ITrainingService trainingService, ITrainerProfileService trainerProfileService)
    {
        _trainingService = trainingService;
        _trainerProfileService = trainerProfileService;
    }

    [HttpGet]
    [RequireRole("Trainer")]
    public ActionResult<List<TrainingDto>> GetTrainerTrainings()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        // Get trainer profile ID
        var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
        if (trainerProfile == null)
        {
            return StatusCode(404, new ErrorResponse("NOT_FOUND", "Trainer profile not found", 404));
        }

        var trainings = _trainingService.GetTrainerTrainings(trainerProfile.Id);
        return Ok(trainings);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TrainingDto>> GetTraining(Guid id)
    {
        var training = await _trainingService.GetTrainingAsync(id);
        if (training == null)
        {
            return StatusCode(404, new ErrorResponse("NOT_FOUND", "Training not found", 404));
        }

        return Ok(training);
    }

    [HttpPost]
    [RequireRole("Trainer")]
    public ActionResult<TrainingDto> CreateTraining([FromBody] CreateTrainingDto trainingDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            // Get trainer profile ID
            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Trainer profile not found", 404));
            }

            var createdTraining = _trainingService.CreateTraining(trainingDto, trainerProfile.Id);
            return CreatedAtAction(nameof(GetTraining), new { id = createdTraining.Id }, createdTraining);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_ARGUMENT", ex.Message, 400));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while creating training", 500) { Details = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [RequireRole("Trainer")]
    public ActionResult<TrainingDto> UpdateTraining(Guid id, [FromBody] UpdateTrainingDto trainingDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            // Get trainer profile ID
            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Trainer profile not found", 404));
            }

            var updatedTraining = _trainingService.UpdateTraining(id, trainingDto, trainerProfile.Id);
            if (updatedTraining == null)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Training not found", 404));
            }

            return Ok(updatedTraining);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ErrorResponse("FORBIDDEN", ex.Message, 403));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while updating training", 500) { Details = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [RequireRole("Trainer")]
    public ActionResult DeleteTraining(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            // Get trainer profile ID
            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Trainer profile not found", 404));
            }

            var deleted = _trainingService.DeleteTraining(id, trainerProfile.Id);
            if (!deleted)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Training not found", 404));
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ErrorResponse("FORBIDDEN", ex.Message, 403));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while deleting training", 500) { Details = ex.Message });
        }
    }

    [HttpPost("{id}/join")]
    public ActionResult<TrainingParticipantDto> JoinTraining(Guid id, [FromBody] JoinTrainingDto joinDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        if (joinDto == null)
        {
            return BadRequest(new ErrorResponse("INVALID_REQUEST", "Join training data is required, including PaymentId", 400));
        }

        try
        {
            var participant = _trainingService.JoinTraining(id, userId, joinDto);
            if (participant == null)
            {
                return BadRequest(new ErrorResponse("TRAINING_FULL_OR_NOT_FOUND", "Unable to join training. Training may be full or not found.", 400));
            }

            return Ok(participant);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_ARGUMENT", ex.Message, 400));
        }
        catch (InvalidOperationException ex)
        {
            var errorCode = ex.Message.Contains("already enrolled") ? "ALREADY_ENROLLED" : "INVALID_OPERATION";
            return BadRequest(new ErrorResponse(errorCode, ex.Message, 400));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while joining training", 500)
            {
                Details = ex.Message
            });
        }
    }

    [HttpDelete("{id}/leave")]
    public ActionResult LeaveTraining(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            var success = _trainingService.LeaveTraining(id, userId);
            if (!success)
            {
                return BadRequest(new ErrorResponse("INVALID_LEAVE_REQUEST", "Unable to leave training. You may not be a participant or training not found.", 400));
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while leaving training", 500) { Details = ex.Message });
        }
    }

    [HttpGet("my-trainings")]
    public ActionResult<List<TrainingDto>> GetMyTrainings()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            var trainings = _trainingService.GetUserTrainings(userId);
            return Ok(trainings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while retrieving your trainings", 500) { Details = ex.Message });
        }
    }

    [HttpGet("{id}/participants")]
    [RequireRole("Trainer")]
    public ActionResult<List<TrainingParticipantDto>> GetTrainingParticipants(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            // Verify trainer owns this training
            var training = _trainingService.GetTraining(id);
            if (training == null)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Training not found", 404));
            }

            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null || training.TrainerProfileId != trainerProfile.Id)
            {
                return StatusCode(403, new ErrorResponse("FORBIDDEN", "You can only view participants for your own trainings", 403));
            }

            var participants = _trainingService.GetTrainingParticipants(id);
            return Ok(participants);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while retrieving training participants", 500) { Details = ex.Message });
        }
    }

    [HttpPut("{id}/participants/{userId}/status")]
    [RequireRole("Trainer")]
    public ActionResult UpdateParticipantStatus(Guid id, Guid userId, [FromBody] UpdateParticipantStatusDto statusDto)
    {
        var trainerUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (trainerUserIdClaim == null || !Guid.TryParse(trainerUserIdClaim.Value, out var trainerUserId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            // Verify trainer owns this training
            var training = _trainingService.GetTraining(id);
            if (training == null)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Training not found", 404));
            }

            var trainerProfile = _trainerProfileService.GetTrainerProfile(trainerUserId);
            if (trainerProfile == null || training.TrainerProfileId != trainerProfile.Id)
            {
                return StatusCode(403, new ErrorResponse("FORBIDDEN", "You can only manage participants for your own trainings", 403));
            }

            var success = _trainingService.UpdateParticipantStatus(id, userId, statusDto);
            if (!success)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Participant not found", 404));
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while updating participant status", 500) { Details = ex.Message });
        }
    }

    [HttpDelete("{id}/participants/{userId}")]
    [RequireRole("Trainer")]
    public ActionResult RemoveParticipant(Guid id, Guid userId)
    {
        var trainerUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (trainerUserIdClaim == null || !Guid.TryParse(trainerUserIdClaim.Value, out var trainerUserId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            // Get trainer profile ID
            var trainerProfile = _trainerProfileService.GetTrainerProfile(trainerUserId);
            if (trainerProfile == null)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Trainer profile not found", 404));
            }

            var success = _trainingService.RemoveParticipant(id, userId, trainerProfile.Id);
            if (!success)
            {
                return StatusCode(404, new ErrorResponse("NOT_FOUND", "Participant not found", 404));
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ErrorResponse("FORBIDDEN", ex.Message, 403));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while removing participant", 500) { Details = ex.Message });
        }
    }

    [HttpPost("{id}/reserve")]
    public async Task<ActionResult<PendingTrainingParticipantDto>> ReserveTrainingSpot(Guid id, [FromBody] ReserveTrainingDto reserveDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            var pendingParticipant = await _trainingService.ReserveTrainingSpotAsync(id, userId, reserveDto.Notes);
            return Ok(pendingParticipant);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_ARGUMENT", ex.Message, 400));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse("INVALID_ARGUMENT", ex.Message, 400));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while reserving training spot", 500) { Details = ex.Message });
        }
    }

    [HttpGet("search")]
    [AllowAnonymous]
    public ActionResult<List<TrainingSearchResultDto>> SearchTrainings([FromQuery] TrainingSearchDto searchDto)
    {
        try
        {
            var results = _trainingService.SearchTrainings(searchDto);
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while searching for trainings", 500) { Details = ex.Message });
        }
    }

    [HttpGet("upcoming")]
    [AllowAnonymous]
    public ActionResult<List<TrainingSearchResultDto>> GetUpcomingTrainings()
    {
        try
        {
            var results = _trainingService.GetUpcomingTrainings();
            return Ok(results);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while getting upcoming trainings", 500) { Details = ex.Message });
        }
    }

    [HttpGet("my-upcoming-trainings")]
    public ActionResult<List<TrainingDto>> GetMyUpcomingTrainings()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return StatusCode(401, new ErrorResponse("UNAUTHORIZED", "User ID not found in token", 401));
        }

        try
        {
            var trainings = _trainingService.GetUserUpcomingTrainings(userId);
            return Ok(trainings);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ErrorResponse("INTERNAL_ERROR", "An error occurred while retrieving your upcoming trainings", 500) { Details = ex.Message });
        }
    }
}