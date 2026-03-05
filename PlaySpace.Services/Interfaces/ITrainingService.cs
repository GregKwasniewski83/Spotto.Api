using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces;

public interface ITrainingService
{
    List<TrainingDto> GetTrainerTrainings(Guid trainerId);
    TrainingDto? GetTraining(Guid id);
    Task<TrainingDto?> GetTrainingAsync(Guid id);
    TrainingDto CreateTraining(CreateTrainingDto trainingDto, Guid trainerId);
    TrainingDto? UpdateTraining(Guid id, UpdateTrainingDto trainingDto, Guid trainerId);
    bool DeleteTraining(Guid id, Guid trainerId);
    
    // Participant management
    Task<PendingTrainingParticipantDto> ReserveTrainingSpotAsync(Guid trainingId, Guid userId, string? notes = null);
    Task<TrainingParticipantDto> JoinTrainingAsync(Guid trainingId, Guid userId, JoinTrainingDto joinDto);
    TrainingParticipantDto? JoinTraining(Guid trainingId, Guid userId, JoinTrainingDto joinDto);
    Task<PendingTrainingParticipant> CreatePendingParticipantAsync(Guid trainingId, Guid userId, string? notes = null);
    bool LeaveTraining(Guid trainingId, Guid userId);
    bool RemoveParticipant(Guid trainingId, Guid participantUserId, Guid trainerProfileId);
    bool UpdateParticipantStatus(Guid trainingId, Guid userId, UpdateParticipantStatusDto statusDto);
    List<TrainingDto> GetUserTrainings(Guid userId);
    List<TrainingParticipantDto> GetTrainingParticipants(Guid trainingId);
    
    // Search methods
    List<TrainingSearchResultDto> SearchTrainings(TrainingSearchDto searchDto);
    List<TrainingSearchResultDto> GetUpcomingTrainings();
    List<TrainingDto> GetUserUpcomingTrainings(Guid userId);
    
    // Stats methods
    int GetTotalTrainingsCount();
}