using Microsoft.Extensions.Logging;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Services.Services;

public class TPayDictionarySeedService
{
    private readonly ITPayDictionaryRepository _dictionaryRepository;
    private readonly ILogger<TPayDictionarySeedService> _logger;

    public TPayDictionarySeedService(ITPayDictionaryRepository dictionaryRepository, ILogger<TPayDictionarySeedService> logger)
    {
        _dictionaryRepository = dictionaryRepository;
        _logger = logger;
    }

    public async Task SeedFallbackDataAsync()
    {
        _logger.LogInformation("Seeding fallback TPay dictionary data");

        await SeedLegalFormsFallbackAsync();
        await SeedCategoriesFallbackAsync();

        _logger.LogInformation("Fallback TPay dictionary data seeding completed");
    }

    private async Task SeedLegalFormsFallbackAsync()
    {
        var existingLegalForms = await _dictionaryRepository.GetAllLegalFormsAsync(activeOnly: false);
        if (existingLegalForms.Any())
        {
            _logger.LogInformation("Legal forms already exist, skipping fallback seed");
            return;
        }

        var fallbackLegalForms = new List<TPayLegalForm>
        {
            new() { Id = 1, Name = "aip/startup", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 2, Name = "działalność gospodarcza", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 3, Name = "fundacja", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 18, Name = "spółka z o.o.", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 12, Name = "spółka akcyjna", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 14, Name = "spółka cywilna", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 24, Name = "spółka jawna", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 23, Name = "inna", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        await _dictionaryRepository.UpsertLegalFormsAsync(fallbackLegalForms);

        var syncRecord = new TPayDictionarySync
        {
            DictionaryType = "LegalForms",
            LastSyncAt = DateTime.UtcNow,
            IsSuccessful = true,
            RecordsCount = fallbackLegalForms.Count,
            SyncVersion = "FALLBACK_SEED"
        };

        await _dictionaryRepository.RecordSyncAttemptAsync(syncRecord);
        
        _logger.LogInformation("Seeded {Count} fallback legal forms", fallbackLegalForms.Count);
    }

    private async Task SeedCategoriesFallbackAsync()
    {
        var existingCategories = await _dictionaryRepository.GetAllCategoriesAsync(activeOnly: false);
        if (existingCategories.Any())
        {
            _logger.LogInformation("Categories already exist, skipping fallback seed");
            return;
        }

        var fallbackCategories = new List<TPayCategory>
        {
            new() { Id = 60, Name = "Sklepy  usługi sportowe", ParentId = null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 61, Name = "Zdrowie i uroda", ParentId = null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 62, Name = "Rozrywka", ParentId = null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 74, Name = "Kursy i szkolenia", ParentId = null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 78, Name = "Usługi finansowe", ParentId = null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 84, Name = "Usługi turystyczne", ParentId = null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new() { Id = 83, Name = "Inne", ParentId = null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        await _dictionaryRepository.UpsertCategoriesAsync(fallbackCategories);

        var syncRecord = new TPayDictionarySync
        {
            DictionaryType = "Categories",
            LastSyncAt = DateTime.UtcNow,
            IsSuccessful = true,
            RecordsCount = fallbackCategories.Count,
            SyncVersion = "FALLBACK_SEED"
        };

        await _dictionaryRepository.RecordSyncAttemptAsync(syncRecord);
        
        _logger.LogInformation("Seeded {Count} fallback categories", fallbackCategories.Count);
    }
}