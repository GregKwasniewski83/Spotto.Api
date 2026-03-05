using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces
{
    public interface IFacilityRepository
    {
        List<Facility> GetFacilities(SearchFiltersDto filters);
        List<Facility> SearchFacilities(SearchFiltersDto filters);
        Facility CreateFacility(CreateFacilityDto facilityDto, Guid userId);
        Facility? GetFacility(Guid id);
        List<Facility> GetUserFacilities(Guid userId);
        bool DeleteFacility(Guid id);
        Facility? UpdateFacility(Guid id, UpdateFacilityDto facilityDto);
        void UpdateFacilityScheduleTemplates(Guid facilityId, FacilityAvailabilityDto availability);
        FacilityDateTimeSlotsDto? GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date);
    }
}
