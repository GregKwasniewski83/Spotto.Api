using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface IRoleRepository
{
    Role? GetRoleByName(string name);
    Role? GetRoleById(Guid id);
    List<Role> GetAllRoles();
    Role CreateRole(CreateRoleDto roleDto);
    List<Role> GetUserRoles(Guid userId);
    void AssignRoleToUser(Guid userId, Guid roleId);
    void RemoveRoleFromUser(Guid userId, Guid roleId);
}