using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class PendingTimeSlotReservationService : IPendingTimeSlotReservationService
{
    private readonly IPendingTimeSlotReservationRepository _pendingReservationRepository;
    private readonly IReservationService _reservationService;

    public PendingTimeSlotReservationService(IPendingTimeSlotReservationRepository pendingReservationRepository, IReservationService reservationService)
    {
        _pendingReservationRepository = pendingReservationRepository;
        _reservationService = reservationService;
    }

    public async Task<PendingReservationDto> CreatePendingReservationAsync(CreatePendingReservationDto pendingReservationDto, Guid userId)
    {
        // First check if the slots are actually available (not booked or unavailable)
        var isAvailable = await _reservationService.IsTimeSlotAvailableAsync(
            pendingReservationDto.TimeSlots, 
            pendingReservationDto.FacilityId, 
            pendingReservationDto.Date);

        if (!isAvailable)
        {
            throw new InvalidOperationException("One or more time slots are not available");
        }

        // Check trainer availability if specified
        if (pendingReservationDto.TrainerProfileId.HasValue)
        {
            var isTrainerAvailable = await _reservationService.IsTrainerAvailableAsync(
                pendingReservationDto.TrainerProfileId.Value,
                pendingReservationDto.TimeSlots,
                pendingReservationDto.Date);

            if (!isTrainerAvailable)
            {
                throw new InvalidOperationException("Trainer is not available for the selected time slots");
            }
        }

        var pendingReservation = await _pendingReservationRepository.CreatePendingReservationAsync(
            pendingReservationDto.FacilityId,
            pendingReservationDto.Date,
            pendingReservationDto.TimeSlots,
            userId,
            pendingReservationDto.TrainerProfileId);

        return MapToDto(pendingReservation);
    }

    public async Task<PendingReservationDto?> GetUserPendingReservationAsync(Guid facilityId, DateTime date, Guid userId)
    {
        var pendingReservation = await _pendingReservationRepository.GetPendingReservationByUserAsync(facilityId, date, userId);
        return pendingReservation == null ? null : MapToDto(pendingReservation);
    }

    public async Task<bool> ExtendPendingReservationAsync(Guid pendingReservationId, int additionalMinutes = 15)
    {
        return await _pendingReservationRepository.ExtendPendingReservationAsync(pendingReservationId, additionalMinutes);
    }

    public async Task<bool> ReleasePendingReservationAsync(Guid facilityId, DateTime date, Guid userId)
    {
        return await _pendingReservationRepository.RemoveUserPendingReservationAsync(facilityId, date, userId);
    }

    public async Task<int> CleanupExpiredPendingReservationsAsync()
    {
        return await _pendingReservationRepository.CleanupExpiredPendingReservationsAsync();
    }

    private PendingReservationDto MapToDto(PendingTimeSlotReservation pendingReservation)
    {
        var remainingMinutes = Math.Max(0, (int)(pendingReservation.ExpiresAt - DateTime.UtcNow).TotalMinutes);

        return new PendingReservationDto
        {
            Id = pendingReservation.Id,
            FacilityId = pendingReservation.FacilityId,
            Date = pendingReservation.Date,
            TimeSlots = pendingReservation.TimeSlots,
            UserId = pendingReservation.UserId,
            TrainerProfileId = pendingReservation.TrainerProfileId,
            ExpiresAt = pendingReservation.ExpiresAt,
            CreatedAt = pendingReservation.CreatedAt,
            FacilityName = pendingReservation.Facility?.Name,
            TrainerDisplayName = pendingReservation.TrainerProfile?.DisplayName,
            RemainingMinutes = remainingMinutes
        };
    }
}