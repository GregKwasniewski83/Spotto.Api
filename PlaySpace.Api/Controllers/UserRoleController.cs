using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/user/roles")]
[Authorize]
public class UserRoleController : ControllerBase
{
    private readonly IRoleService _roleService;

    public UserRoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet("my-roles")]
    public ActionResult<List<string>> GetMyRoles()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var roles = _roleService.GetUserRoles(userId);
        return Ok(roles);
    }

    [HttpGet("available-roles")]
    public ActionResult<List<RoleDto>> GetAvailableRoles()
    {
        var roles = _roleService.GetAllRoles();
        return Ok(roles);
    }

    [HttpPost("assign")]
    public ActionResult<RoleAssignmentResponse> AssignRoleToSelf([FromBody] AssignRoleRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var response = _roleService.AssignRoleToSelf(userId, request.RoleName);
        
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("unassign")]
    public ActionResult<RoleAssignmentResponse> UnassignRoleFromSelf([FromBody] UnassignRoleRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var response = _roleService.UnassignRoleFromSelf(userId, request.RoleName);
        
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPut("update")]
    public ActionResult<RoleAssignmentResponse> UpdateUserRoles([FromBody] UpdateUserRolesRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        var response = _roleService.UpdateUserRoles(userId, request.Roles);
        
        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}