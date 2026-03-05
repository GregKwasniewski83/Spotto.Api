using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories;

public class PendingTrainingParticipantRepository : IPendingTrainingParticipantRepository
{
    private readonly PlaySpaceDbContext _context;

    public PendingTrainingParticipantRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<PendingTrainingParticipant> CreatePendingParticipantAsync(Guid trainingId, Guid userId, string? notes = null)
    {
        // First cleanup expired participants
        await CleanupExpiredPendingParticipantsAsync();

        // Check if user is already an active participant in this training
        var isAlreadyParticipant = await _context.TrainingParticipants
            .AnyAsync(tp => tp.TrainingId == trainingId && 
                           tp.UserId == userId && 
                           tp.Status == "Active");
        
        if (isAlreadyParticipant)
        {
            throw new InvalidOperationException("User is already an active participant in this training");
        }

        // Check for existing pending participant and extend it instead of creating new one
        var existingPending = await GetPendingParticipantAsync(trainingId, userId);
        if (existingPending != null)
        {
            // Extend the existing pending participant
            existingPending.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
            existingPending.Notes = notes; // Update notes if provided
            await _context.SaveChangesAsync();
            return existingPending;
        }

        var pendingParticipant = new PendingTrainingParticipant
        {
            Id = Guid.NewGuid(),
            TrainingId = trainingId,
            UserId = userId,
            Notes = notes,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // 15-minute expiry
            CreatedAt = DateTime.UtcNow
        };

        _context.PendingTrainingParticipants.Add(pendingParticipant);
        await _context.SaveChangesAsync();
        
        return pendingParticipant;
    }

    public async Task<bool> HasExistingPendingParticipantAsync(Guid trainingId, Guid userId)
    {
        var now = DateTime.UtcNow;

        return await _context.PendingTrainingParticipants
            .AnyAsync(p => p.TrainingId == trainingId &&
                          p.UserId == userId &&
                          p.ExpiresAt > now);
    }

    public async Task<PendingTrainingParticipant?> GetPendingParticipantAsync(Guid trainingId, Guid userId)
    {
        var now = DateTime.UtcNow;

        return await _context.PendingTrainingParticipants
            .Include(p => p.Training)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.TrainingId == trainingId &&
                                    p.UserId == userId &&
                                    p.ExpiresAt > now);
    }

    public async Task<bool> RemovePendingParticipantAsync(Guid pendingParticipantId)
    {
        var pendingParticipant = await _context.PendingTrainingParticipants
            .FirstOrDefaultAsync(p => p.Id == pendingParticipantId);

        if (pendingParticipant == null)
            return false;

        _context.PendingTrainingParticipants.Remove(pendingParticipant);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveUserPendingParticipantAsync(Guid trainingId, Guid userId)
    {
        var pendingParticipants = await _context.PendingTrainingParticipants
            .Where(p => p.TrainingId == trainingId &&
                       p.UserId == userId)
            .ToListAsync();

        if (!pendingParticipants.Any())
            return false;

        _context.PendingTrainingParticipants.RemoveRange(pendingParticipants);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> CleanupExpiredPendingParticipantsAsync()
    {
        var now = DateTime.UtcNow;
        
        var expiredParticipants = await _context.PendingTrainingParticipants
            .Where(p => p.ExpiresAt <= now)
            .ToListAsync();

        if (!expiredParticipants.Any())
            return 0;

        _context.PendingTrainingParticipants.RemoveRange(expiredParticipants);
        await _context.SaveChangesAsync();
        
        return expiredParticipants.Count;
    }

    public async Task<bool> ExtendPendingParticipantAsync(Guid pendingParticipantId, int additionalMinutes = 15)
    {
        var pendingParticipant = await _context.PendingTrainingParticipants
            .FirstOrDefaultAsync(p => p.Id == pendingParticipantId);

        if (pendingParticipant == null || pendingParticipant.ExpiresAt <= DateTime.UtcNow)
            return false;

        pendingParticipant.ExpiresAt = pendingParticipant.ExpiresAt.AddMinutes(additionalMinutes);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetPendingCountAsync(Guid trainingId)
    {
        var now = DateTime.UtcNow;
        return await _context.PendingTrainingParticipants
            .CountAsync(p => p.TrainingId == trainingId && p.ExpiresAt > now);
    }
}