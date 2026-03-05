using Microsoft.AspNetCore.Http;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces;

public interface ITrainerProfileService
{
    TrainerProfileDto? GetTrainerProfile(Guid userId);
    TrainerProfileDto? GetTrainerProfileById(Guid trainerProfileId);
    Task<TrainerProfileDto> CreateTrainerProfile(CreateTrainerProfileDto profileDto, Guid userId);
    Task<TrainerProfileDto?> UpdateTrainerProfile(Guid userId, UpdateTrainerProfileDto profileDto);
    bool DeleteTrainerProfile(Guid userId);
    Task<string> UploadAvatarAsync(Guid userId, IFormFile avatar);
    bool AssociateBusinessProfile(Guid trainerUserId, string businessProfileId);
    bool DisassociateBusinessProfile(Guid trainerUserId, string businessProfileId);
    BusinessAssociationResultDto AssociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds);
    BusinessAssociationResultDto DisassociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds);
    List<BusinessProfileDto> GetAssociatedBusinessProfiles(Guid trainerUserId);
    List<AvailableTrainerDto> FindAvailableTrainers(DateTime date, List<string> timeSlots);

    /// <summary>
    /// Find available trainers for a specific business.
    /// Only returns trainers with slots assigned to that business.
    /// </summary>
    List<AvailableTrainerDto> FindAvailableTrainersForBusiness(Guid businessProfileId, DateTime date, List<string> timeSlots);

    TrainerDateTimeSlotsDto? GetMyTimeSlotsForDate(Guid userId, DateTime date);

    /// <summary>
    /// Update trainer schedule templates with business assignments.
    /// Validates that time slots fall within business operating hours when assigned.
    /// </summary>
    Task UpdateTrainerScheduleWithBusinessAsync(Guid userId, UpdateTrainerScheduleWithBusinessDto dto);

    /// <summary>
    /// Update trainer date-specific availability with business assignments.
    /// Validates that time slots fall within business operating hours when assigned.
    /// </summary>
    Task UpdateTrainerDateAvailabilityWithBusinessAsync(Guid userId, UpdateTrainerDateAvailabilityWithBusinessDto dto);

    // TPay registration methods
    Task<TrainerProfileDto> RegisterWithTPayAsync(Guid userId, TPayBusinessRegistrationRequest request);
    Task<TrainerProfileDto> UpdateTPayMerchantDataAsync(Guid userId, TPayBusinessRegistrationResponse response);
}