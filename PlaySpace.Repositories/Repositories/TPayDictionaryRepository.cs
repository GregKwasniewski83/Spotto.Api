using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class TPayDictionaryRepository : ITPayDictionaryRepository
{
    private readonly PlaySpaceDbContext _context;

    public TPayDictionaryRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    // Legal Forms
    public async Task<List<TPayLegalForm>> GetAllLegalFormsAsync(bool activeOnly = true)
    {
        var query = _context.TPayLegalForms.AsQueryable();
        
        if (activeOnly)
        {
            query = query.Where(lf => lf.IsActive);
        }
        
        return await query.OrderBy(lf => lf.Name).ToListAsync();
    }

    public async Task<TPayLegalForm?> GetLegalFormByIdAsync(int id)
    {
        return await _context.TPayLegalForms.FindAsync(id);
    }

    public async Task UpsertLegalFormsAsync(List<TPayLegalForm> legalForms)
    {
        foreach (var legalForm in legalForms)
        {
            var existing = await _context.TPayLegalForms.FindAsync(legalForm.Id);
            
            if (existing == null)
            {
                legalForm.CreatedAt = DateTime.UtcNow;
                legalForm.UpdatedAt = DateTime.UtcNow;
                legalForm.LastSyncedAt = DateTime.UtcNow;
                _context.TPayLegalForms.Add(legalForm);
            }
            else
            {
                existing.Name = legalForm.Name;
                existing.IsActive = legalForm.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.LastSyncedAt = DateTime.UtcNow;
            }
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task DeactivateLegalFormsNotInListAsync(List<int> activeIds)
    {
        await _context.TPayLegalForms
            .Where(lf => !activeIds.Contains(lf.Id))
            .ExecuteUpdateAsync(lf => lf
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
    }

    // Categories
    public async Task<List<TPayCategory>> GetAllCategoriesAsync(bool activeOnly = true)
    {
        var query = _context.TPayCategories
            .Include(c => c.Children)
            .AsQueryable();
        
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }
        
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<List<TPayCategory>> GetCategoriesByParentIdAsync(int? parentId, bool activeOnly = true)
    {
        var query = _context.TPayCategories
            .Where(c => c.ParentId == parentId);
        
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }
        
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<TPayCategory?> GetCategoryByIdAsync(int id)
    {
        return await _context.TPayCategories
            .Include(c => c.Parent)
            .Include(c => c.Children)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task UpsertCategoriesAsync(List<TPayCategory> categories)
    {
        foreach (var category in categories)
        {
            var existing = await _context.TPayCategories.FindAsync(category.Id);
            
            if (existing == null)
            {
                category.CreatedAt = DateTime.UtcNow;
                category.UpdatedAt = DateTime.UtcNow;
                category.LastSyncedAt = DateTime.UtcNow;
                _context.TPayCategories.Add(category);
            }
            else
            {
                existing.Name = category.Name;
                existing.ParentId = category.ParentId;
                existing.IsActive = category.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.LastSyncedAt = DateTime.UtcNow;
            }
        }
        
        await _context.SaveChangesAsync();
    }

    public async Task DeactivateCategoriesNotInListAsync(List<int> activeIds)
    {
        await _context.TPayCategories
            .Where(c => !activeIds.Contains(c.Id))
            .ExecuteUpdateAsync(c => c
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.UpdatedAt, DateTime.UtcNow));
    }

    // Sync Management
    public async Task<TPayDictionarySync?> GetLastSyncAsync(string dictionaryType)
    {
        return await _context.TPayDictionarySyncs
            .Where(s => s.DictionaryType == dictionaryType)
            .OrderByDescending(s => s.LastSyncAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<TPayDictionarySync>> GetSyncHistoryAsync(string? dictionaryType = null, int take = 10)
    {
        var query = _context.TPayDictionarySyncs.AsQueryable();
        
        if (!string.IsNullOrEmpty(dictionaryType))
        {
            query = query.Where(s => s.DictionaryType == dictionaryType);
        }
        
        return await query
            .OrderByDescending(s => s.LastSyncAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task RecordSyncAttemptAsync(TPayDictionarySync syncRecord)
    {
        syncRecord.Id = Guid.NewGuid();
        _context.TPayDictionarySyncs.Add(syncRecord);
        await _context.SaveChangesAsync();
    }
}