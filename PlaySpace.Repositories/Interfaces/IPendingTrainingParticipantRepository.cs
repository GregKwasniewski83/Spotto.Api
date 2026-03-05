using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IPendingTrainingParticipantRepository
{
    Task<PendingTrainingParticipant> CreatePendingParticipantAsync(Guid trainingId, Guid userId, string? notes = null);
    Task<bool> HasExistingPendingParticipantAsync(Guid trainingId, Guid userId);
    Task<PendingTrainingParticipant?> GetPendingParticipantAsync(Guid trainingId, Guid userId);
    Task<bool> RemovePendingParticipantAsync(Guid pendingParticipantId);
    Task<bool> RemoveUserPendingParticipantAsync(Guid trainingId, Guid userId);
    Task<int> CleanupExpiredPendingParticipantsAsync();
    Task<bool> ExtendPendingParticipantAsync(Guid pendingParticipantId, int additionalMinutes = 15);
    Task<int> GetPendingCountAsync(Guid trainingId);
}