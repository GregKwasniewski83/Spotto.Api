using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Services.Services;

public class CategorySeedService
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<CategorySeedService> _logger;

    public CategorySeedService(ICategoryRepository categoryRepository, ILogger<CategorySeedService> logger)
    {
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var existing = await _categoryRepository.GetAllAsync(includeInactive: true);
        if (existing.Any())
        {
            _logger.LogInformation("Categories already exist, skipping seed");
            return;
        }

        _logger.LogInformation("Seeding default categories");

        var categories = new List<CreateCategoryDto>
        {
            new()
            {
                Slug = "shooting",
                IsActive = true,
                Translations = new()
                {
                    new() { LanguageCode = "pl", Name = "Strzelectwo", Description = "Sporty strzeleckie" },
                    new() { LanguageCode = "en", Name = "Shooting", Description = "Shooting sports" }
                }
            },
            new()
            {
                Slug = "tennis",
                IsActive = true,
                Translations = new()
                {
                    new() { LanguageCode = "pl", Name = "Tenis", Description = "Tenis ziemny i inne odmiany tenisa" },
                    new() { LanguageCode = "en", Name = "Tennis", Description = "Tennis and its variations" }
                }
            },
            new()
            {
                Slug = "personal-trainer",
                IsActive = true,
                Translations = new()
                {
                    new() { LanguageCode = "pl", Name = "Trener Personalny", Description = "Treningi z trenerem personalnym" },
                    new() { LanguageCode = "en", Name = "Personal Trainer", Description = "Personal training sessions" }
                }
            },
            new()
            {
                Slug = "squash",
                IsActive = true,
                Translations = new()
                {
                    new() { LanguageCode = "pl", Name = "Squash", Description = "Squash i sporty rakietowe" },
                    new() { LanguageCode = "en", Name = "Squash", Description = "Squash and racket sports" }
                }
            }
        };

        foreach (var dto in categories)
        {
            await _categoryRepository.CreateAsync(dto);
            _logger.LogInformation("Seeded category: {Slug}", dto.Slug);
        }

        _logger.LogInformation("Category seeding completed");
    }
}
