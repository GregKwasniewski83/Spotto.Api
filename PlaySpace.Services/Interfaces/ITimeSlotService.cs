using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface ITimeSlotService
{
    void UpdateFacilityTimeSlots(Guid facilityId, UpdateTimeSlotsDto updateDto, Guid userId);
    GetTimeSlotsResponseDto GetFacilityTimeSlots(Guid facilityId);
    Dictionary<string, List<TimeSlotItemDto>> GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date);
    Task<List<TimeSlotItemDto>> GetAvailableTimeSlotsAsync(Guid facilityId, DateTime date, Guid? currentUserId = null);
}