using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class TPayDictionaryService : ITPayDictionaryService
{
    private readonly ITPayDictionaryRepository _dictionaryRepository;
    private readonly ITPayService _tpayService;
    private readonly ILogger<TPayDictionaryService> _logger;

    public TPayDictionaryService(
        ITPayDictionaryRepository dictionaryRepository,
        ITPayService tpayService,
        ILogger<TPayDictionaryService> logger)
    {
        _dictionaryRepository = dictionaryRepository;
        _tpayService = tpayService;
        _logger = logger;
    }

    // Dictionary Access
    public async Task<List<TPayLegalFormDto>> GetLegalFormsAsync()
    {
        var legalForms = await _dictionaryRepository.GetAllLegalFormsAsync();
        return legalForms.Select(lf => new TPayLegalFormDto
        {
            Id = lf.Id,
            Name = lf.Name,
            IsActive = lf.IsActive
        }).ToList();
    }

    public async Task<List<TPayCategoryDto>> GetCategoriesAsync()
    {
        var categories = await _dictionaryRepository.GetAllCategoriesAsync();
        return categories.Select(MapCategoryToDto).ToList();
    }

    public async Task<List<TPayCategoryDto>> GetCategoriesByParentAsync(int? parentId)
    {
        var categories = await _dictionaryRepository.GetCategoriesByParentIdAsync(parentId);
        return categories.Select(MapCategoryToDto).ToList();
    }

    public async Task<TPayDictionariesStatusDto> GetSyncStatusAsync()
    {
        var syncHistory = await _dictionaryRepository.GetSyncHistoryAsync();
        
        var statusDto = new TPayDictionariesStatusDto();
        
        var legalFormsSync = syncHistory.FirstOrDefault(s => s.DictionaryType == "LegalForms");
        var categoriesSync = syncHistory.FirstOrDefault(s => s.DictionaryType == "Categories");

        if (legalFormsSync != null)
        {
            statusDto.SyncStatus.Add(new TPayDictionarySyncDto
            {
                DictionaryType = legalFormsSync.DictionaryType,
                LastSyncAt = legalFormsSync.LastSyncAt,
                IsSuccessful = legalFormsSync.IsSuccessful,
                ErrorMessage = legalFormsSync.ErrorMessage,
                RecordsCount = legalFormsSync.RecordsCount
            });
        }

        if (categoriesSync != null)
        {
            statusDto.SyncStatus.Add(new TPayDictionarySyncDto
            {
                DictionaryType = categoriesSync.DictionaryType,
                LastSyncAt = categoriesSync.LastSyncAt,
                IsSuccessful = categoriesSync.IsSuccessful,
                ErrorMessage = categoriesSync.ErrorMessage,
                RecordsCount = categoriesSync.RecordsCount
            });
        }

        return statusDto;
    }

    // Sync Operations
    public async Task<bool> SyncLegalFormsAsync()
    {
        var syncRecord = new TPayDictionarySync
        {
            DictionaryType = "LegalForms",
            LastSyncAt = DateTime.UtcNow,
            IsSuccessful = false,
            RecordsCount = 0
        };

        try
        {
            _logger.LogInformation("Starting TPay legal forms synchronization");

            var response = await _tpayService.GetLegalFormsAsync();
            var legalForms = response.list.Select(item => new TPayLegalForm
            {
                Id = item.id,
                Name = item.name,
                IsActive = true
            }).ToList();

            await _dictionaryRepository.UpsertLegalFormsAsync(legalForms);
            await _dictionaryRepository.DeactivateLegalFormsNotInListAsync(legalForms.Select(lf => lf.Id).ToList());

            syncRecord.IsSuccessful = true;
            syncRecord.RecordsCount = legalForms.Count;
            syncRecord.SyncVersion = response.requestId;

            _logger.LogInformation("TPay legal forms synchronization completed successfully. Synced {Count} records", legalForms.Count);
            return true;
        }
        catch (Exception ex)
        {
            syncRecord.ErrorMessage = ex.Message;
            _logger.LogError(ex, "TPay legal forms synchronization failed");
            return false;
        }
        finally
        {
            await _dictionaryRepository.RecordSyncAttemptAsync(syncRecord);
        }
    }

    public async Task<bool> SyncCategoriesAsync()
    {
        var syncRecord = new TPayDictionarySync
        {
            DictionaryType = "Categories",
            LastSyncAt = DateTime.UtcNow,
            IsSuccessful = false,
            RecordsCount = 0
        };

        try
        {
            _logger.LogInformation("Starting TPay categories synchronization");

            var response = await _tpayService.GetCategoriesAsync();
            var categories = response.list.Select(item => new TPayCategory
            {
                Id = item.id,
                Name = item.name,
                ParentId = item.parentId,
                IsActive = true
            }).ToList();

            await _dictionaryRepository.UpsertCategoriesAsync(categories);
            await _dictionaryRepository.DeactivateCategoriesNotInListAsync(categories.Select(c => c.Id).ToList());

            syncRecord.IsSuccessful = true;
            syncRecord.RecordsCount = categories.Count;
            syncRecord.SyncVersion = response.requestId;

            _logger.LogInformation("TPay categories synchronization completed successfully. Synced {Count} records", categories.Count);
            return true;
        }
        catch (Exception ex)
        {
            syncRecord.ErrorMessage = ex.Message;
            _logger.LogError(ex, "TPay categories synchronization failed");
            return false;
        }
        finally
        {
            await _dictionaryRepository.RecordSyncAttemptAsync(syncRecord);
        }
    }

    public async Task<bool> SyncAllDictionariesAsync()
    {
        var legalFormsResult = await SyncLegalFormsAsync();
        var categoriesResult = await SyncCategoriesAsync();

        return legalFormsResult && categoriesResult;
    }

    public async Task<bool> IsInitialSeedRequiredAsync()
    {
        var legalFormsSync = await _dictionaryRepository.GetLastSyncAsync("LegalForms");
        var categoriesSync = await _dictionaryRepository.GetLastSyncAsync("Categories");

        return legalFormsSync == null || categoriesSync == null ||
               !legalFormsSync.IsSuccessful || !categoriesSync.IsSuccessful;
    }

    public async Task SeedInitialDataAsync()
    {
        _logger.LogInformation("Starting initial TPay dictionaries seed");

        var isRequired = await IsInitialSeedRequiredAsync();
        if (!isRequired)
        {
            _logger.LogInformation("Initial seed not required - dictionaries already synchronized");
            return;
        }

        await SyncAllDictionariesAsync();
        _logger.LogInformation("Initial TPay dictionaries seed completed");
    }

    public async Task PerformScheduledSyncAsync()
    {
        _logger.LogInformation("Starting scheduled TPay dictionaries synchronization");

        try
        {
            // Check if sync is needed (e.g., last successful sync was more than 24 hours ago)
            var legalFormsSync = await _dictionaryRepository.GetLastSyncAsync("LegalForms");
            var categoriesSync = await _dictionaryRepository.GetLastSyncAsync("Categories");

            var needsSync = ShouldPerformSync(legalFormsSync) || ShouldPerformSync(categoriesSync);

            if (needsSync)
            {
                await SyncAllDictionariesAsync();
                _logger.LogInformation("Scheduled TPay dictionaries synchronization completed");
            }
            else
            {
                _logger.LogInformation("Scheduled sync skipped - dictionaries are up to date");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled TPay dictionaries synchronization failed");
        }
    }

    private bool ShouldPerformSync(TPayDictionarySync? lastSync)
    {
        if (lastSync == null || !lastSync.IsSuccessful)
            return true;

        // Sync if last successful sync was more than 24 hours ago
        return DateTime.UtcNow - lastSync.LastSyncAt > TimeSpan.FromHours(24);
    }

    private TPayCategoryDto MapCategoryToDto(TPayCategory category)
    {
        return new TPayCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            ParentId = category.ParentId,
            IsActive = category.IsActive,
            Children = category.Children?.Select(MapCategoryToDto).ToList() ?? new List<TPayCategoryDto>()
        };
    }
}