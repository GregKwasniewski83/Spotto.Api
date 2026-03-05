using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface ITrainerProfileRepository
{
    TrainerProfile? GetTrainerProfile(Guid userId);
    TrainerProfile? GetTrainerProfileById(Guid trainerProfileId);
    TrainerProfile CreateTrainerProfile(CreateTrainerProfileDto profileDto, Guid userId);
    TrainerProfile? UpdateTrainerProfile(Guid userId, UpdateTrainerProfileDto profileDto);
    void UpdateTrainerProfile(TrainerProfile profile);
    bool DeleteTrainerProfile(Guid userId);
    bool TrainerProfileExists(Guid userId);
    bool NipExists(string nip, Guid? excludeUserId = null);
    Task UpdateAvatarAsync(Guid userId, string avatarUrl);
    void UpdateTrainerScheduleTemplates(Guid trainerProfileId, TrainerAvailabilityDto availability);
    bool AssociateBusinessProfile(Guid trainerUserId, string businessProfileId);
    bool DisassociateBusinessProfile(Guid trainerUserId, string businessProfileId);
    BusinessAssociationResultDto AssociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds);
    BusinessAssociationResultDto DisassociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds);
    List<string> GetAssociatedBusinessIds(Guid trainerUserId);
    List<(TrainerProfile trainer, List<string> availableSlots)> FindAvailableTrainers(DateTime date, List<string> timeSlots);

    /// <summary>
    /// Find available trainers for a specific business (only returns trainers with slots assigned to that business).
    /// </summary>
    List<(TrainerProfile trainer, List<string> availableSlots)> FindAvailableTrainersForBusiness(Guid businessProfileId, DateTime date, List<string> timeSlots);

    TrainerDateTimeSlotsDto? GetMyTimeSlotsForDate(Guid userId, DateTime date);

    /// <summary>
    /// Update trainer schedule templates with business assignments.
    /// </summary>
    Task UpdateTrainerScheduleTemplatesWithBusinessAsync(Guid trainerProfileId, ScheduleType scheduleType, List<SetTrainerTimeSlotDto> timeSlots);

    /// <summary>
    /// Update trainer date-specific availability with business assignments.
    /// </summary>
    Task UpdateTrainerDateAvailabilityWithBusinessAsync(Guid trainerProfileId, DateTime date, List<SetTrainerTimeSlotDto> timeSlots);
}