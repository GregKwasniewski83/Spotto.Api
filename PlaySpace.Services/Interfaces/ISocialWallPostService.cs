using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface ISocialWallPostService
{
    // Post operations
    SocialWallPostDto CreatePost(CreateSocialWallPostDto postDto, Guid authorId);
    SocialWallPostDto? GetPost(Guid id, Guid? currentUserId = null);
    SocialWallPostDto? UpdatePost(Guid id, UpdateSocialWallPostDto postDto, Guid authorId);
    bool DeletePost(Guid id, Guid authorId);
    List<SocialWallPostDto> GetPosts(SocialWallPostSearchDto searchDto, Guid? currentUserId = null);
    List<SocialWallPostDto> GetUserPosts(Guid userId, Guid? currentUserId = null);
    
    // Like operations
    bool ToggleLike(Guid postId, Guid userId);
    List<SocialWallPostLikeDto> GetPostLikes(Guid postId);
    
    // Comment operations
    SocialWallPostCommentDto CreateComment(Guid postId, Guid userId, CreateSocialWallPostCommentDto commentDto);
    SocialWallPostCommentDto? GetComment(Guid id);
    SocialWallPostCommentDto? UpdateComment(Guid id, UpdateSocialWallPostCommentDto commentDto, Guid userId);
    bool DeleteComment(Guid id, Guid userId);
    List<SocialWallPostCommentDto> GetPostComments(Guid postId);
    
    // Stats methods
    int GetTotalPostsCount();
}