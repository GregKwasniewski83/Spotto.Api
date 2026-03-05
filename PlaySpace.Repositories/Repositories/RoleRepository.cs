using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly PlaySpaceDbContext _context;

    public RoleRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public Role? GetRoleByName(string name)
    {
        return _context.Roles.FirstOrDefault(r => r.Name == name);
    }

    public Role? GetRoleById(Guid id)
    {
        return _context.Roles.FirstOrDefault(r => r.Id == id);
    }

    public List<Role> GetAllRoles()
    {
        return _context.Roles.ToList();
    }

    public Role CreateRole(CreateRoleDto roleDto)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = roleDto.Name,
            Description = roleDto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Roles.Add(role);
        _context.SaveChanges();
        return role;
    }

    public List<Role> GetUserRoles(Guid userId)
    {
        return _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role)
            .ToList();
    }

    public void AssignRoleToUser(Guid userId, Guid roleId)
    {
        var existingUserRole = _context.UserRoles
            .FirstOrDefault(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (existingUserRole == null)
        {
            var userRole = new UserRole
            {
                UserId = userId,
                RoleId = roleId,
                AssignedAt = DateTime.UtcNow
            };

            _context.UserRoles.Add(userRole);
            _context.SaveChanges();
        }
    }

    public void RemoveRoleFromUser(Guid userId, Guid roleId)
    {
        var userRole = _context.UserRoles
            .FirstOrDefault(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole != null)
        {
            _context.UserRoles.Remove(userRole);
            _context.SaveChanges();
        }
    }
}