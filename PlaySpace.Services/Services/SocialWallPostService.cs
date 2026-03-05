using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class SocialWallPostService : ISocialWallPostService
{
    private readonly ISocialWallPostRepository _socialWallPostRepository;

    public SocialWallPostService(ISocialWallPostRepository socialWallPostRepository)
    {
        _socialWallPostRepository = socialWallPostRepository;
    }

    public SocialWallPostDto CreatePost(CreateSocialWallPostDto postDto, Guid authorId)
    {
        var post = _socialWallPostRepository.CreatePost(postDto, authorId);
        return MapToDto(post, authorId);
    }

    public SocialWallPostDto? GetPost(Guid id, Guid? currentUserId = null)
    {
        var post = _socialWallPostRepository.GetPost(id);
        return post == null ? null : MapToDto(post, currentUserId);
    }

    public SocialWallPostDto? UpdatePost(Guid id, UpdateSocialWallPostDto postDto, Guid authorId)
    {
        var post = _socialWallPostRepository.UpdatePost(id, postDto, authorId);
        return post == null ? null : MapToDto(post, authorId);
    }

    public bool DeletePost(Guid id, Guid authorId)
    {
        return _socialWallPostRepository.DeletePost(id, authorId);
    }

    public List<SocialWallPostDto> GetPosts(SocialWallPostSearchDto searchDto, Guid? currentUserId = null)
    {
        var posts = _socialWallPostRepository.GetPosts(searchDto);
        return posts.Select(p => MapToDto(p, currentUserId)).ToList();
    }

    public List<SocialWallPostDto> GetUserPosts(Guid userId, Guid? currentUserId = null)
    {
        var posts = _socialWallPostRepository.GetUserPosts(userId);
        return posts.Select(p => MapToDto(p, currentUserId)).ToList();
    }

    public bool ToggleLike(Guid postId, Guid userId)
    {
        var like = _socialWallPostRepository.ToggleLike(postId, userId);
        return like != null; // Returns true if liked, false if unliked
    }

    public List<SocialWallPostLikeDto> GetPostLikes(Guid postId)
    {
        var likes = _socialWallPostRepository.GetPostLikes(postId);
        return likes.Select(MapLikeToDto).ToList();
    }

    public SocialWallPostCommentDto CreateComment(Guid postId, Guid userId, CreateSocialWallPostCommentDto commentDto)
    {
        var comment = _socialWallPostRepository.CreateComment(postId, userId, commentDto.Content);
        return MapCommentToDto(comment);
    }

    public SocialWallPostCommentDto? GetComment(Guid id)
    {
        var comment = _socialWallPostRepository.GetComment(id);
        return comment == null ? null : MapCommentToDto(comment);
    }

    public SocialWallPostCommentDto? UpdateComment(Guid id, UpdateSocialWallPostCommentDto commentDto, Guid userId)
    {
        var comment = _socialWallPostRepository.UpdateComment(id, commentDto.Content, userId);
        return comment == null ? null : MapCommentToDto(comment);
    }

    public bool DeleteComment(Guid id, Guid userId)
    {
        return _socialWallPostRepository.DeleteComment(id, userId);
    }

    public List<SocialWallPostCommentDto> GetPostComments(Guid postId)
    {
        var comments = _socialWallPostRepository.GetPostComments(postId);
        return comments.Select(MapCommentToDto).ToList();
    }

    private SocialWallPostDto MapToDto(SocialWallPost post, Guid? currentUserId = null)
    {
        var activeLikes = post.Likes.Where(l => l.User != null).ToList();
        
        return new SocialWallPostDto
        {
            Id = post.Id,
            Type = post.Type,
            Title = post.Title,
            Content = post.Content,
            AuthorId = post.AuthorId,
            ReservationId = post.ReservationId,
            TrainingId = post.TrainingId,
            ImageUrl = post.ImageUrl,
            Tags = post.Tags,
            IsActive = post.IsActive,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            AuthorFirstName = post.Author?.FirstName,
            AuthorLastName = post.Author?.LastName,
            AuthorEmail = post.Author?.Email,
            Trainer = MapTrainerProfileToDto(post.Training?.TrainerProfile ?? post.Reservation?.TrainerProfile),
            Facility = MapFacilityToDto(post.Training?.Facility ?? post.Reservation?.Facility),
            LikesCount = activeLikes.Count,
            CommentsCount = post.Comments.Count,
            IsLikedByCurrentUser = currentUserId.HasValue && 
                post.Likes.Any(l => l.UserId == currentUserId.Value),
            Comments = post.Comments.Select(MapCommentToDto).ToList()
        };
    }

    private SocialWallPostLikeDto MapLikeToDto(SocialWallPostLike like)
    {
        return new SocialWallPostLikeDto
        {
            Id = like.Id,
            PostId = like.PostId,
            UserId = like.UserId,
            CreatedAt = like.CreatedAt,
            UserFirstName = like.User?.FirstName,
            UserLastName = like.User?.LastName,
            UserEmail = like.User?.Email
        };
    }

    private SocialWallPostCommentDto MapCommentToDto(SocialWallPostComment comment)
    {
        return new SocialWallPostCommentDto
        {
            Id = comment.Id,
            PostId = comment.PostId,
            UserId = comment.UserId,
            Content = comment.Content,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt,
            UserFirstName = comment.User?.FirstName,
            UserLastName = comment.User?.LastName,
            UserEmail = comment.User?.Email
        };
    }

    private TrainerProfileDto? MapTrainerProfileToDto(TrainerProfile? trainerProfile)
    {
        if (trainerProfile == null) return null;

        return new TrainerProfileDto
        {
            Id = trainerProfile.Id,
            UserId = trainerProfile.UserId,
            Nip = trainerProfile.Nip,
            CompanyName = trainerProfile.CompanyName,
            DisplayName = trainerProfile.DisplayName,
            Address = trainerProfile.Address,
            City = trainerProfile.City,
            PostalCode = trainerProfile.PostalCode,
            AvatarUrl = trainerProfile.AvatarUrl,
            Specializations = trainerProfile.Specializations,
            HourlyRate = trainerProfile.HourlyRate,
            Description = trainerProfile.Description,
            Certifications = trainerProfile.Certifications,
            Languages = trainerProfile.Languages,
            ExperienceYears = trainerProfile.ExperienceYears,
            Rating = trainerProfile.Rating,
            TotalSessions = trainerProfile.TotalSessions,
            AssociatedBusinessIds = trainerProfile.AssociatedBusinessIds,
            CreatedAt = trainerProfile.CreatedAt,
            UpdatedAt = trainerProfile.UpdatedAt,
            Availability = null // Don't include availability in social wall context for performance
        };
    }

    private FacilityDto? MapFacilityToDto(Facility? facility)
    {
        if (facility == null) return null;

        return new FacilityDto
        {
            Id = facility.Id,
            Name = facility.Name,
            Type = facility.Type,
            Description = facility.Description,
            Capacity = facility.Capacity,
            MaxUsers = facility.MaxUsers,
            PricePerUser = facility.PricePerUser,
            PricePerHour = facility.PricePerHour,
            GrossPricePerHour = facility.GrossPricePerHour,
            VatRate = facility.VatRate,
            UserId = facility.UserId,
            Street = facility.Street,
            City = facility.City,
            State = facility.State,
            PostalCode = facility.PostalCode,
            Country = facility.Country,
            AddressLine2 = facility.AddressLine2,
            Latitude = facility.Latitude,
            Longitude = facility.Longitude,
            CreatedAt = facility.CreatedAt,
            UpdatedAt = facility.UpdatedAt,
            Availability = null // Don't include availability in social wall context for performance
        };
    }
    
    public int GetTotalPostsCount()
    {
        return _socialWallPostRepository.GetTotalCount();
    }
}