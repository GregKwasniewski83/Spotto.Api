using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly PlaySpaceDbContext _context;

    public CategoryRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Categories
            .Include(c => c.Translations)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(c => c.IsActive);

        return await query.OrderBy(c => c.Slug).ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(Guid id)
    {
        return await _context.Categories
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Category?> GetBySlugAsync(string slug)
    {
        return await _context.Categories
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Slug == slug);
    }

    public async Task<Category> CreateAsync(CreateCategoryDto dto)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Slug = dto.Slug.ToLower().Trim(),
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);

        foreach (var translationDto in dto.Translations)
        {
            _context.CategoryTranslations.Add(new CategoryTranslation
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                LanguageCode = translationDto.LanguageCode.ToLower().Trim(),
                Name = translationDto.Name,
                Description = translationDto.Description
            });
        }

        await _context.SaveChangesAsync();
        return (await GetByIdAsync(category.Id))!;
    }

    public async Task<Category?> UpdateAsync(Guid id, UpdateCategoryDto dto)
    {
        var category = await _context.Categories
            .Include(c => c.Translations)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
            return null;

        category.Slug = dto.Slug.ToLower().Trim();
        category.IsActive = dto.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        // Replace all translations
        _context.CategoryTranslations.RemoveRange(category.Translations);

        foreach (var translationDto in dto.Translations)
        {
            _context.CategoryTranslations.Add(new CategoryTranslation
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                LanguageCode = translationDto.LanguageCode.ToLower().Trim(),
                Name = translationDto.Name,
                Description = translationDto.Description
            });
        }

        await _context.SaveChangesAsync();
        return (await GetByIdAsync(category.Id))!;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return false;

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null)
    {
        var slugLower = slug.ToLower().Trim();
        var query = _context.Categories.Where(c => c.Slug == slugLower);

        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync();
    }
}
