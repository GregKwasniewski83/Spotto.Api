using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly PlaySpaceDbContext _context;

        public UserRepository(PlaySpaceDbContext context)
        {
            _context = context;
        }

        public User CreateUser(UserDto user)
        {
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.Phone,
                DateOfBirth = user.DateOfBirth,
                Password = user.Password,
                ActivityInterests = user.ActivityInterests
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            return newUser;
        }

        public User GetUser(Guid id)
        {
            return _context.Users.Include(r => r.UserRoles).FirstOrDefault(u => u.Id == id);
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _context.Users.Include(r => r.UserRoles).FirstOrDefaultAsync(u => u.Id == id);
        }

        public User GetUserByEmail(string email)
        {
            return _context.Users.Include(r => r.UserRoles).FirstOrDefault(u => u.Email == email);
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            return await _context.Users.Include(r => r.UserRoles).FirstOrDefaultAsync(u => u.Email == email);
        }

        public User GetUserByRefreshToken(string refreshToken)
        {
            return _context.Users.FirstOrDefault(u => u.RefreshToken == refreshToken && 
                                                     u.RefreshTokenExpiryTime > DateTime.UtcNow);
        }

        public void SetRefreshToken(Guid userId, string refreshToken)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(120); // 120 days expiry
                _context.SaveChanges();
            }
        }

        public async Task<bool> UpdatePlayerTermsAsync(Guid userId, bool playerTerms)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            user.PlayerTerms = playerTerms;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateBusinessTermsAsync(Guid userId, bool businessTerms)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            user.BusinessTerms = businessTerms;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateTrainerTermsAsync(Guid userId, bool trainerTerms)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            user.TrainerTerms = trainerTerms;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateAllTermsAsync(Guid userId, bool playerTerms, bool businessTerms, bool trainerTerms)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            user.PlayerTerms = playerTerms;
            user.BusinessTerms = businessTerms;
            user.TrainerTerms = trainerTerms;
            await _context.SaveChangesAsync();
            return true;
        }

        public User? UpdateUserProfile(Guid userId, UpdateUserProfileDto updateDto)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return null;

            if (!string.IsNullOrEmpty(updateDto.FirstName))
                user.FirstName = updateDto.FirstName;
            
            if (!string.IsNullOrEmpty(updateDto.LastName))
                user.LastName = updateDto.LastName;
            
            if (updateDto.Phone != null)
                user.Phone = updateDto.Phone;
            
            if (updateDto.DateOfBirth != null)
                user.DateOfBirth = updateDto.DateOfBirth;
            
            if (updateDto.ActivityInterests != null)
            {
                user.ActivityInterests = updateDto.ActivityInterests;
                // Explicitly mark the ActivityInterests property as modified
                _context.Entry(user).Property(e => e.ActivityInterests).IsModified = true;
            }

            _context.SaveChanges();
            return user;
        }

        public async Task<bool> UpdateActivityInterestsAsync(Guid userId, List<string> activityInterests)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return false;

            user.ActivityInterests = activityInterests;
            // Explicitly mark the ActivityInterests property as modified
            _context.Entry(user).Property(e => e.ActivityInterests).IsModified = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateAvatarAsync(Guid userId, string avatarUrl)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.AvatarUrl = avatarUrl;
                await _context.SaveChangesAsync();
            }
        }

        public async Task SetPasswordResetTokenAsync(Guid userId, string token, DateTime expiry)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.PasswordResetToken = token;
                user.PasswordResetTokenExpiry = expiry;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdatePasswordAsync(Guid userId, string hashedPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.Password = hashedPassword;
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearPasswordResetTokenAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.PasswordResetToken = null;
                user.PasswordResetTokenExpiry = null;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<bool> AnonymizeUserAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return false;
            }

            // Anonymize all PII while keeping the record for business data integrity
            user.Email = $"deleted_{userId}@deleted.com";
            user.FirstName = "Deleted";
            user.LastName = "User";
            user.Phone = null;
            user.DateOfBirth = null;
            user.Password = null;
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            user.AvatarUrl = null;
            user.ActivityInterests = new List<string>();
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            user.PlayerTerms = false;
            user.BusinessTerms = false;
            user.TrainerTerms = false;
            user.IsEmailVerified = false;

            await _context.SaveChangesAsync();
            return true;
        }

        // Email Verification Methods
        public async Task SetEmailVerificationTokenAsync(Guid userId, string token, DateTime expiry)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.EmailVerificationToken = token;
                user.EmailVerificationTokenExpiry = expiry;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<User?> GetUserByEmailVerificationTokenAsync(string token)
        {
            return await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        }

        public async Task SetEmailVerifiedAsync(Guid userId, bool isVerified)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.IsEmailVerified = isVerified;
                await _context.SaveChangesAsync();
            }
        }

        public async Task ClearEmailVerificationTokenAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null)
            {
                user.EmailVerificationToken = null;
                user.EmailVerificationTokenExpiry = null;
                await _context.SaveChangesAsync();
            }
        }
    }
}
