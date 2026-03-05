using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IProductUsageLogRepository
{
    Task<ProductUsageLog> CreateAsync(ProductUsageLog log);
    Task<List<ProductUsageLog>> GetByPurchaseIdAsync(Guid purchaseId);
}
