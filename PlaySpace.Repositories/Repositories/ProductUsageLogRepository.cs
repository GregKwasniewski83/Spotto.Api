using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class ProductUsageLogRepository : IProductUsageLogRepository
{
    private readonly PlaySpaceDbContext _context;

    public ProductUsageLogRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<ProductUsageLog> CreateAsync(ProductUsageLog log)
    {
        _context.ProductUsageLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<List<ProductUsageLog>> GetByPurchaseIdAsync(Guid purchaseId)
    {
        return await _context.ProductUsageLogs
            .Include(l => l.Facility)
            .Where(l => l.ProductPurchaseId == purchaseId)
            .OrderByDescending(l => l.UsageDate)
            .ToListAsync();
    }
}
