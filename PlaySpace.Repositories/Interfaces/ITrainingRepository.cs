using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface ITrainingRepository
{
    List<Training> GetTrainerTrainings(Guid trainerId);
    Training? GetTraining(Guid id);
    Training CreateTraining(CreateTrainingDto trainingDto, Guid trainerId);
    Training? UpdateTraining(Guid id, UpdateTrainingDto trainingDto);
    bool DeleteTraining(Guid id);
    
    // Participant management
    TrainingParticipant? JoinTraining(Guid trainingId, Guid userId, string? notes, Guid paymentId);
    bool LeaveTraining(Guid trainingId, Guid userId);
    bool UpdateParticipantStatus(Guid trainingId, Guid userId, string status, string? notes);
    TrainingParticipant? GetParticipant(Guid trainingId, Guid userId);
    List<TrainingParticipant> GetTrainingParticipants(Guid trainingId);
    List<Training> GetUserTrainings(Guid userId);
    
    // Search methods
    List<Training> SearchTrainings(TrainingSearchDto searchDto);
    List<Training> GetUpcomingTrainings();
    List<Training> GetUserUpcomingTrainings(Guid userId);
    
    // Stats methods
    int GetTotalCount();
}