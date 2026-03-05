using PlaySpace.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PrivacySettingsController : ControllerBase
{
    private readonly IPrivacySettingsService _privacySettingsService;
    private readonly ILogger<PrivacySettingsController> _logger;

    public PrivacySettingsController(
        IPrivacySettingsService privacySettingsService,
        ILogger<PrivacySettingsController> logger)
    {
        _privacySettingsService = privacySettingsService;
        _logger = logger;
    }

    /// <summary>
    /// Get privacy settings for the authenticated user
    /// Returns 404 if user hasn't completed privacy onboarding yet
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PrivacySettingsResponseDto>> GetPrivacySettings()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var settings = await _privacySettingsService.GetPrivacySettingsAsync(userId);
            if (settings == null)
            {
                return NotFound("Privacy settings not found. User needs to complete privacy onboarding.");
            }

            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving privacy settings for user");
            return StatusCode(500, "An error occurred while retrieving privacy settings");
        }
    }

    /// <summary>
    /// Create privacy settings for the authenticated user
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<PrivacySettingsResponseDto>> CreatePrivacySettings(
        [FromBody] PrivacySettingsUpdateRequestDto request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Request body cannot be empty");
            }

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            // Check if privacy settings already exist
            var existingSettings = await _privacySettingsService.GetPrivacySettingsAsync(userId);
            if (existingSettings != null)
            {
                return Conflict("Privacy settings already exist for this user. Use PUT to update them.");
            }

            var createdSettings = await _privacySettingsService.CreatePrivacySettingsAsync(userId, request);
            
            _logger.LogInformation("Privacy settings created for user {UserId}", userId);
            return CreatedAtAction(nameof(GetPrivacySettings), createdSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating privacy settings for user");
            return StatusCode(500, "An error occurred while creating privacy settings");
        }
    }

    /// <summary>
    /// Complete privacy onboarding by creating initial privacy settings
    /// </summary>
    [HttpPost("onboarding")]
    public async Task<ActionResult<PrivacySettingsResponseDto>> CompletePrivacyOnboarding(
        [FromBody] PrivacySettingsUpdateRequestDto request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Request body cannot be empty");
            }

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            // Check if privacy settings already exist
            var existingSettings = await _privacySettingsService.GetPrivacySettingsAsync(userId);
            if (existingSettings != null)
            {
                return BadRequest("Privacy settings already exist for this user");
            }

            var createdSettings = await _privacySettingsService.CreatePrivacySettingsAsync(userId, request);
            
            _logger.LogInformation("User {UserId} completed privacy onboarding", userId);
            return Ok(createdSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing privacy onboarding for user");
            return StatusCode(500, "An error occurred while completing privacy onboarding");
        }
    }

    /// <summary>
    /// Update privacy settings for the authenticated user
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<PrivacySettingsResponseDto>> UpdatePrivacySettings(
        [FromBody] PrivacySettingsUpdateRequestDto request)
    {
        try
        {
            if (request == null)
            {
                return BadRequest("Request body cannot be empty");
            }

            if (!request.HasAnyUpdate())
            {
                return BadRequest("At least one privacy setting must be provided");
            }

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var updatedSettings = await _privacySettingsService.UpdatePrivacySettingsAsync(userId, request);
            return Ok(updatedSettings);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Privacy settings not found for user update");
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for privacy settings update");
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating privacy settings for user");
            return StatusCode(500, "An error occurred while updating privacy settings");
        }
    }

    /// <summary>
    /// Update a specific privacy setting
    /// </summary>
    [HttpPatch("{settingName}")]
    public async Task<ActionResult<PrivacySettingsResponseDto>> UpdateSpecificSetting(
        string settingName,
        [FromBody] PrivacySettingUpdateDto update)
    {
        try
        {
            if (update == null)
            {
                return BadRequest("Request body cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(settingName))
            {
                return BadRequest("Setting name cannot be empty");
            }

            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var updatedSettings = await _privacySettingsService.UpdateSpecificSettingAsync(
                userId, settingName, update.Value);
            
            if (updatedSettings == null)
            {
                return BadRequest($"Invalid setting name: {settingName}");
            }

            return Ok(updatedSettings);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Privacy settings not found for specific setting update");
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid setting name: {SettingName}", settingName);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating specific privacy setting {SettingName}", settingName);
            return StatusCode(500, "An error occurred while updating the privacy setting");
        }
    }

    /// <summary>
    /// Reset privacy settings to default values
    /// </summary>
    [HttpPost("reset")]
    public async Task<ActionResult<PrivacySettingsResponseDto>> ResetPrivacySettings()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Unauthorized("User ID not found in token");
            }

            var defaultSettings = await _privacySettingsService.ResetToDefaultAsync(userId);
            return Ok(defaultSettings);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Privacy settings not found for reset");
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting privacy settings for user");
            return StatusCode(500, "An error occurred while resetting privacy settings");
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return Guid.Empty;
    }
}