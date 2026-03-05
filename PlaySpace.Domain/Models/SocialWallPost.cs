using PlaySpace.Domain.Models;

namespace PlaySpace.Domain.Models;

public class SocialWallPost
{
    public Guid Id { get; set; }
    public required string Type { get; set; } // "training", "event", "announcement", etc.
    public required string Title { get; set; }
    public required string Content { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? TrainingId { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public User? Author { get; set; }
    public Reservation? Reservation { get; set; }
    public Training? Training { get; set; }
    public List<SocialWallPostLike> Likes { get; set; } = new();
    public List<SocialWallPostComment> Comments { get; set; } = new();
}

public class SocialWallPostLike
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public SocialWallPost? Post { get; set; }
    public User? User { get; set; }
}

public class SocialWallPostComment
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public SocialWallPost? Post { get; set; }
    public User? User { get; set; }
}