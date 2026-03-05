using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces
{
    public interface IFacilityService
    {
        List<Facility> GetFacilities(SearchFiltersDto filters);
        List<FacilitySearchResultDto> SearchFacilities(SearchFiltersDto filters);
        FacilityDto CreateFacility(CreateFacilityDto facilityDto, Guid userId);
        FacilityDto? GetFacility(Guid id);
        List<FacilityDto> GetUserFacilities(Guid userId);
        bool DeleteFacility(Guid facilityId, Guid userId);
        FacilityDto? UpdateFacility(Guid facilityId, UpdateFacilityDto facilityDto, Guid userId);
        void UpdateFacilityScheduleTemplates(Guid facilityId, FacilityAvailabilityDto availability, Guid userId);
        FacilityDateTimeSlotsDto? GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date);

        // Agent Dashboard methods
        FacilityDateTimeSlotsWithBookingsDto? GetFacilityTimeSlotsWithBookings(Guid facilityId, DateTime date, Guid userId);
    }
}
