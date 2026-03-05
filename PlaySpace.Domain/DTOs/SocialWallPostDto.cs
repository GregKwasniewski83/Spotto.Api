namespace PlaySpace.Domain.DTOs;

public class SocialWallPostDto
{
    public Guid Id { get; set; }
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public Guid AuthorId { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? TrainingId { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Author information
    public string? AuthorFirstName { get; set; }
    public string? AuthorLastName { get; set; }
    public string? AuthorEmail { get; set; }
    
    // Related objects (populated from Training if available)
    public TrainerProfileDto? Trainer { get; set; }
    public FacilityDto? Facility { get; set; }
    
    // Engagement statistics
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    
    // Related data
    public List<SocialWallPostCommentDto> Comments { get; set; } = new();
}

public class CreateSocialWallPostDto
{
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? TrainingId { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? Tags { get; set; }
}

public class UpdateSocialWallPostDto
{
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Content { get; set; }
    public Guid? ReservationId { get; set; }
    public Guid? TrainingId { get; set; }
    public string? ImageUrl { get; set; }
    public List<string>? Tags { get; set; }
    public bool IsActive { get; set; }
}

public class SocialWallPostCommentDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // User information
    public string? UserFirstName { get; set; }
    public string? UserLastName { get; set; }
    public string? UserEmail { get; set; }
}

public class CreateSocialWallPostCommentDto
{
    public required string Content { get; set; }
}

public class UpdateSocialWallPostCommentDto
{
    public required string Content { get; set; }
}

public class SocialWallPostLikeDto
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // User information
    public string? UserFirstName { get; set; }
    public string? UserLastName { get; set; }
    public string? UserEmail { get; set; }
}

public class SocialWallPostSearchDto
{
    public string? Type { get; set; }
    public string? Search { get; set; } // Search in title and content
    public List<string>? Tags { get; set; }
    public Guid? AuthorId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool? IsActive { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}