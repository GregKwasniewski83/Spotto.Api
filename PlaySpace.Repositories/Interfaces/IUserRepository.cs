using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces
{
    public interface IUserRepository
    {
        User GetUser(Guid id);
        Task<User?> GetUserByIdAsync(Guid id);
        User CreateUser(UserDto user);
        User GetUserByEmail(string email);
        Task<User?> GetUserByEmailAsync(string email);
        User GetUserByRefreshToken(string refreshToken);
        void SetRefreshToken(Guid userId, string refreshToken);
        Task<bool> UpdatePlayerTermsAsync(Guid userId, bool playerTerms);
        Task<bool> UpdateBusinessTermsAsync(Guid userId, bool businessTerms);
        Task<bool> UpdateTrainerTermsAsync(Guid userId, bool trainerTerms);
        Task<bool> UpdateAllTermsAsync(Guid userId, bool playerTerms, bool businessTerms, bool trainerTerms);
        User? UpdateUserProfile(Guid userId, UpdateUserProfileDto updateDto);
        Task<bool> UpdateActivityInterestsAsync(Guid userId, List<string> activityInterests);
        Task UpdateAvatarAsync(Guid userId, string avatarUrl);
        Task SetPasswordResetTokenAsync(Guid userId, string token, DateTime expiry);
        Task UpdatePasswordAsync(Guid userId, string hashedPassword);
        Task ClearPasswordResetTokenAsync(Guid userId);
        Task<bool> AnonymizeUserAsync(Guid userId);

        // Email Verification
        Task SetEmailVerificationTokenAsync(Guid userId, string token, DateTime expiry);
        Task<User?> GetUserByEmailVerificationTokenAsync(string token);
        Task SetEmailVerifiedAsync(Guid userId, bool isVerified);
        Task ClearEmailVerificationTokenAsync(Guid userId);
    }
}
