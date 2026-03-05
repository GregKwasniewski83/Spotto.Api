namespace PlaySpace.Domain.DTOs;

public class RoleDto
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateRoleDto
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class UserRoleDto
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
}

public class AssignRoleRequest
{
    public required string RoleName { get; set; }
}

public class UnassignRoleRequest
{
    public required string RoleName { get; set; }
}

public class RoleAssignmentResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> CurrentRoles { get; set; } = new List<string>();
}

public class UpdateUserRolesRequest
{
    public List<string> Roles { get; set; } = new List<string>();
}