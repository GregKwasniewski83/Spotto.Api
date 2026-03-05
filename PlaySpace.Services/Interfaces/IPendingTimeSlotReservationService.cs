using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IPendingTimeSlotReservationService
{
    Task<PendingReservationDto> CreatePendingReservationAsync(CreatePendingReservationDto pendingReservationDto, Guid userId);
    Task<PendingReservationDto?> GetUserPendingReservationAsync(Guid facilityId, DateTime date, Guid userId);
    Task<bool> ExtendPendingReservationAsync(Guid pendingReservationId, int additionalMinutes = 15);
    Task<bool> ReleasePendingReservationAsync(Guid facilityId, DateTime date, Guid userId);
    Task<int> CleanupExpiredPendingReservationsAsync();
}