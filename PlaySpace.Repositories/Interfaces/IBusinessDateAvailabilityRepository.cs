using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface IBusinessDateAvailabilityRepository
{
    BusinessDateAvailabilityDto? GetBusinessDateAvailability(Guid businessProfileId, DateTime date);
    BusinessDateAvailabilityDto CreateBusinessDateAvailability(Guid businessProfileId, CreateBusinessDateAvailabilityDto dto);
    BusinessDateAvailabilityDto? UpdateBusinessDateAvailability(Guid businessProfileId, UpdateBusinessDateAvailabilityDto dto);
    bool DeleteBusinessDateAvailability(Guid businessProfileId, DateTime date);
    List<BusinessDateAvailability> GetBusinessDateAvailabilities(Guid businessProfileId, DateTime startDate, DateTime endDate);
}