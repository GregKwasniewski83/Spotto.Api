namespace PlaySpace.Domain.Models;

public class BusinessProfileAgent
{
    public Guid Id { get; set; }
    public Guid BusinessProfileId { get; set; }
    public Guid AgentUserId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public DateTime AssignedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public BusinessProfile BusinessProfile { get; set; } = null!;
    public User AgentUser { get; set; } = null!;
    public User AssignedByUser { get; set; } = null!;
}