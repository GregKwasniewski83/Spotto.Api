using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class TimeSlotService : ITimeSlotService
{
    private readonly ITimeSlotRepository _timeSlotRepository;
    private readonly IFacilityRepository _facilityRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IBusinessProfileAgentRepository _businessProfileAgentRepository;
    private readonly IPendingTimeSlotReservationRepository _pendingReservationRepository;

    public TimeSlotService(
        ITimeSlotRepository timeSlotRepository,
        IFacilityRepository facilityRepository,
        IReservationRepository reservationRepository,
        IBusinessProfileAgentRepository businessProfileAgentRepository,
        IPendingTimeSlotReservationRepository pendingReservationRepository)
    {
        _timeSlotRepository = timeSlotRepository;
        _facilityRepository = facilityRepository;
        _reservationRepository = reservationRepository;
        _businessProfileAgentRepository = businessProfileAgentRepository;
        _pendingReservationRepository = pendingReservationRepository;
    }

    public void UpdateFacilityTimeSlots(Guid facilityId, UpdateTimeSlotsDto updateDto, Guid userId)
    {
        var facility = _facilityRepository.GetFacility(facilityId);
        if (facility == null)
        {
            throw new ArgumentException("Facility not found");
        }

        // Check if user is the facility owner OR an authorized agent
        var isFacilityOwner = facility.UserId == userId;
        var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
            _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(userId, facility.BusinessProfileId.Value).Result;

        if (!isFacilityOwner && !isAuthorizedAgent)
        {
            throw new UnauthorizedAccessException("You can only manage time slots for facilities you own or manage as an agent");
        }

        _timeSlotRepository.UpdateTimeSlots(facilityId, updateDto);
    }

    public GetTimeSlotsResponseDto GetFacilityTimeSlots(Guid facilityId)
    {
        return _timeSlotRepository.GetFacilityTimeSlotsStructured(facilityId);
    }

    public Dictionary<string, List<TimeSlotItemDto>> GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date)
    {
        // This now returns the fully merged view for a specific date:
        // Business template + Facility overrides + Date-specific exceptions
        var timeSlots = _timeSlotRepository.GetFacilityTimeSlotsForDate(facilityId, date);
        var result = new Dictionary<string, List<TimeSlotItemDto>>();
        
        var dateKey = date.ToString("yyyy-MM-dd");
        result[dateKey] = new List<TimeSlotItemDto>();

        foreach (var timeSlot in timeSlots)
        {
            result[dateKey].Add(new TimeSlotItemDto
            {
                Id = timeSlot.Time,
                Time = timeSlot.Time,
                IsAvailable = timeSlot.IsAvailable,
                IsBooked = timeSlot.IsBooked,
                BookedBy = timeSlot.BookedByUserId?.ToString()
            });
        }

        return result;
    }

    public async Task<List<TimeSlotItemDto>> GetAvailableTimeSlotsAsync(Guid facilityId, DateTime date, Guid? currentUserId = null)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Get the effective time slots for the date (business template + facility overrides + date-specific)
        var effectiveTimeSlots = _timeSlotRepository.GetFacilityTimeSlotsForDate(facilityId, utcDate);

        // Get reserved time slots from active reservations
        var reservedTimeSlots = _reservationRepository.GetReservedTimeSlots(facilityId, utcDate);

        // Get ALL pending time slots (including current user's) to prevent double booking
        // Users should not be able to initiate a new payment for slots they already have pending
        var pendingTimeSlots = await _pendingReservationRepository.GetPendingTimeSlotsAsync(facilityId, utcDate, excludeUserId: null);

        // Filter to only available and not booked slots, exclude reserved and pending time slots
        var availableSlots = effectiveTimeSlots
            .Where(ts => ts.IsAvailable &&
                        !ts.IsBooked &&
                        !reservedTimeSlots.Contains(ts.Time) &&
                        !pendingTimeSlots.Contains(ts.Time))
            .Select(ts => new TimeSlotItemDto
            {
                Id = ts.Id.ToString(),
                Time = ts.Time,
                IsAvailable = ts.IsAvailable,
                IsBooked = ts.IsBooked,
                BookedBy = ts.BookedByUserId?.ToString()
            })
            .OrderBy(ts => ts.Time)
            .ToList();

        return availableSlots;
    }
}