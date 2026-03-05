using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/business-parent-child")]
public class BusinessParentChildAssociationController : ControllerBase
{
    private readonly IBusinessParentChildAssociationService _associationService;
    private readonly IAgentManagementService _agentManagementService;
    private readonly ILogger<BusinessParentChildAssociationController> _logger;

    public BusinessParentChildAssociationController(
        IBusinessParentChildAssociationService associationService,
        IAgentManagementService agentManagementService,
        ILogger<BusinessParentChildAssociationController> logger)
    {
        _associationService = associationService;
        _agentManagementService = agentManagementService;
        _logger = logger;
    }

    // ============ CONFIRMATION ENDPOINTS ============

    /// <summary>
    /// Get association details by confirmation token.
    /// Used by the frontend to display association information before confirming.
    /// </summary>
    [HttpGet("confirm/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<BusinessParentChildAssociationResponseDto>> GetByToken(string token)
    {
        try
        {
            var association = await _associationService.GetByTokenAsync(token);
            if (association == null)
            {
                return NotFound(new { message = "Association not found or token has expired" });
            }

            return Ok(association);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving association by token");
            return StatusCode(500, new { message = "An error occurred while retrieving association", error = ex.Message });
        }
    }

    /// <summary>
    /// Process association confirmation (confirm or reject).
    /// Called from the frontend confirmation page.
    /// </summary>
    [HttpPost("confirm")]
    [AllowAnonymous]
    public async Task<ActionResult<BusinessParentChildAssociationResponseDto>> ProcessConfirmation(
        [FromBody] ConfirmParentChildAssociationDto dto)
    {
        try
        {
            var result = await _associationService.ProcessConfirmationAsync(
                dto.Token,
                dto.Confirm,
                dto.RejectionReason,
                dto.UseParentTPay,
                dto.UseParentNipForInvoices);

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
            _logger.LogError(ex, "Error processing association confirmation");
            return StatusCode(500, new { message = "An error occurred while processing confirmation", error = ex.Message });
        }
    }

    // ============ CHILD BUSINESS ENDPOINTS ============
    // Note: Association requests are created automatically during business profile registration
    // when parentBusinessProfileId is specified in CreateBusinessProfileDto

    /// <summary>
    /// Get all associations for a child business.
    /// </summary>
    [HttpGet("child/{childBusinessProfileId}/associations")]
    [Authorize]
    public async Task<ActionResult<List<BusinessParentChildAssociationResponseDto>>> GetChildAssociations(
        Guid childBusinessProfileId,
        [FromQuery] string? status = null)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the child business profile
            if (!await HasBusinessProfileAccess(childBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to access this business profile" });
            }

            var associations = await _associationService.GetChildAssociationsAsync(childBusinessProfileId, status);
            return Ok(associations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child associations");
            return StatusCode(500, new { message = "An error occurred while retrieving associations", error = ex.Message });
        }
    }

    /// <summary>
    /// Get the confirmed parent for a child business.
    /// </summary>
    [HttpGet("child/{childBusinessProfileId}/parent")]
    [Authorize]
    public async Task<ActionResult<BusinessParentChildAssociationResponseDto>> GetConfirmedParent(
        Guid childBusinessProfileId)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the child business profile
            if (!await HasBusinessProfileAccess(childBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to access this business profile" });
            }

            var parent = await _associationService.GetConfirmedParentForChildAsync(childBusinessProfileId);
            if (parent == null)
            {
                return NotFound(new { message = "No confirmed parent association found" });
            }

            return Ok(parent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving confirmed parent");
            return StatusCode(500, new { message = "An error occurred while retrieving parent", error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a pending association request.
    /// </summary>
    [HttpDelete("child/{childBusinessProfileId}/request/{parentBusinessProfileId}")]
    [Authorize]
    public async Task<ActionResult> CancelRequest(Guid childBusinessProfileId, Guid parentBusinessProfileId)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the child business profile
            if (!await HasBusinessProfileAccess(childBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to manage this business profile" });
            }

            var result = await _associationService.CancelAssociationRequestAsync(
                childBusinessProfileId,
                parentBusinessProfileId);

            if (!result)
            {
                return NotFound(new { message = "Association request not found" });
            }

            return Ok(new { message = "Association request cancelled successfully" });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling association request");
            return StatusCode(500, new { message = "An error occurred while cancelling request", error = ex.Message });
        }
    }

    /// <summary>
    /// Resend confirmation email for a pending association.
    /// </summary>
    [HttpPost("child/{childBusinessProfileId}/association/{associationId}/resend")]
    [Authorize]
    public async Task<ActionResult> ResendConfirmation(Guid childBusinessProfileId, Guid associationId)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the child business profile
            if (!await HasBusinessProfileAccess(childBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to manage this business profile" });
            }

            var result = await _associationService.ResendConfirmationEmailAsync(associationId, childBusinessProfileId);

            if (!result)
            {
                return BadRequest(new { message = "Failed to resend confirmation email" });
            }

            return Ok(new { message = "Confirmation email sent successfully" });
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
            _logger.LogError(ex, "Error resending confirmation email");
            return StatusCode(500, new { message = "An error occurred while resending email", error = ex.Message });
        }
    }

    /// <summary>
    /// Remove an association (as child business).
    /// </summary>
    [HttpDelete("child/{childBusinessProfileId}/association/{associationId}")]
    [Authorize]
    public async Task<ActionResult> RemoveAssociationAsChild(Guid childBusinessProfileId, Guid associationId)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the child business profile
            if (!await HasBusinessProfileAccess(childBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to manage this business profile" });
            }

            var result = await _associationService.RemoveAssociationAsync(associationId, childBusinessProfileId);

            if (!result)
            {
                return NotFound(new { message = "Association not found" });
            }

            return Ok(new { message = "Association removed successfully" });
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing association");
            return StatusCode(500, new { message = "An error occurred while removing association", error = ex.Message });
        }
    }

    // ============ PARENT BUSINESS ENDPOINTS ============

    /// <summary>
    /// Get all child associations for a parent business profile.
    /// </summary>
    [HttpGet("parent/{parentBusinessProfileId}/children")]
    [Authorize]
    public async Task<ActionResult<List<BusinessParentChildAssociationResponseDto>>> GetParentChildAssociations(
        Guid parentBusinessProfileId,
        [FromQuery] string? status = null)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the parent business profile
            if (!await HasBusinessProfileAccess(parentBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to access this business profile's associations" });
            }

            var associations = await _associationService.GetParentAssociationsAsync(parentBusinessProfileId, status);
            return Ok(associations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child associations for parent");
            return StatusCode(500, new { message = "An error occurred while retrieving associations", error = ex.Message });
        }
    }

    /// <summary>
    /// Get pending child association requests for a parent business.
    /// </summary>
    [HttpGet("parent/{parentBusinessProfileId}/pending")]
    [Authorize]
    public async Task<ActionResult<List<PendingChildAssociationRequestDto>>> GetPendingChildRequests(
        Guid parentBusinessProfileId)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the parent business profile
            if (!await HasBusinessProfileAccess(parentBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to access this business profile's requests" });
            }

            var pendingRequests = await _associationService.GetPendingRequestsForParentAsync(parentBusinessProfileId);
            return Ok(pendingRequests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending child requests");
            return StatusCode(500, new { message = "An error occurred while retrieving pending requests", error = ex.Message });
        }
    }

    /// <summary>
    /// Update permissions for a child business association.
    /// </summary>
    [HttpPut("parent/{parentBusinessProfileId}/children/{associationId}/permissions")]
    [Authorize]
    public async Task<ActionResult<BusinessParentChildAssociationResponseDto>> UpdateChildPermissions(
        Guid parentBusinessProfileId,
        Guid associationId,
        [FromBody] UpdateChildAssociationPermissionsDto dto)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the parent business profile
            if (!await HasBusinessProfileAccess(parentBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to modify this association" });
            }

            var result = await _associationService.UpdateChildPermissionsAsync(associationId, parentBusinessProfileId, dto);
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
            _logger.LogError(ex, "Error updating child permissions");
            return StatusCode(500, new { message = "An error occurred while updating permissions", error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a child business association (as parent).
    /// </summary>
    [HttpDelete("parent/{parentBusinessProfileId}/children/{associationId}")]
    [Authorize]
    public async Task<ActionResult> RemoveChildAssociation(Guid parentBusinessProfileId, Guid associationId)
    {
        try
        {
            var userId = GetUserId();

            // Verify user has access to the parent business profile
            if (!await HasBusinessProfileAccess(parentBusinessProfileId, userId))
            {
                return StatusCode(403, new { message = "You do not have permission to modify this association" });
            }

            var result = await _associationService.ParentRemoveAssociationAsync(associationId, parentBusinessProfileId);

            if (!result)
            {
                return NotFound(new { message = "Association not found" });
            }

            return Ok(new { message = "Child association removed successfully" });
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing child association");
            return StatusCode(500, new { message = "An error occurred while removing association", error = ex.Message });
        }
    }

    // ============ HELPER ENDPOINTS ============

    /// <summary>
    /// Check if association between child and parent is confirmed.
    /// </summary>
    [HttpGet("check/{childBusinessProfileId}/{parentBusinessProfileId}")]
    [Authorize]
    public async Task<ActionResult<bool>> IsAssociationConfirmed(
        Guid childBusinessProfileId,
        Guid parentBusinessProfileId)
    {
        try
        {
            var isConfirmed = await _associationService.IsAssociationConfirmedAsync(childBusinessProfileId, parentBusinessProfileId);
            return Ok(new { isConfirmed });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking association status");
            return StatusCode(500, new { message = "An error occurred while checking association status", error = ex.Message });
        }
    }

    /// <summary>
    /// Get effective TPay merchant ID for a business profile.
    /// Returns parent's merchant ID if child uses parent's TPay.
    /// </summary>
    [HttpGet("effective-tpay/{businessProfileId}")]
    [Authorize]
    public async Task<ActionResult> GetEffectiveTPayMerchantId(Guid businessProfileId)
    {
        try
        {
            var merchantId = await _associationService.GetEffectiveTPayMerchantIdAsync(businessProfileId);
            var usesParentTPay = await _associationService.ShouldUseParentTPayAsync(businessProfileId);

            return Ok(new
            {
                merchantId,
                usesParentTPay
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting effective TPay merchant ID");
            return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        }
    }

    /// <summary>
    /// Get effective seller info for invoices.
    /// Returns parent's info if child uses parent's NIP for invoices.
    /// </summary>
    [HttpGet("effective-seller/{businessProfileId}")]
    [Authorize]
    public async Task<ActionResult> GetEffectiveSellerInfo(Guid businessProfileId)
    {
        try
        {
            var sellerInfo = await _associationService.GetEffectiveSellerInfoAsync(businessProfileId);
            var usesParentNip = await _associationService.ShouldUseParentNipForInvoicesAsync(businessProfileId);

            if (sellerInfo == null)
            {
                return NotFound(new { message = "Business profile not found" });
            }

            return Ok(new
            {
                nip = sellerInfo.Value.Nip,
                companyName = sellerInfo.Value.CompanyName,
                address = sellerInfo.Value.Address,
                city = sellerInfo.Value.City,
                postalCode = sellerInfo.Value.PostalCode,
                usesParentNip
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting effective seller info");
            return StatusCode(500, new { message = "An error occurred", error = ex.Message });
        }
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    private async Task<bool> HasBusinessProfileAccess(Guid businessProfileId, Guid userId)
    {
        // Check if user is the business owner
        var isOwner = await _agentManagementService.IsUserBusinessOwnerAsync(userId, businessProfileId);
        if (isOwner) return true;

        // Check if user is an agent for this business profile
        var isAgent = await _agentManagementService.IsUserAgentForBusinessAsync(userId, businessProfileId);
        return isAgent;
    }
}
