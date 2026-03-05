using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces;

public interface ITPayDictionaryService
{
    // Dictionary Access
    Task<List<TPayLegalFormDto>> GetLegalFormsAsync();
    Task<List<TPayCategoryDto>> GetCategoriesAsync();
    Task<List<TPayCategoryDto>> GetCategoriesByParentAsync(int? parentId);
    Task<TPayDictionariesStatusDto> GetSyncStatusAsync();

    // Sync Operations
    Task<bool> SyncLegalFormsAsync();
    Task<bool> SyncCategoriesAsync();
    Task<bool> SyncAllDictionariesAsync();
    Task<bool> IsInitialSeedRequiredAsync();
    Task SeedInitialDataAsync();

    // Background/Scheduled Operations
    Task PerformScheduledSyncAsync();
}