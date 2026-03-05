using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IExternalAuthRepository
{
    Task<ExternalAuth?> GetByProviderAndExternalUserIdAsync(AuthProvider provider, string externalUserId);
    Task<ExternalAuth?> GetByProviderAndEmailAsync(AuthProvider provider, string email);
    Task<List<ExternalAuth>> GetByUserIdAsync(Guid userId);
    Task<ExternalAuth> CreateAsync(ExternalAuth externalAuth);
    Task<ExternalAuth?> UpdateAsync(ExternalAuth externalAuth);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> DeleteByUserIdAndProviderAsync(Guid userId, AuthProvider provider);
}