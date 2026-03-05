using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class ExternalAuthService : IExternalAuthService
{
    private readonly IExternalProviderService _providerService;
    private readonly IExternalAuthRepository _externalAuthRepository;
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;
    private readonly AuthService _authService;
    private readonly ILogger<ExternalAuthService> _logger;

    public ExternalAuthService(
        IExternalProviderService providerService,
        IExternalAuthRepository externalAuthRepository,
        IUserService userService,
        IRoleService roleService,
        AuthService authService,
        ILogger<ExternalAuthService> logger)
    {
        _providerService = providerService;
        _externalAuthRepository = externalAuthRepository;
        _userService = userService;
        _roleService = roleService;
        _authService = authService;
        _logger = logger;
    }

    public async Task<AuthResponse> ExternalLoginAsync(ExternalLoginRequest request)
    {
        try
        {
            // 1. Verify the external token
            var userInfo = await _providerService.VerifyTokenAsync(request.Provider, request.IdToken);
            var provider = _providerService.ParseProvider(request.Provider);

            // 2. Check if external auth record exists
            var existingExternalAuth = await _externalAuthRepository.GetByProviderAndExternalUserIdAsync(provider, userInfo.ExternalUserId);

            if (existingExternalAuth != null)
            {
                // 3a. Existing user - generate auth response
                _logger.LogInformation("External login successful for existing user {UserId} via {Provider}", existingExternalAuth.UserId, provider);
                var user = _userService.GetUser(existingExternalAuth.UserId);
                return _authService.GenerateAuthResponse(user);
            }

            // 3b. New user - check if email already exists
            var existingUser = _userService.GetUserByEmail(userInfo.Email);
            if (existingUser != null)
            {
                // User exists with this email but no external auth - suggest account linking
                throw new InvalidOperationException($"An account with email {userInfo.Email} already exists. Please link your {request.Provider} account.");
            }

            // 4. Create new user with external auth
            var newUser = await CreateUserWithExternalAuthAsync(userInfo, provider);
            
            _logger.LogInformation("New user created via external login {UserId} via {Provider}", newUser.Id, provider);
            return _authService.GenerateAuthResponse(newUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External login failed for provider {Provider}", request.Provider);
            throw;
        }
    }

    public async Task<AuthResponse> LinkExternalAccountAsync(Guid userId, LinkExternalAccountRequest request)
    {
        try
        {
            // 1. Verify the external token
            var userInfo = await _providerService.VerifyTokenAsync(request.Provider, request.IdToken);
            var provider = _providerService.ParseProvider(request.Provider);

            // 2. Check if external auth already exists for this provider and user
            var existingUserAuths = await _externalAuthRepository.GetByUserIdAsync(userId);
            if (existingUserAuths.Any(ea => ea.Provider == provider))
            {
                throw new InvalidOperationException($"This account is already linked to a {request.Provider} account.");
            }

            // 3. Check if external account is already linked to another user
            var existingExternalAuth = await _externalAuthRepository.GetByProviderAndExternalUserIdAsync(provider, userInfo.ExternalUserId);
            if (existingExternalAuth != null)
            {
                throw new InvalidOperationException($"This {request.Provider} account is already linked to another user.");
            }

            // 4. Create external auth link
            var externalAuth = new ExternalAuth
            {
                UserId = userId,
                Provider = provider,
                ExternalUserId = userInfo.ExternalUserId,
                Email = userInfo.Email,
                DisplayName = userInfo.DisplayName
            };

            await _externalAuthRepository.CreateAsync(externalAuth);

            // 5. Update user's email verification if external provider confirms it
            if (userInfo.EmailVerified)
            {
                // Note: You'll need to add a method to update email verification in UserService
                // await _userService.UpdateEmailVerificationAsync(userId, true);
            }

            _logger.LogInformation("External account linked for user {UserId} via {Provider}", userId, provider);
            
            var user = _userService.GetUser(userId);
            return _authService.GenerateAuthResponse(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External account linking failed for user {UserId} and provider {Provider}", userId, request.Provider);
            throw;
        }
    }

    public async Task<bool> UnlinkExternalAccountAsync(Guid userId, string provider)
    {
        try
        {
            var authProvider = _providerService.ParseProvider(provider);
            var result = await _externalAuthRepository.DeleteByUserIdAndProviderAsync(userId, authProvider);
            
            if (result)
            {
                _logger.LogInformation("External account unlinked for user {UserId} and provider {Provider}", userId, provider);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External account unlinking failed for user {UserId} and provider {Provider}", userId, provider);
            throw;
        }
    }

    public async Task<List<ExternalAuthDto>> GetUserExternalAccountsAsync(Guid userId)
    {
        var externalAuths = await _externalAuthRepository.GetByUserIdAsync(userId);
        
        return externalAuths.Select(ea => new ExternalAuthDto
        {
            Id = ea.Id,
            Provider = ea.Provider.ToString().ToLowerInvariant(),
            ExternalUserId = ea.ExternalUserId,
            Email = ea.Email,
            DisplayName = ea.DisplayName,
            CreatedAt = ea.CreatedAt
        }).ToList();
    }

    private async Task<UserDto> CreateUserWithExternalAuthAsync(ExternalUserInfo userInfo, AuthProvider provider)
    {
        // Ensure default roles exist
        _roleService.EnsureDefaultRolesExist();

        // Create user with external auth info
        var user = _userService.CreateUser(new UserDto
        {
            Email = userInfo.Email,
            FirstName = userInfo.FirstName ?? "Unknown",
            LastName = userInfo.LastName ?? "User",
            Password = null, // No password for external auth users
            PlayerTerms = true, // Always true as per business rule
            BusinessTerms = false,
            TrainerTerms = false,
            ActivityInterests = new List<string>(),
            Roles = new List<string> { "Player" } // Default to Player role
        });

        // Create external auth record
        var externalAuth = new ExternalAuth
        {
            UserId = user.Id,
            Provider = provider,
            ExternalUserId = userInfo.ExternalUserId,
            Email = userInfo.Email,
            DisplayName = userInfo.DisplayName
        };

        await _externalAuthRepository.CreateAsync(externalAuth);

        // Update user's email verification and auth provider if needed
        // Note: You'll need to add methods to UserService to update these fields

        return user;
    }
}