using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class ExternalProviderService : IExternalProviderService
{
    private readonly IGoogleAuthService _googleAuthService;
    private readonly IAppleAuthService _appleAuthService;

    public ExternalProviderService(IGoogleAuthService googleAuthService, IAppleAuthService appleAuthService)
    {
        _googleAuthService = googleAuthService;
        _appleAuthService = appleAuthService;
    }

    public async Task<ExternalUserInfo> VerifyTokenAsync(string provider, string idToken)
    {
        var authProvider = ParseProvider(provider);
        
        return authProvider switch
        {
            AuthProvider.Google => await _googleAuthService.VerifyGoogleTokenAsync(idToken),
            AuthProvider.Apple => await _appleAuthService.VerifyAppleTokenAsync(idToken),
            _ => throw new ArgumentException($"Unsupported authentication provider: {provider}")
        };
    }

    public AuthProvider ParseProvider(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => AuthProvider.Google,
            "apple" => AuthProvider.Apple,
            _ => throw new ArgumentException($"Unsupported provider: {provider}")
        };
    }
}