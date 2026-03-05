using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<List<CategoryDto>> GetAllAsync(bool includeInactive = false)
    {
        var categories = await _categoryRepository.GetAllAsync(includeInactive);
        return categories.Select(MapToDto).ToList();
    }

    public async Task<CategoryDto?> GetByIdAsync(Guid id)
    {
        var category = await _categoryRepository.GetByIdAsync(id);
        return category == null ? null : MapToDto(category);
    }

    public async Task<CategoryDto?> GetBySlugAsync(string slug)
    {
        var category = await _categoryRepository.GetBySlugAsync(slug);
        return category == null ? null : MapToDto(category);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto)
    {
        if (await _categoryRepository.SlugExistsAsync(dto.Slug))
            throw new ValidationException($"A category with slug '{dto.Slug}' already exists");

        var category = await _categoryRepository.CreateAsync(dto);
        return MapToDto(category);
    }

    public async Task<CategoryDto?> UpdateAsync(Guid id, UpdateCategoryDto dto)
    {
        if (await _categoryRepository.SlugExistsAsync(dto.Slug, excludeId: id))
            throw new ValidationException($"A category with slug '{dto.Slug}' already exists");

        var category = await _categoryRepository.UpdateAsync(id, dto);
        return category == null ? null : MapToDto(category);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        return await _categoryRepository.DeleteAsync(id);
    }

    private static CategoryDto MapToDto(Category category)
    {
        return new CategoryDto
        {
            Id = category.Id,
            Slug = category.Slug,
            IsActive = category.IsActive,
            Translations = category.Translations.Select(t => new CategoryTranslationDto
            {
                LanguageCode = t.LanguageCode,
                Name = t.Name,
                Description = t.Description
            }).ToList(),
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }
}
