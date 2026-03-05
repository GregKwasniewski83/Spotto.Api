using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface ITPayDictionaryRepository
{
    // Legal Forms
    Task<List<TPayLegalForm>> GetAllLegalFormsAsync(bool activeOnly = true);
    Task<TPayLegalForm?> GetLegalFormByIdAsync(int id);
    Task UpsertLegalFormsAsync(List<TPayLegalForm> legalForms);
    Task DeactivateLegalFormsNotInListAsync(List<int> activeIds);

    // Categories  
    Task<List<TPayCategory>> GetAllCategoriesAsync(bool activeOnly = true);
    Task<List<TPayCategory>> GetCategoriesByParentIdAsync(int? parentId, bool activeOnly = true);
    Task<TPayCategory?> GetCategoryByIdAsync(int id);
    Task UpsertCategoriesAsync(List<TPayCategory> categories);
    Task DeactivateCategoriesNotInListAsync(List<int> activeIds);

    // Sync Management
    Task<TPayDictionarySync?> GetLastSyncAsync(string dictionaryType);
    Task<List<TPayDictionarySync>> GetSyncHistoryAsync(string? dictionaryType = null, int take = 10);
    Task RecordSyncAttemptAsync(TPayDictionarySync syncRecord);
}