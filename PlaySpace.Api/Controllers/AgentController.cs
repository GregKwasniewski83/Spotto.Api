using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaySpace.Domain.Attributes;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AgentController : ControllerBase
{
    private readonly IAgentManagementService _agentManagementService;
    private readonly IBusinessProfileService _businessProfileService;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IAgentManagementService agentManagementService,
        IBusinessProfileService businessProfileService,
        ILogger<AgentController> logger)
    {
        _agentManagementService = agentManagementService;
        _businessProfileService = businessProfileService;
        _logger = logger;
    }

    [HttpPost("invite")]
    [RequireRole("Business")]
    public async Task<ActionResult<AgentInvitationResponse>> InviteAgent([FromBody] InviteAgentDto inviteDto, [FromQuery] Guid? businessProfileId = null)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // If businessProfileId not provided, try to get user's business profile
            Guid targetBusinessProfileId;
            if (businessProfileId.HasValue)
            {
                targetBusinessProfileId = businessProfileId.Value;
            }
            else
            {
                var userBusinessProfile = _businessProfileService.GetBusinessProfileByUserId(userId);
                if (userBusinessProfile == null)
                {
                    return BadRequest(new { message = "Business profile not found. Please specify businessProfileId or create a business profile first." });
                }
                targetBusinessProfileId = userBusinessProfile.Id;
            }

            var response = await _agentManagementService.InviteAgentAsync(targetBusinessProfileId, userId, inviteDto);
            
            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting agent {Email}", inviteDto.Email);
            return StatusCode(500, new { message = "An error occurred while sending the invitation" });
        }
    }

    [HttpGet("business-profile/{businessProfileId}")]
    [RequireRole("Business")]
    public async Task<ActionResult<AgentOperationResponse>> GetBusinessAgents(Guid businessProfileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // Verify user owns this business profile
            if (!await _agentManagementService.IsUserBusinessOwnerAsync(userId, businessProfileId))
            {
                return StatusCode(403, new { message = "You don't have permission to view agents for this business profile" });
            }

            var response = await _agentManagementService.GetBusinessAgentsAsync(businessProfileId);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents for business profile {BusinessProfileId}", businessProfileId);
            return StatusCode(500, new { message = "An error occurred while retrieving agents" });
        }
    }

    [HttpGet("my-business")]
    [RequireRole("Business")]
    public async Task<ActionResult<AgentOperationResponse>> GetMyBusinessAgents()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var userBusinessProfile = _businessProfileService.GetBusinessProfileByUserId(userId);
            if (userBusinessProfile == null)
            {
                return BadRequest(new { message = "Business profile not found" });
            }

            var response = await _agentManagementService.GetBusinessAgentsAsync(userBusinessProfile.Id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents for user's business profile");
            return StatusCode(500, new { message = "An error occurred while retrieving agents" });
        }
    }

    [HttpDelete("business-profile/{businessProfileId}/agent/{agentUserId}")]
    [RequireRole("Business")]
    public async Task<ActionResult<AgentOperationResponse>> RemoveAgent(Guid businessProfileId, Guid agentUserId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var response = await _agentManagementService.RemoveAgentAsync(businessProfileId, agentUserId, userId);
            
            if (response.Success)
            {
                return Ok(response);
            }
            else
            {
                return BadRequest(response);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing agent {AgentUserId} from business profile {BusinessProfileId}", agentUserId, businessProfileId);
            return StatusCode(500, new { message = "An error occurred while removing the agent" });
        }
    }

    [HttpGet("invitations/business-profile/{businessProfileId}")]
    [RequireRole("Business")]
    public async Task<ActionResult<List<AgentInvitationDto>>> GetPendingInvitations(Guid businessProfileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            // Verify user owns this business profile
            if (!await _agentManagementService.IsUserBusinessOwnerAsync(userId, businessProfileId))
            {
                return StatusCode(403, new { message = "You don't have permission to view invitations for this business profile" });
            }

            var invitations = await _agentManagementService.GetPendingInvitationsAsync(businessProfileId);
            return Ok(invitations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending invitations for business profile {BusinessProfileId}", businessProfileId);
            return StatusCode(500, new { message = "An error occurred while retrieving invitations" });
        }
    }

    [HttpDelete("invitations/{invitationId}")]
    [RequireRole("Business")]
    public async Task<ActionResult> CancelInvitation(Guid invitationId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("User ID not found in token");
            }

            var success = await _agentManagementService.CancelInvitationAsync(invitationId, userId);
            
            if (success)
            {
                return Ok(new { message = "Invitation cancelled successfully" });
            }
            else
            {
                return BadRequest(new { message = "Failed to cancel invitation. It may not exist or you don't have permission." });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling invitation {InvitationId}", invitationId);
            return StatusCode(500, new { message = "An error occurred while cancelling the invitation" });
        }
    }

    [HttpGet("invitation/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<AgentInvitationDto>> GetInvitationDetails(string token)
    {
        try
        {
            var invitation = await _agentManagementService.GetInvitationByTokenAsync(token);
            
            if (invitation == null)
            {
                return NotFound(new { message = "Invitation not found or expired" });
            }

            return Ok(invitation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invitation details for token {Token}", token);
            return StatusCode(500, new { message = "An error occurred while retrieving invitation details" });
        }
    }
}