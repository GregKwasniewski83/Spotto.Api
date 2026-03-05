namespace PlaySpace.Domain.DTOs;

public class CategoryTranslationDto
{
    public required string LanguageCode { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public bool IsActive { get; set; }
    public List<CategoryTranslationDto> Translations { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateCategoryDto
{
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CategoryTranslationDto> Translations { get; set; } = new();
}

public class UpdateCategoryDto
{
    public required string Slug { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CategoryTranslationDto> Translations { get; set; } = new();
}
