using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces;

public interface IExternalAuthService
{
    Task<AuthResponse> ExternalLoginAsync(ExternalLoginRequest request);
    Task<AuthResponse> LinkExternalAccountAsync(Guid userId, LinkExternalAccountRequest request);
    Task<bool> UnlinkExternalAccountAsync(Guid userId, string provider);
    Task<List<ExternalAuthDto>> GetUserExternalAccountsAsync(Guid userId);
}

public interface IGoogleAuthService
{
    Task<ExternalUserInfo> VerifyGoogleTokenAsync(string idToken);
}

public interface IAppleAuthService  
{
    Task<ExternalUserInfo> VerifyAppleTokenAsync(string idToken);
}

public interface IExternalProviderService
{
    Task<ExternalUserInfo> VerifyTokenAsync(string provider, string idToken);
    AuthProvider ParseProvider(string provider);
}