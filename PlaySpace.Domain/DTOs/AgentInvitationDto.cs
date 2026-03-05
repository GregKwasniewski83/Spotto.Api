namespace PlaySpace.Domain.DTOs;

// DTO for inviting a new agent
public class InviteAgentDto
{
    public required string Email { get; set; }
}

// DTO for agent invitation details
public class AgentInvitationDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid BusinessProfileId { get; set; }
    public string BusinessProfileName { get; set; } = string.Empty;
    public Guid InvitedByUserId { get; set; }
    public string InvitedByUserName { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public string? AcceptedByUserName { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// DTO for agent registration with invitation token
public class RegisterAgentDto
{
    public required string InvitationToken { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Password { get; set; }
    public string? Phone { get; set; }
    public string? DateOfBirth { get; set; }
    public List<string> ActivityInterests { get; set; } = new();
}

// DTO for agent details in business profile context
public class BusinessAgentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public string AssignedByUserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// Response DTO for agent invitation
public class AgentInvitationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public AgentInvitationDto? Invitation { get; set; }
}

// Response DTO for agent operations
public class AgentOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<BusinessAgentDto> Agents { get; set; } = new();
}