using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface ISocialWallPostRepository
{
    // Post operations
    SocialWallPost CreatePost(CreateSocialWallPostDto postDto, Guid authorId);
    SocialWallPost? GetPost(Guid id);
    SocialWallPost? UpdatePost(Guid id, UpdateSocialWallPostDto postDto, Guid authorId);
    bool DeletePost(Guid id, Guid authorId);
    List<SocialWallPost> GetPosts(SocialWallPostSearchDto searchDto);
    List<SocialWallPost> GetUserPosts(Guid userId);
    
    // Like operations
    SocialWallPostLike? ToggleLike(Guid postId, Guid userId);
    bool IsLikedByUser(Guid postId, Guid userId);
    List<SocialWallPostLike> GetPostLikes(Guid postId);
    
    // Comment operations
    SocialWallPostComment CreateComment(Guid postId, Guid userId, string content);
    SocialWallPostComment? GetComment(Guid id);
    SocialWallPostComment? UpdateComment(Guid id, string content, Guid userId);
    bool DeleteComment(Guid id, Guid userId);
    List<SocialWallPostComment> GetPostComments(Guid postId);
    
    // Stats methods
    int GetTotalCount();
}