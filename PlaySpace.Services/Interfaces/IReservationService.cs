using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IReservationService
{
    Task<ReservationDto> CreateReservationAsync(CreateReservationDto reservationDto, Guid userId);
    ReservationDto CreateReservation(CreateReservationDto reservationDto, Guid userId);
    Task<ReservationDto> CreateAdminReservationAsync(AdminCreateReservationDto reservationDto, Guid createdById);
    Task<GroupReservationResponseDto> CreateAdminGroupReservationAsync(CreateAdminGroupReservationDto groupDto, Guid createdById);
    ReservationDto? GetReservation(Guid id);
    List<ReservationDto> GetUserReservations(Guid userId);
    bool CancelReservation(Guid reservationId, Guid userId);
    Task<ReservationDto> CancelReservationWithRefundAsync(Guid reservationId, Guid userId);
    Task<ReservationDto> CancelReservationByAgentAsync(Guid reservationId, Guid agentUserId, AgentCancelReservationDto cancelDto);
    Task<bool> IsTimeSlotAvailableAsync(List<string> timeSlots, Guid facilityId, DateTime date, Guid? excludeUserId = null);
    ReservationDto? UpdateReservation(Guid reservationId, UpdateReservationDto updateDto, Guid userId);
    Task<bool> IsTrainerAvailableAsync(Guid trainerProfileId, List<string> timeSlots, DateTime date);

   

    // Stats methods
    int GetTotalReservationsCount();

    // Upcoming reservations
    List<ReservationDto> GetUserUpcomingReservations(Guid userId);

    // Group reservations
    Task<GroupReservationResponseDto> CreateGroupReservationAsync(CreateGroupReservationDto groupReservationDto, Guid userId);
    GroupReservationResponseDto? GetGroupReservation(Guid groupId);
    bool CancelGroupReservation(Guid groupId, Guid userId);

    // NEW: Partial cancellation methods
    Task<PartialCancellationResponseDto> CancelSpecificSlotsAsync(Guid reservationId, List<Guid> slotIds, Guid userId);
    Task<ReservationWithSlotsDto?> GetReservationWithSlotsAsync(Guid id);

    // NEW: Toggle payment status for offline payments
    Task<ReservationDto> ToggleReservationPaymentAsync(Guid reservationId, Guid agentUserId, string paymentMethod = "OFFLINE");

    // NEW: Apply product purchase to reservation (for agents)
    Task<ApplyProductResultDto> ApplyProductToReservationAsync(Guid reservationId, ApplyProductToReservationDto dto, Guid agentUserId);

    // NEW: Reschedule reservation (for agents)
    Task<RescheduleResultDto> RescheduleReservationAsync(Guid reservationId, RescheduleReservationDto dto, Guid agentUserId);

    // NEW: Update reservation notes (for agents/owners)
    Task<ReservationDto> UpdateReservationNotesAsync(Guid reservationId, UpdateReservationNotesDto dto, Guid userId);

    // NEW: Pay for existing unpaid reservation (for users)
    Task<PayForReservationResponseDto> PayForReservationAsync(Guid reservationId, PayForReservationDto paymentDto, Guid userId);

    // NEW: Get unpaid reservations for a user
    List<ReservationDto> GetUnpaidReservationsForUser(Guid userId);
}