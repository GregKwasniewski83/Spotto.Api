using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
using BCrypt.Net;

namespace PlaySpace.Services.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleService _roleService;
    private readonly IEmailService _emailService;
    private readonly IFtpStorageService _ftpStorageService;

    public UserService(IUserRepository userRepository, IRoleService roleService, IEmailService emailService, IFtpStorageService ftpStorageService)
    {
        _userRepository = userRepository;
        _roleService = roleService;
        _emailService = emailService;
        _ftpStorageService = ftpStorageService;
    }

    public UserDto GetUser(Guid id)
    {
        var user = _userRepository.GetUser(id);
        return user == null ? null : MapToDto(user);
    }

    public UserDto CreateUser(UserDto userDto)
    {
        var userResult = _userRepository.CreateUser(userDto);
        
        // Assign roles to the new user
        foreach (var roleName in userDto.Roles)
        {
            _roleService.AssignRoleToUser(userResult.Id, roleName);
        }
        
        return MapToDto(userResult);
    }

    public UserDto GetUserByEmail(string email)
    {
        var user = _userRepository.GetUserByEmail(email);
        return user == null ? null : MapToDto(user);
    }

    public UserDto GetUserByRefreshToken(string refreshToken)
    {
        var user = _userRepository.GetUserByRefreshToken(refreshToken);
        return user == null ? null : MapToDto(user);
    }

    public void SetRefreshToken(Guid userId, string refreshToken)
    {
        _userRepository.SetRefreshToken(userId, refreshToken);
    }

    public UserDto GetCurrentUser(Guid userId)
    {
        var user = _userRepository.GetUser(userId);
        if (user == null) return null;
        
        return MapToDto(user);
    }

    public async Task<bool> UpdatePlayerTermsAsync(Guid userId, bool playerTerms)
    {
        return await _userRepository.UpdatePlayerTermsAsync(userId, playerTerms);
    }

    public async Task<bool> UpdateBusinessTermsAsync(Guid userId, bool businessTerms)
    {
        return await _userRepository.UpdateBusinessTermsAsync(userId, businessTerms);
    }

    public async Task<bool> UpdateTrainerTermsAsync(Guid userId, bool trainerTerms)
    {
        return await _userRepository.UpdateTrainerTermsAsync(userId, trainerTerms);
    }

    public async Task<bool> UpdateAllTermsAsync(Guid userId, bool playerTerms, bool businessTerms, bool trainerTerms)
    {
        if (!playerTerms)
        {
            throw new InvalidOperationException("Player terms must always be accepted and cannot be set to false.");
        }

        return await _userRepository.UpdateAllTermsAsync(userId, playerTerms, businessTerms, trainerTerms);
    }

    public UserProfileDto? UpdateUserProfile(Guid userId, UpdateUserProfileDto updateDto)
    {
        var updatedUser = _userRepository.UpdateUserProfile(userId, updateDto);
        return updatedUser == null ? null : MapToProfileDto(updatedUser);
    }

    public async Task<bool> UpdateActivityInterestsAsync(Guid userId, List<string> activityInterests)
    {
        return await _userRepository.UpdateActivityInterestsAsync(userId, activityInterests);
    }

    public UserProfileDto? GetUserProfile(Guid userId)
    {
        var user = _userRepository.GetUser(userId);
        return user == null ? null : MapToProfileDto(user);
    }

    private UserDto MapToDto(User user)
    {
        var userRoles = _roleService.GetUserRoles(user.Id);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            DateOfBirth = user.DateOfBirth,
            AvatarUrl = user.AvatarUrl,
            Password = user.Password, // Include for auth purposes
            PlayerTerms = user.PlayerTerms,
            BusinessTerms = user.BusinessTerms,
            TrainerTerms = user.TrainerTerms,
            IsEmailVerified = user.IsEmailVerified,
            ActivityInterests = user.ActivityInterests,
            Roles = userRoles
        };
    }

    private UserProfileDto MapToProfileDto(User user)
    {
        var userRoles = _roleService.GetUserRoles(user.Id);
        
        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            DateOfBirth = user.DateOfBirth,
            AvatarUrl = user.AvatarUrl,
            PlayerTerms = user.PlayerTerms,
            BusinessTerms = user.BusinessTerms,
            TrainerTerms = user.TrainerTerms,
            ActivityInterests = user.ActivityInterests,
            Roles = userRoles
        };
    }

    public async Task<string> UploadAvatarAsync(Guid userId, IFormFile avatar)
    {
        if (avatar == null || avatar.Length == 0)
            throw new ArgumentException("Avatar file is required");

        var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
        if (!allowedTypes.Contains(avatar.ContentType.ToLower()))
            throw new ArgumentException("Only JPEG, PNG and GIF files are allowed");

        if (avatar.Length > 5 * 1024 * 1024) // 5MB limit
            throw new ArgumentException("File size cannot exceed 5MB");

        var fileExtension = Path.GetExtension(avatar.FileName).ToLowerInvariant();
        var fileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";

        // Upload to FTP server
        using (var stream = avatar.OpenReadStream())
        {
            var avatarUrl = await _ftpStorageService.UploadFileAsync(stream, "users", fileName);
            await _userRepository.UpdateAvatarAsync(userId, avatarUrl);
            return avatarUrl;
        }
    }

    public async Task<bool> UpdateAvatarUrlAsync(Guid userId, string avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
            throw new ArgumentException("Avatar URL cannot be empty");

        await _userRepository.UpdateAvatarAsync(userId, avatarUrl);
        return true;
    }

    public async Task<bool> RequestPasswordResetAsync(string email, string resetUrl)
    {
        var user = _userRepository.GetUserByEmail(email);

        // Don't reveal if user exists for security reasons
        if (user == null)
            return true;

        // Only allow password reset for local auth users
        if (user.Password == null || string.IsNullOrEmpty(user.Password))
            return true; // User uses external auth, silently ignore

        // Generate secure random token
        var resetToken = GenerateSecureToken();
        var tokenExpiry = DateTime.UtcNow.AddHours(24); // Token valid for 24 hours

        // Save token to database
        await _userRepository.SetPasswordResetTokenAsync(user.Id, resetToken, tokenExpiry);

        // Send password reset email
        await _emailService.SendPasswordResetEmailAsync(email, resetToken, resetUrl);

        return true;
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = _userRepository.GetUserByEmail(email);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid reset token");

        // Validate token
        if (string.IsNullOrEmpty(user.PasswordResetToken) || user.PasswordResetToken != token)
            throw new UnauthorizedAccessException("Invalid reset token");

        // Check if token is expired
        if (user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Reset token has expired");

        // Hash new password
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);

        // Update password and clear reset token
        await _userRepository.UpdatePasswordAsync(user.Id, hashedPassword);
        await _userRepository.ClearPasswordResetTokenAsync(user.Id);

        // Send confirmation email
        await _emailService.SendPasswordChangedEmailAsync(email, user.FirstName);

        return true;
    }

    public async Task<bool> DeleteAccountAsync(Guid userId, string email, string? reason)
    {
        // Get user to verify ownership and email match
        var user = await _userRepository.GetUserByIdAsync(userId);

        if (user == null)
        {
            return false;
        }

        // Security: Verify email matches authenticated user
        if (!user.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Email does not match authenticated user");
        }

        // Store info for email before anonymization
        var userName = $"{user.FirstName} {user.LastName}";
        var userEmail = user.Email;

        // Remove all user roles (authorization data, not business data)
        foreach (var userRole in user.UserRoles)
        {
            _roleService.RemoveRoleFromUser(userId, userRole.Role.Name);
        }

        // Anonymize user data (preserves business records like payments, reservations)
        var anonymized = await _userRepository.AnonymizeUserAsync(userId);

        if (!anonymized)
        {
            return false;
        }

        // Send confirmation email (fire-and-forget, don't fail on anonymization if email fails)
        try
        {
            await _emailService.SendAccountDeletedEmailAsync(userEmail, userName, reason);
        }
        catch (Exception ex)
        {
            // Email errors logged by EmailService, don't rethrow
        }

        return true;
    }

    // Email Verification Methods
    public async Task<string> GenerateEmailVerificationTokenAsync(Guid userId)
    {
        var token = GenerateSecureToken();
        var expiry = DateTime.UtcNow.AddDays(7); // Token valid for 7 days

        await _userRepository.SetEmailVerificationTokenAsync(userId, token, expiry);
        return token;
    }

    public async Task<bool> VerifyEmailAsync(string token)
    {
        var user = await _userRepository.GetUserByEmailVerificationTokenAsync(token);

        if (user == null)
            return false;

        // Check if token is expired
        if (user.EmailVerificationTokenExpiry == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
            return false;

        // Mark email as verified and clear token
        await _userRepository.SetEmailVerifiedAsync(user.Id, true);
        await _userRepository.ClearEmailVerificationTokenAsync(user.Id);

        return true;
    }

    public async Task MarkEmailVerifiedAsync(Guid userId)
    {
        await _userRepository.SetEmailVerifiedAsync(userId, true);
    }

    public async Task<bool> ResendVerificationEmailAsync(Guid userId, string webUrlBase, string deepLinkScheme)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null || user.IsEmailVerified)
            return false;

        // Generate new token
        var token = await GenerateEmailVerificationTokenAsync(userId);

        // Build verification URLs
        var webUrl = $"{webUrlBase}/verify-email?token={token}";
        var deepLinkUrl = $"{deepLinkScheme}://verify-email?token={token}";

        // Send verification email
        await _emailService.SendEmailVerificationAsync(user.Email, user.FirstName, webUrl, deepLinkUrl);

        return true;
    }

    public async Task<UserDto?> GetUserByVerificationTokenAsync(string token)
    {
        var user = await _userRepository.GetUserByEmailVerificationTokenAsync(token);
        return user == null ? null : MapToDto(user);
    }

    private string GenerateSecureToken()
    {
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        return Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
