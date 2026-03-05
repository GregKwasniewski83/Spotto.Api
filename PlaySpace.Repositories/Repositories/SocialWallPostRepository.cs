using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories;

public class SocialWallPostRepository : ISocialWallPostRepository
{
    private readonly PlaySpaceDbContext _context;

    public SocialWallPostRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public SocialWallPost CreatePost(CreateSocialWallPostDto postDto, Guid authorId)
    {
        var post = new SocialWallPost
        {
            Id = Guid.NewGuid(),
            Type = postDto.Type,
            Title = postDto.Title,
            Content = postDto.Content,
            AuthorId = authorId,
            ReservationId = postDto.ReservationId,
            TrainingId = postDto.TrainingId,
            ImageUrl = postDto.ImageUrl,
            Tags = postDto.Tags ?? new List<string>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SocialWallPosts.Add(post);
        _context.SaveChanges();
        
        return GetPost(post.Id)!;
    }

    public SocialWallPost? GetPost(Guid id)
    {
        return _context.SocialWallPosts
            .Include(p => p.Author)
            .Include(p => p.Reservation)
                .ThenInclude(r => r.Facility)
            .Include(p => p.Reservation)
                .ThenInclude(r => r.TrainerProfile)
            .Include(p => p.Training)
                .ThenInclude(t => t.TrainerProfile)
            .Include(p => p.Training)
                .ThenInclude(t => t.Facility)
            .Include(p => p.Likes)
                .ThenInclude(l => l.User)
            .Include(p => p.Comments)
                .ThenInclude(c => c.User)
            .FirstOrDefault(p => p.Id == id);
    }

    public SocialWallPost? UpdatePost(Guid id, UpdateSocialWallPostDto postDto, Guid authorId)
    {
        var post = _context.SocialWallPosts.FirstOrDefault(p => p.Id == id && p.AuthorId == authorId);
        if (post == null)
            return null;

        post.Type = postDto.Type;
        post.Title = postDto.Title;
        post.Content = postDto.Content;
        post.ReservationId = postDto.ReservationId;
        post.TrainingId = postDto.TrainingId;
        post.ImageUrl = postDto.ImageUrl;
        post.Tags = postDto.Tags ?? new List<string>();
        post.IsActive = postDto.IsActive;
        post.UpdatedAt = DateTime.UtcNow;

        _context.SaveChanges();
        return GetPost(id);
    }

    public bool DeletePost(Guid id, Guid authorId)
    {
        var post = _context.SocialWallPosts.FirstOrDefault(p => p.Id == id && p.AuthorId == authorId);
        if (post == null)
            return false;

        _context.SocialWallPosts.Remove(post);
        _context.SaveChanges();
        return true;
    }

    public List<SocialWallPost> GetPosts(SocialWallPostSearchDto searchDto)
    {
        var query = _context.SocialWallPosts
            .Include(p => p.Author)
            .Include(p => p.Reservation)
                .ThenInclude(r => r.Facility)
            .Include(p => p.Reservation)
                .ThenInclude(r => r.TrainerProfile)
            .Include(p => p.Training)
                .ThenInclude(t => t.TrainerProfile)
            .Include(p => p.Training)
                .ThenInclude(t => t.Facility)
            .Include(p => p.Likes)
                .ThenInclude(l => l.User)
            .Include(p => p.Comments)
                .ThenInclude(c => c.User)
            .AsQueryable();

        // Apply filters
        if (searchDto.IsActive.HasValue)
            query = query.Where(p => p.IsActive == searchDto.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(searchDto.Type))
            query = query.Where(p => p.Type.ToLower() == searchDto.Type.ToLower());

        if (!string.IsNullOrWhiteSpace(searchDto.Search))
            query = query.Where(p => 
                p.Title.ToLower().Contains(searchDto.Search.ToLower()) ||
                p.Content.ToLower().Contains(searchDto.Search.ToLower()));

        if (searchDto.Tags != null && searchDto.Tags.Any())
            query = query.Where(p => p.Tags != null && searchDto.Tags.Any(tag => p.Tags.Contains(tag)));

        if (searchDto.AuthorId.HasValue)
            query = query.Where(p => p.AuthorId == searchDto.AuthorId.Value);

        if (searchDto.FromDate.HasValue)
        {
            var fromDate = DateTime.SpecifyKind(searchDto.FromDate.Value, DateTimeKind.Utc);
            query = query.Where(p => p.CreatedAt >= fromDate);
        }

        if (searchDto.ToDate.HasValue)
        {
            var toDate = DateTime.SpecifyKind(searchDto.ToDate.Value, DateTimeKind.Utc);
            query = query.Where(p => p.CreatedAt <= toDate);
        }

        // Apply pagination
        var skip = (searchDto.Page - 1) * searchDto.PageSize;
        
        return query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(searchDto.PageSize)
            .ToList();
    }

    public List<SocialWallPost> GetUserPosts(Guid userId)
    {
        return _context.SocialWallPosts
            .Include(p => p.Author)
            .Include(p => p.Reservation)
                .ThenInclude(r => r.Facility)
            .Include(p => p.Reservation)
                .ThenInclude(r => r.TrainerProfile)
            .Include(p => p.Training)
                .ThenInclude(t => t.TrainerProfile)
            .Include(p => p.Training)
                .ThenInclude(t => t.Facility)
            .Include(p => p.Likes)
                .ThenInclude(l => l.User)
            .Include(p => p.Comments)
                .ThenInclude(c => c.User)
            .Where(p => p.AuthorId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    public SocialWallPostLike? ToggleLike(Guid postId, Guid userId)
    {
        var existingLike = _context.SocialWallPostLikes
            .FirstOrDefault(l => l.PostId == postId && l.UserId == userId);

        if (existingLike != null)
        {
            _context.SocialWallPostLikes.Remove(existingLike);
            _context.SaveChanges();
            return null; // Like removed
        }
        else
        {
            var newLike = new SocialWallPostLike
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.SocialWallPostLikes.Add(newLike);
            _context.SaveChanges();
            return newLike;
        }
    }

    public bool IsLikedByUser(Guid postId, Guid userId)
    {
        return _context.SocialWallPostLikes
            .Any(l => l.PostId == postId && l.UserId == userId);
    }

    public List<SocialWallPostLike> GetPostLikes(Guid postId)
    {
        return _context.SocialWallPostLikes
            .Include(l => l.User)
            .Where(l => l.PostId == postId)
            .OrderByDescending(l => l.CreatedAt)
            .ToList();
    }

    public SocialWallPostComment CreateComment(Guid postId, Guid userId, string content)
    {
        var comment = new SocialWallPostComment
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            UserId = userId,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SocialWallPostComments.Add(comment);
        _context.SaveChanges();
        
        return GetComment(comment.Id)!;
    }

    public SocialWallPostComment? GetComment(Guid id)
    {
        return _context.SocialWallPostComments
            .Include(c => c.User)
            .Include(c => c.Post)
            .FirstOrDefault(c => c.Id == id);
    }

    public SocialWallPostComment? UpdateComment(Guid id, string content, Guid userId)
    {
        var comment = _context.SocialWallPostComments
            .FirstOrDefault(c => c.Id == id && c.UserId == userId);
        
        if (comment == null)
            return null;

        comment.Content = content;
        comment.UpdatedAt = DateTime.UtcNow;
        
        _context.SaveChanges();
        return GetComment(id);
    }

    public bool DeleteComment(Guid id, Guid userId)
    {
        var comment = _context.SocialWallPostComments
            .FirstOrDefault(c => c.Id == id && c.UserId == userId);
        
        if (comment == null)
            return false;

        _context.SocialWallPostComments.Remove(comment);
        _context.SaveChanges();
        return true;
    }

    public List<SocialWallPostComment> GetPostComments(Guid postId)
    {
        return _context.SocialWallPostComments
            .Include(c => c.User)
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .ToList();
    }
    
    public int GetTotalCount()
    {
        return _context.SocialWallPosts.Count();
    }
}