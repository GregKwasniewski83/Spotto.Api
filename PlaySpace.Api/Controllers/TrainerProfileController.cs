using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/trainer-profile")]
[Authorize]
public class TrainerProfileController : ControllerBase
{
    private readonly ITrainerProfileService _trainerProfileService;
    private readonly ITrainerBusinessAssociationService _associationService;

    public TrainerProfileController(
        ITrainerProfileService trainerProfileService,
        ITrainerBusinessAssociationService associationService)
    {
        _trainerProfileService = trainerProfileService;
        _associationService = associationService;
    }

    [HttpGet]
    public ActionResult<TrainerProfileDto> GetTrainerProfile()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var profile = _trainerProfileService.GetTrainerProfile(userId);
            if (profile == null)
            {
                return NotFound("Trainer profile not found");
            }

            return Ok(profile);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving trainer profile", error = ex.Message });
        }
    }

    [HttpGet("{trainerProfileId}")]
    [AllowAnonymous]
    public ActionResult<TrainerProfileDto> GetTrainerProfileById(Guid trainerProfileId)
    {
        try
        {
            var profile = _trainerProfileService.GetTrainerProfileById(trainerProfileId);
            if (profile == null)
            {
                return NotFound("Trainer profile not found");
            }

            return Ok(profile);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving trainer profile", error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<ActionResult<TrainerProfileDto>> CreateTrainerProfile([FromBody] CreateTrainerProfileDto profileDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var createdProfile = await _trainerProfileService.CreateTrainerProfile(profileDto, userId);
            
            // Return additional information about TPay registration
            var response = new
            {
                trainerProfile = createdProfile,
                tpayRegistration = new
                {
                    isRegistered = !string.IsNullOrEmpty(createdProfile.TPayMerchantId),
                    merchantId = createdProfile.TPayMerchantId,
                    activationLink = createdProfile.TPayActivationLink,
                    verificationStatus = createdProfile.TPayVerificationStatus
                }
            };
            
            return CreatedAtAction(nameof(GetTrainerProfile), response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (TPayTransactionException ex)
        {
            var problem = new TPayProblemDetails
            {
                Type = "https://playspace.app/problems/tpay-registration-failed",
                Title = "TPay Registration Failed",
                Status = 400,
                Detail = GetPrimaryErrorMessage(ex),
                Instance = Request.Path,
                TPayRequestId = ex.TPayRequestId,
                TPayErrors = ex.TPayErrors?.Select(e => new TPayErrorDetail
                {
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    FieldName = e.FieldName,
                    DevMessage = e.DevMessage,
                    DocUrl = e.DocUrl
                }).ToList()
            };
            return BadRequest(problem);
        }
        catch (TPayException ex)
        {
            var problem = new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/tpay-error",
                Title = "TPay Service Error",
                Status = 400,
                Detail = ex.Message,
                Instance = Request.Path
            };
            return BadRequest(problem);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating trainer profile", error = ex.Message });
        }
    }

    [HttpPut]
    public async Task<ActionResult<TrainerProfileDto>> UpdateTrainerProfile([FromBody] UpdateTrainerProfileDto profileDto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var updatedProfile = await _trainerProfileService.UpdateTrainerProfile(userId, profileDto);
            if (updatedProfile == null)
            {
                return NotFound("Trainer profile not found");
            }

            // Return additional information about TPay registration if it was updated
            var response = new
            {
                trainerProfile = updatedProfile,
                tpayRegistration = new
                {
                    isRegistered = !string.IsNullOrEmpty(updatedProfile.TPayMerchantId),
                    merchantId = updatedProfile.TPayMerchantId,
                    activationLink = updatedProfile.TPayActivationLink,
                    verificationStatus = updatedProfile.TPayVerificationStatus,
                    wasUpdated = profileDto.UpdateTPayRegistration
                }
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating trainer profile", error = ex.Message });
        }
    }

    [HttpDelete]
    public ActionResult DeleteTrainerProfile()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var deleted = _trainerProfileService.DeleteTrainerProfile(userId);
            if (!deleted)
            {
                return NotFound("Trainer profile not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting trainer profile", error = ex.Message });
        }
    }

    [HttpPost("avatar")]
    public async Task<ActionResult<string>> UploadAvatar(IFormFile avatar)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var avatarUrl = await _trainerProfileService.UploadAvatarAsync(userId, avatar);
            return Ok(new { avatarUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while uploading avatar", error = ex.Message });
        }
    }

    [HttpPost("associate-business")]
    public ActionResult<BusinessAssociationResultDto> AssociateBusinessProfiles([FromBody] AssociateBusinessProfileDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (dto.BusinessProfileIds == null || !dto.BusinessProfileIds.Any())
            {
                return BadRequest(new { message = "At least one business profile ID is required" });
            }

            var result = _trainerProfileService.AssociateMultipleBusinessProfiles(userId, dto.BusinessProfileIds);
            
            if (result.Failed.Any() && !result.Successful.Any())
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while associating business profiles", error = ex.Message });
        }
    }

    [HttpPost("disassociate-business")]
    public ActionResult<BusinessAssociationResultDto> DisassociateBusinessProfiles([FromBody] DisassociateBusinessProfileDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            if (dto.BusinessProfileIds == null || !dto.BusinessProfileIds.Any())
            {
                return BadRequest(new { message = "At least one business profile ID is required" });
            }

            var result = _trainerProfileService.DisassociateMultipleBusinessProfiles(userId, dto.BusinessProfileIds);
            
            if (result.Failed.Any() && !result.Successful.Any())
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while disassociating business profiles", error = ex.Message });
        }
    }

    [HttpGet("associated-businesses")]
    public ActionResult<List<BusinessProfileDto>> GetAssociatedBusinessProfiles()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var associatedBusinesses = _trainerProfileService.GetAssociatedBusinessProfiles(userId);
            return Ok(associatedBusinesses);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving associated business profiles", error = ex.Message });
        }
    }

    [HttpPost("find-available")]
    [AllowAnonymous]
    public ActionResult<List<AvailableTrainerDto>> FindAvailableTrainers([FromBody] TrainerAvailabilitySearchDto searchDto)
    {
        try
        {
            if (searchDto.TimeSlots == null || !searchDto.TimeSlots.Any())
            {
                return BadRequest(new { message = "At least one time slot is required" });
            }

            var availableTrainers = _trainerProfileService.FindAvailableTrainers(searchDto.Date, searchDto.TimeSlots);
            return Ok(availableTrainers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while finding available trainers", error = ex.Message });
        }
    }

    [HttpPost("find-available-for-business/{businessProfileId}")]
    [AllowAnonymous]
    public ActionResult<List<AvailableTrainerDto>> FindAvailableTrainersForBusiness(Guid businessProfileId, [FromBody] TrainerAvailabilitySearchDto searchDto)
    {
        try
        {
            if (searchDto.TimeSlots == null || !searchDto.TimeSlots.Any())
            {
                return BadRequest(new { message = "At least one time slot is required" });
            }

            var availableTrainers = _trainerProfileService.FindAvailableTrainersForBusiness(businessProfileId, searchDto.Date, searchDto.TimeSlots);
            return Ok(availableTrainers);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while finding available trainers for business", error = ex.Message });
        }
    }

    [HttpPut("schedule")]
    public async Task<ActionResult> UpdateScheduleWithBusiness([FromBody] UpdateTrainerScheduleWithBusinessDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            await _trainerProfileService.UpdateTrainerScheduleWithBusinessAsync(userId, dto);
            return Ok(new { message = "Schedule updated successfully" });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating schedule", error = ex.Message });
        }
    }

    [HttpPut("availability/date")]
    public async Task<ActionResult> UpdateDateAvailabilityWithBusiness([FromBody] UpdateTrainerDateAvailabilityWithBusinessDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            await _trainerProfileService.UpdateTrainerDateAvailabilityWithBusinessAsync(userId, dto);
            return Ok(new { message = "Date availability updated successfully" });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating date availability", error = ex.Message });
        }
    }

    [HttpGet("timeslots/{date}")]
    public ActionResult<TrainerDateTimeSlotsDto> GetMyTimeSlotsForDate(DateTime date)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var timeSlots = _trainerProfileService.GetMyTimeSlotsForDate(userId, date);
            if (timeSlots == null)
            {
                return NotFound("Trainer profile not found");
            }

            return Ok(timeSlots);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving trainer time slots", error = ex.Message });
        }
    }

    [HttpGet("test/timeslots/{userId}/{date}")]
    [AllowAnonymous]
    public ActionResult<TrainerDateTimeSlotsDto> TestGetTimeSlotsForDate(Guid userId, DateTime date)
    {
        try
        {
            var timeSlots = _trainerProfileService.GetMyTimeSlotsForDate(userId, date);
            if (timeSlots == null)
            {
                return NotFound($"Trainer profile not found for userId: {userId}");
            }

            return Ok(timeSlots);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving trainer time slots", error = ex.Message });
        }
    }

    [HttpPost("tpay/register")]
    public async Task<ActionResult<TrainerProfileDto>> RegisterWithTPay([FromBody] TPayBusinessRegistrationRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var result = await _trainerProfileService.RegisterWithTPayAsync(userId, request);

            return Ok(new
            {
                success = true,
                message = "Trainer successfully registered with TPay",
                trainerProfile = result,
                activationLink = result.TPayActivationLink,
                verificationStatus = result.TPayVerificationStatus
            });
        }
        catch (InvalidOperationException ex)
        {
            var problem = new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/invalid-operation",
                Title = "Invalid Operation",
                Status = 400,
                Detail = ex.Message,
                Instance = Request.Path
            };
            return BadRequest(problem);
        }
        catch (TPayTransactionException ex)
        {
            var problem = new TPayProblemDetails
            {
                Type = "https://playspace.app/problems/tpay-registration-failed",
                Title = "TPay Registration Failed",
                Status = 400,
                Detail = GetPrimaryErrorMessage(ex),
                Instance = Request.Path,
                TPayRequestId = ex.TPayRequestId,
                TPayErrors = ex.TPayErrors?.Select(e => new TPayErrorDetail
                {
                    ErrorCode = e.ErrorCode,
                    ErrorMessage = e.ErrorMessage,
                    FieldName = e.FieldName,
                    DevMessage = e.DevMessage,
                    DocUrl = e.DocUrl
                }).ToList()
            };

            return BadRequest(problem);
        }
        catch (TPayException ex)
        {
            var problem = new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/tpay-error",
                Title = "TPay Service Error",
                Status = 400,
                Detail = ex.Message,
                Instance = Request.Path
            };
            return BadRequest(problem);
        }
        catch (Exception ex)
        {
            var problem = new Domain.Models.ProblemDetails
            {
                Type = "https://playspace.app/problems/registration-failed",
                Title = "Registration Failed",
                Status = 500,
                Detail = "An unexpected error occurred while registering with TPay",
                Instance = Request.Path
            };
            problem.Extensions.Add("internalError", ex.Message);
            return StatusCode(500, problem);
        }
    }

    private static string GetPrimaryErrorMessage(TPayTransactionException ex)
    {
        if (ex.TPayErrors?.Count > 0)
        {
            return ex.TPayErrors.First().ErrorMessage;
        }
        return ex.Message;
    }

    // ==================== Trainer-Business Association Endpoints ====================

    [HttpPost("associations/request")]
    public async Task<ActionResult<TrainerBusinessAssociationResponseDto>> RequestAssociation([FromBody] RequestAssociationDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // Get trainer profile for this user
            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return NotFound("Trainer profile not found. Please create a trainer profile first.");
            }

            var result = await _associationService.RequestAssociationAsync(trainerProfile.Id, dto.BusinessProfileId);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while requesting association", error = ex.Message });
        }
    }

    [HttpGet("associations/by-token/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<TrainerBusinessAssociationResponseDto>> GetAssociationByToken(string token)
    {
        try
        {
            var association = await _associationService.GetByTokenAsync(token);
            if (association == null)
            {
                return NotFound(new { message = "Association not found or token expired" });
            }

            return Ok(association);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving association", error = ex.Message });
        }
    }

    [HttpPost("associations/confirm")]
    [AllowAnonymous]
    public async Task<ActionResult<TrainerBusinessAssociationResponseDto>> ProcessConfirmation([FromBody] ConfirmAssociationDto dto)
    {
        try
        {
            var result = await _associationService.ProcessConfirmationAsync(
                dto.Token,
                dto.Confirm,
                dto.RejectionReason,
                dto.CanRunOwnTrainings,
                dto.IsEmployee);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while processing confirmation", error = ex.Message });
        }
    }

    [HttpGet("associations")]
    public async Task<ActionResult<List<TrainerBusinessAssociationResponseDto>>> GetMyAssociations([FromQuery] string? status = null)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return NotFound("Trainer profile not found");
            }

            var associations = await _associationService.GetTrainerAssociationsAsync(trainerProfile.Id, status);
            return Ok(associations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving associations", error = ex.Message });
        }
    }

    [HttpGet("associations/confirmed")]
    public async Task<ActionResult<List<TrainerBusinessAssociationResponseDto>>> GetConfirmedAssociations()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return NotFound("Trainer profile not found");
            }

            var associations = await _associationService.GetConfirmedAssociationsForTrainerAsync(trainerProfile.Id);
            return Ok(associations);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving confirmed associations", error = ex.Message });
        }
    }

    [HttpDelete("associations/{associationId}")]
    public async Task<ActionResult> RemoveAssociation(Guid associationId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var result = await _associationService.RemoveAssociationAsync(associationId, userId);
            if (!result)
            {
                return NotFound("Association not found");
            }

            return NoContent();
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while removing association", error = ex.Message });
        }
    }

    [HttpPost("associations/{associationId}/resend")]
    public async Task<ActionResult> ResendConfirmationEmail(Guid associationId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return NotFound("Trainer profile not found");
            }

            var result = await _associationService.ResendConfirmationEmailAsync(associationId, trainerProfile.Id);
            return Ok(new { success = result, message = "Confirmation email resent successfully" });
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
            return StatusCode(500, new { message = "An error occurred while resending confirmation email", error = ex.Message });
        }
    }

    [HttpPost("associations/{associationId}/cancel")]
    public async Task<ActionResult> CancelAssociationRequest(Guid associationId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var trainerProfile = _trainerProfileService.GetTrainerProfile(userId);
            if (trainerProfile == null)
            {
                return NotFound("Trainer profile not found");
            }

            // Get association to verify ownership and get business profile ID
            var associations = await _associationService.GetTrainerAssociationsAsync(trainerProfile.Id, "Pending");
            var association = associations.FirstOrDefault(a => a.Id == associationId);
            if (association == null)
            {
                return NotFound("Pending association not found");
            }

            var result = await _associationService.CancelAssociationRequestAsync(trainerProfile.Id, association.BusinessProfileId);
            if (!result)
            {
                return NotFound("Association not found");
            }

            return Ok(new { success = true, message = "Association request cancelled successfully" });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while cancelling association request", error = ex.Message });
        }
    }

    // ==================== TPay Endpoints ====================

    [HttpPost("tpay/update-merchant")]
    public async Task<ActionResult<TrainerProfileDto>> UpdateTPayMerchantData([FromBody] TPayBusinessRegistrationResponse response)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var result = await _trainerProfileService.UpdateTPayMerchantDataAsync(userId, response);
            
            return Ok(new {
                success = true,
                message = "TPay merchant data updated successfully",
                trainerProfile = result
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { 
                error = "UPDATE_FAILED", 
                message = "Failed to update TPay merchant data", 
                details = ex.Message 
            });
        }
    }
}