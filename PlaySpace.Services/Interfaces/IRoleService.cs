using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IRoleService
{
    RoleDto? GetRoleByName(string name);
    List<RoleDto> GetAllRoles();
    RoleDto CreateRole(CreateRoleDto roleDto);
    List<string> GetUserRoles(Guid userId);
    void AssignRoleToUser(Guid userId, string roleName);
    void RemoveRoleFromUser(Guid userId, string roleName);
    void EnsureDefaultRolesExist();
    RoleAssignmentResponse AssignRoleToSelf(Guid userId, string roleName);
    RoleAssignmentResponse UnassignRoleFromSelf(Guid userId, string roleName);
    RoleAssignmentResponse UpdateUserRoles(Guid userId, List<string> roleNames);
}