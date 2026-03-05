using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories;

public class PendingTimeSlotReservationRepository : IPendingTimeSlotReservationRepository
{
    private readonly PlaySpaceDbContext _context;

    public PendingTimeSlotReservationRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<PendingTimeSlotReservation> CreatePendingReservationAsync(
        Guid facilityId, DateTime date, List<string> timeSlots, Guid userId,
        Guid? trainerProfileId = null, Guid? paymentId = null,
        int numberOfUsers = 1, bool payForAllUsers = true)
    {
        // First cleanup expired reservations
        await CleanupExpiredPendingReservationsAsync();

        // Check for conflicts with OTHER users
        var hasConflict = await HasConflictingPendingReservationAsync(facilityId, date, timeSlots, userId);
        if (hasConflict)
        {
            throw new InvalidOperationException("Time slots are already pending reservation by another user");
        }

        // Only remove user's pending reservations that have OVERLAPPING timeslots (not all of them)
        await RemoveUserOverlappingPendingReservationsAsync(facilityId, date, timeSlots, userId);

        var pendingReservation = new PendingTimeSlotReservation
        {
            Id = Guid.NewGuid(),
            FacilityId = facilityId,
            Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
            TimeSlots = timeSlots,
            UserId = userId,
            TrainerProfileId = trainerProfileId,
            PaymentId = paymentId,
            NumberOfUsers = numberOfUsers,
            PayForAllUsers = payForAllUsers,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // 15-minute expiry
            CreatedAt = DateTime.UtcNow
        };

        _context.PendingTimeSlotReservations.Add(pendingReservation);
        await _context.SaveChangesAsync();

        return pendingReservation;
    }

    public async Task<bool> HasConflictingPendingReservationAsync(Guid facilityId, DateTime date, List<string> timeSlots, Guid? excludeUserId = null)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        var conflictingReservations = await _context.PendingTimeSlotReservations
            .Where(p => p.FacilityId == facilityId &&
                       p.Date == utcDate &&
                       p.ExpiresAt > now &&
                       (excludeUserId == null || p.UserId != excludeUserId))
            .ToListAsync();

        // Check if any of the requested time slots overlap with existing pending reservations
        foreach (var pending in conflictingReservations)
        {
            if (timeSlots.Any(slot => pending.TimeSlots.Contains(slot)))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<PendingTimeSlotReservation?> GetPendingReservationByUserAsync(Guid facilityId, DateTime date, Guid userId)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        return await _context.PendingTimeSlotReservations
            .Include(p => p.Facility)
            .Include(p => p.TrainerProfile)
            .FirstOrDefaultAsync(p => p.FacilityId == facilityId &&
                                    p.Date == utcDate &&
                                    p.UserId == userId &&
                                    p.ExpiresAt > now);
    }

    public async Task<PendingTimeSlotReservation?> GetPendingReservationByPaymentIdAsync(Guid paymentId)
    {
        // Note: We don't check ExpiresAt here because we want to find the pending reservation
        // even if it has expired (as a fallback for creating reservations)
        return await _context.PendingTimeSlotReservations
            .Include(p => p.Facility)
            .Include(p => p.TrainerProfile)
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
    }

    public async Task<bool> RemovePendingReservationAsync(Guid pendingReservationId)
    {
        var pendingReservation = await _context.PendingTimeSlotReservations
            .FirstOrDefaultAsync(p => p.Id == pendingReservationId);

        if (pendingReservation == null)
            return false;

        _context.PendingTimeSlotReservations.Remove(pendingReservation);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveUserPendingReservationAsync(Guid facilityId, DateTime date, Guid userId)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        var pendingReservations = await _context.PendingTimeSlotReservations
            .Where(p => p.FacilityId == facilityId &&
                       p.Date == utcDate &&
                       p.UserId == userId)
            .ToListAsync();

        if (!pendingReservations.Any())
            return false;

        _context.PendingTimeSlotReservations.RemoveRange(pendingReservations);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveUserOverlappingPendingReservationsAsync(Guid facilityId, DateTime date, List<string> timeSlots, Guid userId)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        var pendingReservations = await _context.PendingTimeSlotReservations
            .Where(p => p.FacilityId == facilityId &&
                       p.Date == utcDate &&
                       p.UserId == userId)
            .ToListAsync();

        // Only remove reservations that have overlapping timeslots
        var overlapping = pendingReservations
            .Where(p => p.TimeSlots.Any(slot => timeSlots.Contains(slot)))
            .ToList();

        if (!overlapping.Any())
            return false;

        _context.PendingTimeSlotReservations.RemoveRange(overlapping);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> CleanupExpiredPendingReservationsAsync()
    {
        var now = DateTime.UtcNow;
        
        var expiredReservations = await _context.PendingTimeSlotReservations
            .Where(p => p.ExpiresAt <= now)
            .ToListAsync();

        if (!expiredReservations.Any())
            return 0;

        _context.PendingTimeSlotReservations.RemoveRange(expiredReservations);
        await _context.SaveChangesAsync();
        
        return expiredReservations.Count;
    }

    public async Task<bool> ExtendPendingReservationAsync(Guid pendingReservationId, int additionalMinutes = 15)
    {
        var pendingReservation = await _context.PendingTimeSlotReservations
            .FirstOrDefaultAsync(p => p.Id == pendingReservationId);

        if (pendingReservation == null || pendingReservation.ExpiresAt <= DateTime.UtcNow)
            return false;

        pendingReservation.ExpiresAt = pendingReservation.ExpiresAt.AddMinutes(additionalMinutes);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<string>> GetPendingTimeSlotsAsync(Guid facilityId, DateTime date, Guid? excludeUserId = null)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var now = DateTime.UtcNow;

        var pendingReservations = await _context.PendingTimeSlotReservations
            .Where(p => p.FacilityId == facilityId &&
                       p.Date == utcDate &&
                       p.ExpiresAt > now &&
                       (excludeUserId == null || p.UserId != excludeUserId))
            .ToListAsync();

        // Flatten all pending time slots into a single list
        return pendingReservations
            .SelectMany(p => p.TimeSlots)
            .Distinct()
            .ToList();
    }
}