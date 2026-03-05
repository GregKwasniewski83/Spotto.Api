using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SocialWallPostController : ControllerBase
{
    private readonly ISocialWallPostService _socialWallPostService;

    public SocialWallPostController(ISocialWallPostService socialWallPostService)
    {
        _socialWallPostService = socialWallPostService;
    }

    [HttpPost]
    public ActionResult<SocialWallPostDto> CreatePost([FromBody] CreateSocialWallPostDto postDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var post = _socialWallPostService.CreatePost(postDto, userId);
            return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the post", error = ex.Message });
        }
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public ActionResult<SocialWallPostDto> GetPost(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        Guid? currentUserId = null;
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            currentUserId = userId;
        }

        var post = _socialWallPostService.GetPost(id, currentUserId);
        if (post == null)
        {
            return NotFound("Post not found");
        }

        return Ok(post);
    }

    [HttpPut("{id}")]
    public ActionResult<SocialWallPostDto> UpdatePost(Guid id, [FromBody] UpdateSocialWallPostDto postDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var post = _socialWallPostService.UpdatePost(id, postDto, userId);
            if (post == null)
            {
                return NotFound("Post not found or you don't have permission to update it");
            }

            return Ok(post);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the post", error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public ActionResult DeletePost(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var success = _socialWallPostService.DeletePost(id, userId);
            if (!success)
            {
                return NotFound("Post not found or you don't have permission to delete it");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the post", error = ex.Message });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<List<SocialWallPostDto>> GetPosts([FromQuery] SocialWallPostSearchDto searchDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        Guid? currentUserId = null;
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            currentUserId = userId;
        }

        try
        {
            var posts = _socialWallPostService.GetPosts(searchDto, currentUserId);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving posts", error = ex.Message });
        }
    }

    [HttpGet("user/{userId}")]
    [AllowAnonymous]
    public ActionResult<List<SocialWallPostDto>> GetUserPosts(Guid userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        Guid? currentUserId = null;
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var currentUserIdParsed))
        {
            currentUserId = currentUserIdParsed;
        }

        try
        {
            var posts = _socialWallPostService.GetUserPosts(userId, currentUserId);
            return Ok(posts);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving user posts", error = ex.Message });
        }
    }

    [HttpPost("{id}/like")]
    public ActionResult ToggleLike(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var isLiked = _socialWallPostService.ToggleLike(id, userId);
            return Ok(new { isLiked, message = isLiked ? "Post liked" : "Post unliked" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while toggling like", error = ex.Message });
        }
    }

    [HttpGet("{id}/likes")]
    [AllowAnonymous]
    public ActionResult<List<SocialWallPostLikeDto>> GetPostLikes(Guid id)
    {
        try
        {
            var likes = _socialWallPostService.GetPostLikes(id);
            return Ok(likes);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving post likes", error = ex.Message });
        }
    }

    [HttpPost("{id}/comments")]
    public ActionResult<SocialWallPostCommentDto> CreateComment(Guid id, [FromBody] CreateSocialWallPostCommentDto commentDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var comment = _socialWallPostService.CreateComment(id, userId, commentDto);
            return CreatedAtAction(nameof(GetComment), new { id = comment.Id }, comment);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while creating the comment", error = ex.Message });
        }
    }

    [HttpGet("comments/{id}")]
    [AllowAnonymous]
    public ActionResult<SocialWallPostCommentDto> GetComment(Guid id)
    {
        var comment = _socialWallPostService.GetComment(id);
        if (comment == null)
        {
            return NotFound("Comment not found");
        }

        return Ok(comment);
    }

    [HttpPut("comments/{id}")]
    public ActionResult<SocialWallPostCommentDto> UpdateComment(Guid id, [FromBody] UpdateSocialWallPostCommentDto commentDto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var comment = _socialWallPostService.UpdateComment(id, commentDto, userId);
            if (comment == null)
            {
                return NotFound("Comment not found or you don't have permission to update it");
            }

            return Ok(comment);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while updating the comment", error = ex.Message });
        }
    }

    [HttpDelete("comments/{id}")]
    public ActionResult DeleteComment(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized("User ID not found in token");
        }

        try
        {
            var success = _socialWallPostService.DeleteComment(id, userId);
            if (!success)
            {
                return NotFound("Comment not found or you don't have permission to delete it");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while deleting the comment", error = ex.Message });
        }
    }

    [HttpGet("{id}/comments")]
    [AllowAnonymous]
    public ActionResult<List<SocialWallPostCommentDto>> GetPostComments(Guid id)
    {
        try
        {
            var comments = _socialWallPostService.GetPostComments(id);
            return Ok(comments);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while retrieving post comments", error = ex.Message });
        }
    }
}