using PlaySpace.Domain.Configuration;
using PlaySpace.Domain.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly FrontendConfiguration _frontendConfig;

    public UserController(IUserService userService, IRoleService roleService, IPushNotificationService pushNotificationService, IOptions<FrontendConfiguration> frontendConfig)
    {
        _userService = userService;
        _roleService = roleService;
        _pushNotificationService = pushNotificationService;
        _frontendConfig = frontendConfig.Value;
    }

    [HttpGet("{id}")]
    public ActionResult<UserDto> GetUser(Guid id)
    {
        var user = _userService.GetUser(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserDto> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var user = _userService.GetCurrentUser(userId);
        if (user == null) return NotFound("User not found");
        
        return Ok(user);
    }

    [HttpPost]
    public ActionResult<UserDto> CreateUser([FromBody] UserDto user)
    {
        var createdUser = _userService.CreateUser(user);
        return CreatedAtAction(nameof(GetUser), new { id = createdUser.Id }, createdUser);
    }

    [HttpPut("{id}/player-terms")]
    [Authorize]
    public async Task<ActionResult> UpdatePlayerTerms(Guid id, [FromBody] UpdateTermsRequest request)
    {
        var success = await _userService.UpdatePlayerTermsAsync(id, request.Accepted);
        if (!success) return NotFound("User not found");

        // Assign Player role if terms are accepted
        if (request.Accepted)
        {
            try
            {
                _roleService.AssignRoleToUser(id, "Player");
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the terms update
                // The terms were updated successfully, role assignment is secondary
            }
        }

        return Ok(new { message = "Player terms updated successfully" });
    }

    [HttpPut("{id}/business-terms")]
    [Authorize]
    public async Task<ActionResult> UpdateBusinessTerms(Guid id, [FromBody] UpdateTermsRequest request)
    {
        var success = await _userService.UpdateBusinessTermsAsync(id, request.Accepted);
        if (!success) return NotFound("User not found");

        // Assign Business role if terms are accepted, remove if not accepted
        if (request.Accepted)
        {
            try
            {
                _roleService.AssignRoleToUser(id, "Business");
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the terms update
                // The terms were updated successfully, role assignment is secondary
            }
        }
        else
        {
            try
            {
                _roleService.RemoveRoleFromUser(id, "Business");
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the terms update
                // The terms were updated successfully, role removal is secondary
            }
        }

        return Ok(new { message = "Business terms updated successfully" });
    }

    [HttpPut("{id}/trainer-terms")]
    [Authorize]
    public async Task<ActionResult> UpdateTrainerTerms(Guid id, [FromBody] UpdateTermsRequest request)
    {
        var success = await _userService.UpdateTrainerTermsAsync(id, request.Accepted);
        if (!success) return NotFound("User not found");

        // Assign Trainer role if terms are accepted, remove if not accepted
        if (request.Accepted)
        {
            try
            {
                _roleService.AssignRoleToUser(id, "Trainer");
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the terms update
                // The terms were updated successfully, role assignment is secondary
            }
        }
        else
        {
            try
            {
                _roleService.RemoveRoleFromUser(id, "Trainer");
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the terms update
                // The terms were updated successfully, role removal is secondary
            }
        }

        return Ok(new { message = "Trainer terms updated successfully" });
    }

    [HttpGet("profile")]
    [Authorize]
    public ActionResult<UserProfileDto> GetUserProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var userProfile = _userService.GetUserProfile(userId);
        if (userProfile == null) return NotFound("User profile not found");
        
        return Ok(userProfile);
    }

    [HttpPut("profile")]
    [Authorize]
    public ActionResult<UserProfileDto> UpdateUserProfile([FromBody] UpdateUserProfileDto updateDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var updatedProfile = _userService.UpdateUserProfile(userId, updateDto);
        if (updatedProfile == null) return NotFound("User not found");
        
        return Ok(updatedProfile);
    }

    [HttpPut("activity-interests")]
    [Authorize]
    public async Task<ActionResult> UpdateActivityInterests([FromBody] UpdateUserActivityInterestsDto updateDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var success = await _userService.UpdateActivityInterestsAsync(userId, updateDto.ActivityInterests);
        if (!success) return NotFound("User not found");
        
        return Ok(new { message = "Activity interests updated successfully" });
    }

    [HttpPost("avatar")]
    [Authorize]
    public async Task<ActionResult<string>> UploadAvatar(IFormFile avatar)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var avatarUrl = await _userService.UploadAvatarAsync(userId, avatar);
            return Ok(new { avatarUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while uploading avatar", error = ex.Message });
        }
    }

    [HttpPut("avatar-url")]
    [Authorize]
    public async Task<ActionResult> UpdateAvatarUrl([FromBody] UpdateAvatarUrlDto updateDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var success = await _userService.UpdateAvatarUrlAsync(userId, updateDto.AvatarUrl);
            if (!success)
            {
                return NotFound("User not found");
            }

            return Ok(new { message = "Avatar URL updated successfully", avatarUrl = updateDto.AvatarUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating avatar URL", error = ex.Message });
        }
    }

    [HttpPut("terms/update")]
    [Authorize]
    public async Task<ActionResult> UpdateAllTerms([FromBody] UpdateAllTermsRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var success = await _userService.UpdateAllTermsAsync(userId, request.PlayerTerms, request.BusinessTerms, request.TrainerTerms);
            if (!success)
            {
                return NotFound("User not found");
            }

            if (request.PlayerTerms)
            {
                try
                {
                    _roleService.AssignRoleToUser(userId, "Player");
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                try
                {
                    //Player role is not removed even if terms are not accepted
                    //Everyone is a player ;) !
                    //_roleService.RemoveRoleFromUser(userId, "Player");
                }
                catch (Exception ex)
                {
                }
            }

            if (request.BusinessTerms)
            {
                try
                {
                    _roleService.AssignRoleToUser(userId, "Business");
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                try
                {
                    _roleService.RemoveRoleFromUser(userId, "Business");
                }
                catch (Exception ex)
                {
                }
            }

            if (request.TrainerTerms)
            {
                try
                {
                    _roleService.AssignRoleToUser(userId, "Trainer");
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
                try
                {
                    _roleService.RemoveRoleFromUser(userId, "Trainer");
                }
                catch (Exception ex)
                {
                }
            }

            return Ok(new { message = "Terms updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating terms", error = ex.Message });
        }
    }

    [HttpPost("test-notification")]
    [Authorize]
    public async Task<ActionResult> SendTestNotification([FromBody] TestNotificationRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        if (string.IsNullOrEmpty(request.PushToken))
        {
            return BadRequest("Push token is required");
        }

        try
        {
            var success = await _pushNotificationService.SendCustomNotificationAsync(
                request.PushToken,
                "Test Notification! 🚀",
                "Hey Bro! This is a test notification from Spotto API. Everything is working perfectly! 🎉",
                new Dictionary<string, string>
                {
                    ["type"] = "TEST_NOTIFICATION",
                    ["userId"] = userId.ToString(),
                    ["deepLink"] = $"{_frontendConfig.DeepLinkScheme}://home"
                }
            );

            if (success)
            {
                return Ok(new { message = "Test notification sent successfully Bro!" });
            }
            else
            {
                return StatusCode(500, new { message = "Failed to send test notification" });
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while sending test notification", error = ex.Message });
        }
    }
}

public class UpdateTermsRequest
{
    public bool Accepted { get; set; }
}

public class UpdateAllTermsRequest
{
    public bool PlayerTerms { get; set; }
    public bool BusinessTerms { get; set; }
    public bool TrainerTerms { get; set; }
}

public class TestNotificationRequest
{
    public string PushToken { get; set; } = string.Empty;
}
