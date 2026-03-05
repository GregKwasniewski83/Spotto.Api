using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IPendingTimeSlotReservationRepository
{
    Task<PendingTimeSlotReservation> CreatePendingReservationAsync(
        Guid facilityId, DateTime date, List<string> timeSlots, Guid userId,
        Guid? trainerProfileId = null, Guid? paymentId = null,
        int numberOfUsers = 1, bool payForAllUsers = true);
    Task<bool> HasConflictingPendingReservationAsync(Guid facilityId, DateTime date, List<string> timeSlots, Guid? excludeUserId = null);
    Task<PendingTimeSlotReservation?> GetPendingReservationByUserAsync(Guid facilityId, DateTime date, Guid userId);
    Task<PendingTimeSlotReservation?> GetPendingReservationByPaymentIdAsync(Guid paymentId);
    Task<bool> RemovePendingReservationAsync(Guid pendingReservationId);
    Task<bool> RemoveUserPendingReservationAsync(Guid facilityId, DateTime date, Guid userId);
    Task<int> CleanupExpiredPendingReservationsAsync();
    Task<bool> ExtendPendingReservationAsync(Guid pendingReservationId, int additionalMinutes = 15);
    Task<List<string>> GetPendingTimeSlotsAsync(Guid facilityId, DateTime date, Guid? excludeUserId = null);
}