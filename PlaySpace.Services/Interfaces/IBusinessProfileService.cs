using Microsoft.AspNetCore.Http;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces;

public interface IBusinessProfileService
{
    BusinessProfileDto? GetBusinessProfileByUserId(Guid userId);
    BusinessProfileDto? GetBusinessProfileById(Guid businessProfileId);
    Task<BusinessProfileDto> CreateBusinessProfile(CreateBusinessProfileDto profileDto, Guid userId);
    Task<BusinessProfileDto?> UpdateBusinessProfile(Guid businessProfileId, UpdateBusinessProfileDto profileDto);
    bool DeleteBusinessProfile(Guid businessProfileId);
    List<ScheduleSlotDto> GetScheduleForDate(Guid businessProfileId, DateTime date);
    Task<string> UploadAvatarAsync(Guid businessProfileId, IFormFile avatar);
    Task<FacilityPlanUploadResult> ProcessFacilityPlanUploadAsync(IFormFile facilityPlan);

    // Date-specific availability method (for internal use)
    BusinessDateAvailabilityDto? GetDateAvailability(Guid businessProfileId, DateTime date);

    // TPay merchant integration methods
    Task<BusinessProfileDto> RegisterWithTPayAsync(Guid businessProfileId, TPayBusinessRegistrationRequest request);
    Task<BusinessProfileDto> UpdateTPayMerchantDataAsync(Guid businessProfileId, TPayBusinessRegistrationResponse response);

    // KSeF integration methods
    Task<KSeFConfigurationDto> GetKSeFConfigurationAsync(Guid businessProfileId);
    Task<bool> ConfigureKSeFAsync(Guid businessProfileId, ConfigureKSeFDto configDto);
    Task<bool> UpdateKSeFStatusAsync(Guid businessProfileId, bool enabled);
    Task<KSeFConnectionTestDto> TestKSeFConnectionAsync(Guid businessProfileId);

    // Agent Dashboard methods
    BusinessProfileWithFacilitiesDto? GetBusinessProfileWithFacilities(Guid businessProfileId);
}