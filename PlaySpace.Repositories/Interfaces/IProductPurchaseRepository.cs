using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IProductPurchaseRepository
{
    Task<ProductPurchase> CreateAsync(ProductPurchase purchase);
    Task<ProductPurchase?> GetByIdAsync(Guid id);
    Task<ProductPurchase?> GetByPaymentIdAsync(Guid paymentId);
    Task<List<ProductPurchase>> GetUserPurchasesAsync(Guid userId);
    Task<ProductPurchase> UpdateAsync(ProductPurchase purchase);
    Task<ProductPurchase?> FindByPrefixAndEmailAsync(string idPrefix, string userEmail);

    // Agent methods
    Task<(List<ProductPurchase> purchases, int totalCount)> GetBusinessPurchasesAsync(
        Guid businessProfileId,
        AgentProductPurchaseFilterDto filter);

    // Sales reporting
    Task<List<ProductPurchase>> GetMonthlyPurchasesForBusinessAsync(Guid businessProfileId, int year, int month);
}
