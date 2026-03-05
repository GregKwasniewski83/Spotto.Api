using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class ProductPurchaseRepository : IProductPurchaseRepository
{
    private readonly PlaySpaceDbContext _context;

    public ProductPurchaseRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<ProductPurchase> CreateAsync(ProductPurchase purchase)
    {
        _context.ProductPurchases.Add(purchase);
        await _context.SaveChangesAsync();
        return purchase;
    }

    public async Task<ProductPurchase?> GetByIdAsync(Guid id)
    {
        return await _context.ProductPurchases
            .Include(p => p.Product)
                .ThenInclude(pr => pr!.BusinessProfile)
            .Include(p => p.User)
            .Include(p => p.Payment)
            .Include(p => p.UsageLogs)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<ProductPurchase?> GetByPaymentIdAsync(Guid paymentId)
    {
        return await _context.ProductPurchases
            .Include(p => p.Product)
                .ThenInclude(pr => pr!.BusinessProfile)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
    }

    public async Task<List<ProductPurchase>> GetUserPurchasesAsync(Guid userId)
    {
        return await _context.ProductPurchases
            .Include(p => p.Product)
                .ThenInclude(pr => pr!.BusinessProfile)
            .Include(p => p.Payment)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();
    }

    public async Task<ProductPurchase> UpdateAsync(ProductPurchase purchase)
    {
        purchase.UpdatedAt = DateTime.UtcNow;
        _context.ProductPurchases.Update(purchase);
        await _context.SaveChangesAsync();
        return purchase;
    }

    public async Task<ProductPurchase?> FindByPrefixAndEmailAsync(string idPrefix, string userEmail)
    {
        // Normalize inputs
        var prefixLower = idPrefix.ToLower().Trim();
        var emailLower = userEmail.ToLower().Trim();

        // First get all purchases for this email (EF can't translate Id.ToString().StartsWith() to SQL)
        var userPurchases = await _context.ProductPurchases
            .Include(p => p.Product)
                .ThenInclude(pr => pr!.BusinessProfile)
            .Include(p => p.User)
            .Where(p => p.User != null && p.User.Email.ToLower() == emailLower)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync();

        // Filter in memory by ID prefix
        return userPurchases.FirstOrDefault(p =>
            p.Id.ToString().ToLower().StartsWith(prefixLower));
    }

    public async Task<(List<ProductPurchase> purchases, int totalCount)> GetBusinessPurchasesAsync(
        Guid businessProfileId,
        AgentProductPurchaseFilterDto filter)
    {
        var query = _context.ProductPurchases
            .Include(p => p.User)
            .Include(p => p.Product)
            .Where(p => p.BusinessProfileId == businessProfileId);

        // Apply status filter
        if (!string.IsNullOrEmpty(filter.Status))
        {
            query = query.Where(p => p.Status == filter.Status);
        }

        // Apply date filters
        if (filter.ExpiryDateFrom.HasValue)
        {
            var expiryFrom = DateTime.SpecifyKind(filter.ExpiryDateFrom.Value.Date, DateTimeKind.Utc);
            query = query.Where(p => p.ExpiryDate >= expiryFrom);
        }

        if (filter.ExpiryDateTo.HasValue)
        {
            var expiryTo = DateTime.SpecifyKind(filter.ExpiryDateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(p => p.ExpiryDate < expiryTo);
        }

        if (filter.PurchaseDateFrom.HasValue)
        {
            var purchaseFrom = DateTime.SpecifyKind(filter.PurchaseDateFrom.Value.Date, DateTimeKind.Utc);
            query = query.Where(p => p.PurchaseDate >= purchaseFrom);
        }

        if (filter.PurchaseDateTo.HasValue)
        {
            var purchaseTo = DateTime.SpecifyKind(filter.PurchaseDateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(p => p.PurchaseDate < purchaseTo);
        }

        // Apply customer filters
        if (!string.IsNullOrEmpty(filter.CustomerEmail))
        {
            var emailLower = filter.CustomerEmail.ToLower();
            query = query.Where(p => p.User != null && p.User.Email.ToLower().Contains(emailLower));
        }

        if (!string.IsNullOrEmpty(filter.CustomerName))
        {
            var nameLower = filter.CustomerName.ToLower();
            query = query.Where(p => p.User != null &&
                (p.User.FirstName.ToLower().Contains(nameLower) ||
                 p.User.LastName.ToLower().Contains(nameLower) ||
                 (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(nameLower)));
        }

        // Apply product filter
        if (filter.ProductId.HasValue)
        {
            query = query.Where(p => p.ProductId == filter.ProductId.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination
        var pageSize = Math.Max(1, Math.Min(filter.PageSize, 100)); // Limit to 100 max
        var page = Math.Max(1, filter.Page);
        var skip = (page - 1) * pageSize;

        var purchases = await query
            .OrderByDescending(p => p.PurchaseDate)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return (purchases, totalCount);
    }

    public async Task<List<ProductPurchase>> GetMonthlyPurchasesForBusinessAsync(Guid businessProfileId, int year, int month)
    {
        return await _context.ProductPurchases
            .Where(p => p.BusinessProfileId == businessProfileId
                     && p.PurchaseDate.Year == year
                     && p.PurchaseDate.Month == month)
            .ToListAsync();
    }
}
