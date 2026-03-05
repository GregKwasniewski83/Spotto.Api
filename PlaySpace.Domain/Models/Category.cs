namespace PlaySpace.Domain.Models;

public class Category
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }  // URL-friendly identifier, e.g. "fitness", "yoga"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<CategoryTranslation> Translations { get; set; } = new();
}

public class CategoryTranslation
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public required string LanguageCode { get; set; }  // e.g. "pl", "en", "de"
    public required string Name { get; set; }
    public string? Description { get; set; }

    public Category? Category { get; set; }
}
