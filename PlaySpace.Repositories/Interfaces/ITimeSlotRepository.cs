using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface ITimeSlotRepository
{
    List<TimeSlot> GetFacilityTimeSlots(Guid facilityId);
    GetTimeSlotsResponseDto GetFacilityTimeSlotsStructured(Guid facilityId);
    List<TimeSlot> GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date);
    void UpdateTimeSlots(Guid facilityId, UpdateTimeSlotsDto updateDto);
    TimeSlot? GetTimeSlot(Guid facilityId, string time, DateTime? date);
}