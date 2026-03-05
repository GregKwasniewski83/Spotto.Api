namespace PlaySpace.Domain.Models;

public class AgentInvitation
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string InvitationToken { get; set; }
    public Guid BusinessProfileId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public Guid? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public BusinessProfile BusinessProfile { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
    public User? AcceptedByUser { get; set; }
}