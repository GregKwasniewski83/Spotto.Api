using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface IReservationRepository
{
    Reservation CreateReservation(CreateReservationDto reservationDto, Guid userId);
    Reservation? GetReservation(Guid id);
    Reservation? GetReservationByPaymentId(Guid paymentId);
    List<Reservation> GetUserReservations(Guid userId); // includes agent-created reservations where user's email was provided
    bool CancelReservation(Guid reservationId, Guid userId);
    bool CancelReservationByAgent(Guid reservationId, Guid agentUserId, string? agentName, string? notes);
    Task<bool> IsTimeSlotAvailableAsync(List<string> timeSlots, Guid facilityId, DateTime date, Guid? excludeUserId = null);
    List<string> GetReservedTimeSlots(Guid facilityId, DateTime date);
    Reservation? UpdateReservation(Guid reservationId, UpdateReservationDto updateDto, Guid userId);
    
    // Stats methods
    int GetTotalCount();
    
    // Upcoming reservations (includes agent-created reservations where user's email was provided)
    List<Reservation> GetUserUpcomingReservations(Guid userId);
    
    // Group reservations
    List<Reservation> CreateGroupReservations(List<CreateReservationDto> reservationDtos, Guid userId, Guid groupId);
    List<Reservation> GetGroupReservations(Guid groupId);
    bool CancelGroupReservations(Guid groupId, Guid userId);

    // Agent Dashboard methods
    List<Reservation> GetReservationsForFacilityAndDate(Guid facilityId, DateTime date);

    // Admin/Agent reservation creation (without payment)
    Reservation CreateAdminReservation(AdminCreateReservationDto reservationDto, Guid createdById);
    List<Reservation> CreateAdminGroupReservations(CreateAdminGroupReservationDto groupDto, Guid createdById, Guid groupId);

    // NEW: Partial cancellation methods
    Task<bool> CancelReservationSlotsAsync(Guid reservationId, List<Guid> slotIds, string? cancellationReason = null);
    Task<Reservation?> GetReservationWithSlotsAsync(Guid id);

    // NEW: Async update method for payment assignment
    Task<Reservation> UpdateReservationAsync(Reservation reservation);

    // Replace all slots for a reservation (for rescheduling)
    Task ReplaceReservationSlotsAsync(Guid reservationId, List<string> newTimeSlots, decimal slotPrice);

    // Get guest reservations by email (for unpaid reservations lookup)
    List<Reservation> GetGuestReservationsByEmail(string email);

    // Sales reporting
    Task<List<Reservation>> GetMonthlyReservationsForBusinessAsync(Guid businessProfileId, int year, int month);
}