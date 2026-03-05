using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using Microsoft.AspNetCore.Http;

namespace PlaySpace.Services.Interfaces;

public interface IUserService
{
    UserDto GetUser(Guid id);
    UserDto CreateUser(UserDto user);
    UserDto GetUserByEmail(string email);
    UserDto GetUserByRefreshToken(string refreshToken);
    void SetRefreshToken(Guid userId, string refreshToken);
    UserDto GetCurrentUser(Guid userId);
    Task<bool> UpdatePlayerTermsAsync(Guid userId, bool playerTerms);
    Task<bool> UpdateBusinessTermsAsync(Guid userId, bool businessTerms);
    Task<bool> UpdateTrainerTermsAsync(Guid userId, bool trainerTerms);
    Task<bool> UpdateAllTermsAsync(Guid userId, bool playerTerms, bool businessTerms, bool trainerTerms);
    UserProfileDto? UpdateUserProfile(Guid userId, UpdateUserProfileDto updateDto);
    Task<bool> UpdateActivityInterestsAsync(Guid userId, List<string> activityInterests);
    UserProfileDto? GetUserProfile(Guid userId);
    Task<string> UploadAvatarAsync(Guid userId, IFormFile avatar);
    Task<bool> UpdateAvatarUrlAsync(Guid userId, string avatarUrl);
    Task<bool> RequestPasswordResetAsync(string email, string resetUrl);
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword);
    Task<bool> DeleteAccountAsync(Guid userId, string email, string? reason);

    // Email Verification
    Task<string> GenerateEmailVerificationTokenAsync(Guid userId);
    Task<bool> VerifyEmailAsync(string token);
    Task<bool> ResendVerificationEmailAsync(Guid userId, string webUrlBase, string deepLinkScheme);
    Task<UserDto?> GetUserByVerificationTokenAsync(string token);
    Task MarkEmailVerifiedAsync(Guid userId);
}
