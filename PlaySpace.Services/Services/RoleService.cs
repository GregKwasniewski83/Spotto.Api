using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using PlaySpace.Repositories.Interfaces;
using System;

namespace PlaySpace.Services.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;

    public RoleService(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    public RoleDto? GetRoleByName(string name)
    {
        var role = _roleRepository.GetRoleByName(name);
        return role == null ? null : MapToDto(role);
    }

    public List<RoleDto> GetAllRoles()
    {
        var roles = _roleRepository.GetAllRoles();
        return roles.Select(MapToDto).ToList();
    }

    public RoleDto CreateRole(CreateRoleDto roleDto)
    {
        var role = _roleRepository.CreateRole(roleDto);
        return MapToDto(role);
    }

    public List<string> GetUserRoles(Guid userId)
    {
        var roles = _roleRepository.GetUserRoles(userId);
        return roles.Select(r => r.Name).ToList();
    }

    public void AssignRoleToUser(Guid userId, string roleName)
    {
        var role = _roleRepository.GetRoleByName(roleName);
        if (role != null)
        {
            _roleRepository.AssignRoleToUser(userId, role.Id);
        }
    }

    public void RemoveRoleFromUser(Guid userId, string roleName)
    {
        var role = _roleRepository.GetRoleByName(roleName);
        if (role != null)
        {
            _roleRepository.RemoveRoleFromUser(userId, role.Id);
        }
    }

    public void EnsureDefaultRolesExist()
    {
        var defaultRoles = new[] { "Player", "Business", "Trainer", "Agent" };

        foreach (var roleName in defaultRoles)
        {
            var existingRole = _roleRepository.GetRoleByName(roleName);
            if (existingRole == null)
            {
                _roleRepository.CreateRole(new CreateRoleDto
                {
                    Name = roleName,
                    Description = $"Default {roleName} role"
                });
            }
        }
    }

    public RoleAssignmentResponse AssignRoleToSelf(Guid userId, string roleName)
    {
        try
        {
            // Check if role exists
            var role = _roleRepository.GetRoleByName(roleName);
            if (role == null)
            {
                return new RoleAssignmentResponse
                {
                    Success = false,
                    Message = $"Role '{roleName}' does not exist",
                    CurrentRoles = GetUserRoles(userId)
                };
            }

            // Check if user already has this role
            var currentRoles = GetUserRoles(userId);
            if (currentRoles.Contains(roleName))
            {
                return new RoleAssignmentResponse
                {
                    Success = false,
                    Message = $"You already have the '{roleName}' role",
                    CurrentRoles = currentRoles
                };
            }

            // Assign the role
            AssignRoleToUser(userId, roleName);
            var updatedRoles = GetUserRoles(userId);

            return new RoleAssignmentResponse
            {
                Success = true,
                Message = $"Successfully assigned '{roleName}' role",
                CurrentRoles = updatedRoles
            };
        }
        catch (Exception ex)
        {
            return new RoleAssignmentResponse
            {
                Success = false,
                Message = $"Failed to assign role: {ex.Message}",
                CurrentRoles = GetUserRoles(userId)
            };
        }
    }

    public RoleAssignmentResponse UnassignRoleFromSelf(Guid userId, string roleName)
    {
        try
        {
            // Prevent removing Player role
            if (roleName.Equals("Player", StringComparison.OrdinalIgnoreCase))
            {
                return new RoleAssignmentResponse
                {
                    Success = false,
                    Message = "Cannot remove Player role. Every user must have Player role",
                    CurrentRoles = GetUserRoles(userId)
                };
            }

            // Check if user has this role
            var currentRoles = GetUserRoles(userId);
            if (!currentRoles.Contains(roleName))
            {
                return new RoleAssignmentResponse
                {
                    Success = false,
                    Message = $"You don't have the '{roleName}' role to remove",
                    CurrentRoles = currentRoles
                };
            }

            // Remove the role
            RemoveRoleFromUser(userId, roleName);
            var updatedRoles = GetUserRoles(userId);

            return new RoleAssignmentResponse
            {
                Success = true,
                Message = $"Successfully removed '{roleName}' role",
                CurrentRoles = updatedRoles
            };
        }
        catch (Exception ex)
        {
            return new RoleAssignmentResponse
            {
                Success = false,
                Message = $"Failed to remove role: {ex.Message}",
                CurrentRoles = GetUserRoles(userId)
            };
        }
    }

    public RoleAssignmentResponse UpdateUserRoles(Guid userId, List<string> roleNames)
    {
        try
        {
            // Ensure Player is always included
            var updatedRoles = roleNames.ToList();
            if (!updatedRoles.Contains("Player"))
            {
                updatedRoles.Add("Player");
            }

            // Validate all roles exist
            var invalidRoles = new List<string>();
            foreach (var roleName in updatedRoles)
            {
                var role = _roleRepository.GetRoleByName(roleName);
                if (role == null)
                {
                    invalidRoles.Add(roleName);
                }
            }

            if (invalidRoles.Any())
            {
                return new RoleAssignmentResponse
                {
                    Success = false,
                    Message = $"Invalid role(s): {string.Join(", ", invalidRoles)}",
                    CurrentRoles = GetUserRoles(userId)
                };
            }

            // Get current roles
            var currentRoles = GetUserRoles(userId);

            // Remove roles that are no longer needed (except Player)
            var rolesToRemove = currentRoles.Except(updatedRoles).Where(r => r != "Player").ToList();
            foreach (var roleToRemove in rolesToRemove)
            {
                RemoveRoleFromUser(userId, roleToRemove);
            }

            // Add new roles
            var rolesToAdd = updatedRoles.Except(currentRoles).ToList();
            foreach (var roleToAdd in rolesToAdd)
            {
                AssignRoleToUser(userId, roleToAdd);
            }

            var finalRoles = GetUserRoles(userId);
            return new RoleAssignmentResponse
            {
                Success = true,
                Message = $"Successfully updated roles to: {string.Join(", ", finalRoles)}",
                CurrentRoles = finalRoles
            };
        }
        catch (Exception ex)
        {
            return new RoleAssignmentResponse
            {
                Success = false,
                Message = $"Failed to update roles: {ex.Message}",
                CurrentRoles = GetUserRoles(userId)
            };
        }
    }

    private RoleDto MapToDto(Role role)
    {
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt
        };
    }
}