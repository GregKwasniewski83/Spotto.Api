using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public class PendingTrainingParticipant
{
    public Guid Id { get; set; }
    public Guid TrainingId { get; set; }
    public Guid UserId { get; set; }
    public string? Notes { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public Training? Training { get; set; }
    public User? User { get; set; }
}