using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class TrainingService : ITrainingService
{
    private readonly ITrainingRepository _trainingRepository;
    private readonly ITrainerProfileRepository _trainerProfileRepository;
    private readonly IPaymentService _paymentService;
    private readonly IPendingTrainingParticipantRepository _pendingParticipantRepository;

    public TrainingService(
        ITrainingRepository trainingRepository, 
        ITrainerProfileRepository trainerProfileRepository,
        IPaymentService paymentService,
        IPendingTrainingParticipantRepository pendingParticipantRepository)
    {
        _trainingRepository = trainingRepository;
        _trainerProfileRepository = trainerProfileRepository;
        _paymentService = paymentService;
        _pendingParticipantRepository = pendingParticipantRepository;
    }

    public List<TrainingDto> GetTrainerTrainings(Guid trainerId)
    {
        var trainings = _trainingRepository.GetTrainerTrainings(trainerId);

        // Filter to only show trainings with upcoming sessions
        var today = DateTime.UtcNow.Date;
        var trainingsWithUpcomingSessions = trainings
            .Where(t => t.Sessions != null && t.Sessions.Any(s => s.Date >= today))
            .ToList();

        return trainingsWithUpcomingSessions.Select(MapToDto).ToList();
    }

    public TrainingDto? GetTraining(Guid id)
    {
        var training = _trainingRepository.GetTraining(id);
        return training == null ? null : MapToDto(training);
    }

    public async Task<TrainingDto?> GetTrainingAsync(Guid id)
    {
        var training = _trainingRepository.GetTraining(id);
        return training == null ? null : await MapToDtoAsync(training);
    }

    public TrainingDto CreateTraining(CreateTrainingDto trainingDto, Guid trainerId)
    {
        // Verify trainer exists
        var trainerProfile = _trainerProfileRepository.GetTrainerProfileById(trainerId);
        if (trainerProfile == null)
        {
            throw new ArgumentException("Trainer profile not found", nameof(trainerId));
        }

        if (trainerProfile.TrainerType == Domain.Models.TrainerType.Employee)
        {
            throw new InvalidOperationException("Employee trainers cannot create their own trainings");
        }

        var training = _trainingRepository.CreateTraining(trainingDto, trainerId);
        return MapToDto(training);
    }

    public TrainingDto? UpdateTraining(Guid id, UpdateTrainingDto trainingDto, Guid trainerId)
    {
        // Verify ownership
        var existingTraining = _trainingRepository.GetTraining(id);
        if (existingTraining == null)
            return null;

        if (existingTraining.TrainerProfileId != trainerId)
        {
            throw new UnauthorizedAccessException("You can only update your own trainings");
        }

        var updatedTraining = _trainingRepository.UpdateTraining(id, trainingDto);
        return updatedTraining == null ? null : MapToDto(updatedTraining);
    }

    public bool DeleteTraining(Guid id, Guid trainerId)
    {
        // Verify ownership
        var existingTraining = _trainingRepository.GetTraining(id);
        if (existingTraining == null)
            return false;

        if (existingTraining.TrainerProfileId != trainerId)
        {
            throw new UnauthorizedAccessException("You can only delete your own trainings");
        }

        return _trainingRepository.DeleteTraining(id);
    }

    private TrainingDto MapToDto(Training training)
    {
        var activeParticipants = training.Participants.Where(p => p.Status == "Active").ToList();
        
        return new TrainingDto
        {
            Id = training.Id,
            TrainerProfileId = training.TrainerProfileId,
            TrainerProfile = MapTrainerProfileToDto(training.TrainerProfile),
            FacilityId = training.FacilityId,
            Facility = MapFacilityToDto(training.Facility),
            ReservationId = training.ReservationId,
            Title = training.Title,
            Description = training.Description,
            Specialization = training.Specialization,
            Duration = training.Duration,
            MaxParticipants = training.MaxParticipants,
            Price = training.Price,
            GrossPrice = training.GrossPrice,
            VatRate = training.VatRate,
            CreatedAt = training.CreatedAt,
            UpdatedAt = training.UpdatedAt,
            Sessions = training.Sessions.Select(MapSessionToDto).ToList(),
            Participants = training.Participants.Select(MapParticipantToDto).ToList(),
            CurrentParticipantCount = activeParticipants.Count,
            PendingParticipantCount = 0 // Will be set by calling method if needed
        };
    }

    private async Task<TrainingDto> MapToDtoAsync(Training training)
    {
        var activeParticipants = training.Participants.Where(p => p.Status == "Active").ToList();
        var pendingCount = await _pendingParticipantRepository.GetPendingCountAsync(training.Id);

        return new TrainingDto
        {
            Id = training.Id,
            TrainerProfileId = training.TrainerProfileId,
            TrainerProfile = MapTrainerProfileToDto(training.TrainerProfile),
            FacilityId = training.FacilityId,
            Facility = MapFacilityToDto(training.Facility),
            ReservationId = training.ReservationId,
            Title = training.Title,
            Description = training.Description,
            Specialization = training.Specialization,
            Duration = training.Duration,
            MaxParticipants = training.MaxParticipants,
            Price = training.Price,
            GrossPrice = training.GrossPrice,
            VatRate = training.VatRate,
            CreatedAt = training.CreatedAt,
            UpdatedAt = training.UpdatedAt,
            Sessions = training.Sessions.Select(MapSessionToDto).ToList(),
            Participants = training.Participants.Select(MapParticipantToDto).ToList(),
            CurrentParticipantCount = activeParticipants.Count,
            PendingParticipantCount = pendingCount
        };
    }

    private TrainingSessionDto MapSessionToDto(TrainingSession session)
    {
        return new TrainingSessionDto
        {
            Id = session.Id,
            TrainingId = session.TrainingId,
            Date = session.Date,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            CurrentParticipants = session.CurrentParticipants,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt
        };
    }

    private TrainingParticipantDto MapParticipantToDto(TrainingParticipant participant)
    {
        return new TrainingParticipantDto
        {
            Id = participant.Id,
            TrainingId = participant.TrainingId,
            UserId = participant.UserId,
            JoinedAt = participant.JoinedAt,
            Status = participant.Status,
            Notes = participant.Notes,
            CreatedAt = participant.CreatedAt,
            UpdatedAt = participant.UpdatedAt,
            UserFirstName = participant.User?.FirstName,
            UserLastName = participant.User?.LastName,
            UserEmail = participant.User?.Email
        };
    }

    public async Task<PendingTrainingParticipantDto> ReserveTrainingSpotAsync(Guid trainingId, Guid userId, string? notes = null)
    {
        // Check training exists and has capacity
        var training = _trainingRepository.GetTraining(trainingId);
        if (training == null)
            throw new ArgumentException("Training not found");

        // Check capacity including pending participants
        var activeParticipants = training.Participants.Count(p => p.Status == "Active");
        var pendingParticipants = await _pendingParticipantRepository.GetPendingCountAsync(trainingId);
        
        if (activeParticipants + pendingParticipants >= training.MaxParticipants)
            throw new InvalidOperationException("Training is full or no spots available");

        var pendingParticipant = await _pendingParticipantRepository.CreatePendingParticipantAsync(trainingId, userId, notes);
        
        return new PendingTrainingParticipantDto
        {
            Id = pendingParticipant.Id,
            TrainingId = pendingParticipant.TrainingId,
            UserId = pendingParticipant.UserId,
            Notes = pendingParticipant.Notes,
            ExpiresAt = pendingParticipant.ExpiresAt,
            CreatedAt = pendingParticipant.CreatedAt
        };
    }

    public async Task<TrainingParticipantDto> JoinTrainingAsync(Guid trainingId, Guid userId, JoinTrainingDto joinDto)
    {
        // Validate payment exists and is completed
        var payment = await _paymentService.GetPaymentByIdAsync(joinDto.PaymentId);
        if (payment == null)
            throw new ArgumentException("Payment not found");
        
        if (payment.Status != "COMPLETED")
            throw new InvalidOperationException("Payment is not completed");
        
        if (payment.IsConsumed)
            throw new InvalidOperationException("Payment has already been used");

        // Validate payment amount matches training cost
        await ValidatePaymentAmount(payment, trainingId);

        var participant = _trainingRepository.JoinTraining(trainingId, userId, joinDto.Notes, joinDto.PaymentId);
        
        // Remove any pending participant for this user/training since participant was created successfully
        await _pendingParticipantRepository.RemoveUserPendingParticipantAsync(trainingId, userId);
        
        return MapParticipantToDto(participant);
    }

    public TrainingParticipantDto? JoinTraining(Guid trainingId, Guid userId, JoinTrainingDto joinDto)
    {
        // Legacy sync method - calls async version
        return JoinTrainingAsync(trainingId, userId, joinDto).Result;
    }

    public async Task<PendingTrainingParticipant> CreatePendingParticipantAsync(Guid trainingId, Guid userId, string? notes = null)
    {
        return await _pendingParticipantRepository.CreatePendingParticipantAsync(trainingId, userId, notes);
    }

    public bool LeaveTraining(Guid trainingId, Guid userId)
    {
        return _trainingRepository.LeaveTraining(trainingId, userId);
    }

    public bool RemoveParticipant(Guid trainingId, Guid participantUserId, Guid trainerProfileId)
    {
        // First verify the trainer owns this training
        var training = _trainingRepository.GetTraining(trainingId);
        if (training == null || training.TrainerProfileId != trainerProfileId)
        {
            throw new UnauthorizedAccessException("You can only remove participants from your own trainings");
        }

        // Remove the participant
        return _trainingRepository.LeaveTraining(trainingId, participantUserId);
    }

    public bool UpdateParticipantStatus(Guid trainingId, Guid userId, UpdateParticipantStatusDto statusDto)
    {
        return _trainingRepository.UpdateParticipantStatus(trainingId, userId, statusDto.Status, statusDto.Notes);
    }

    public List<TrainingDto> GetUserTrainings(Guid userId)
    {
        var trainings = _trainingRepository.GetUserTrainings(userId);
        return trainings.Select(MapToDto).ToList();
    }

    public List<TrainingParticipantDto> GetTrainingParticipants(Guid trainingId)
    {
        var participants = _trainingRepository.GetTrainingParticipants(trainingId);
        return participants.Select(MapParticipantToDto).ToList();
    }

    private FacilityDto? MapFacilityToDto(Facility? facility)
    {
        if (facility == null) return null;

        return new FacilityDto
        {
            Id = facility.Id,
            Name = facility.Name,
            Type = facility.Type,
            Description = facility.Description,
            Capacity = facility.Capacity,
            MaxUsers = facility.MaxUsers,
            PricePerUser = facility.PricePerUser,
            PricePerHour = facility.PricePerHour,
            GrossPricePerHour = facility.GrossPricePerHour,
            VatRate = facility.VatRate,
            UserId = facility.UserId,
            Street = facility.Street,
            City = facility.City,
            State = facility.State,
            PostalCode = facility.PostalCode,
            Country = facility.Country,
            AddressLine2 = facility.AddressLine2,
            Latitude = facility.Latitude,
            Longitude = facility.Longitude,
            CreatedAt = facility.CreatedAt,
            UpdatedAt = facility.UpdatedAt,
            Availability = null // Don't include availability in training context for performance
        };
    }

    private TrainerProfileDto? MapTrainerProfileToDto(TrainerProfile? trainerProfile)
    {
        if (trainerProfile == null) return null;

        return new TrainerProfileDto
        {
            Id = trainerProfile.Id,
            UserId = trainerProfile.UserId,
            Nip = trainerProfile.Nip,
            CompanyName = trainerProfile.CompanyName,
            DisplayName = trainerProfile.DisplayName,
            Address = trainerProfile.Address,
            City = trainerProfile.City,
            PostalCode = trainerProfile.PostalCode,
            AvatarUrl = trainerProfile.AvatarUrl,
            Specializations = trainerProfile.Specializations,
            HourlyRate = trainerProfile.HourlyRate,
            Description = trainerProfile.Description,
            Certifications = trainerProfile.Certifications,
            Languages = trainerProfile.Languages,
            ExperienceYears = trainerProfile.ExperienceYears,
            Rating = trainerProfile.Rating,
            TotalSessions = trainerProfile.TotalSessions,
            AssociatedBusinessIds = trainerProfile.AssociatedBusinessIds,
            CreatedAt = trainerProfile.CreatedAt,
            UpdatedAt = trainerProfile.UpdatedAt,
            Availability = null // Don't include availability in training context for performance
        };
    }

    public List<TrainingSearchResultDto> SearchTrainings(TrainingSearchDto searchDto)
    {
        var trainings = _trainingRepository.SearchTrainings(searchDto);
        return trainings.Select(MapToSearchResultDto).ToList();
    }

    public List<TrainingSearchResultDto> GetUpcomingTrainings()
    {
        var trainings = _trainingRepository.GetUpcomingTrainings();
        return trainings.Select(MapToSearchResultDto).ToList();
    }

    public List<TrainingDto> GetUserUpcomingTrainings(Guid userId)
    {
        var trainings = _trainingRepository.GetUserUpcomingTrainings(userId);
        return trainings.Select(MapToDto).ToList();
    }

    private TrainingSearchResultDto MapToSearchResultDto(Training training)
    {
        var activeParticipants = training.Participants.Where(p => p.Status == "Active").ToList();
        
        return new TrainingSearchResultDto
        {
            Id = training.Id,
            Title = training.Title,
            Description = training.Description,
            Specialization = training.Specialization,
            Duration = training.Duration,
            MaxParticipants = training.MaxParticipants,
            CurrentParticipantCount = activeParticipants.Count,
            Price = training.Price,
            GrossPrice = training.GrossPrice,
            VatRate = training.VatRate,
            Trainer = MapTrainerToSearchResultDto(training.TrainerProfile),
            Facility = MapFacilityToSearchResultDto(training.Facility),
            Sessions = training.Sessions.Select(MapSessionToDto).ToList(),
            Participants = training.Participants.Select(MapParticipantToDto).ToList(),
            CreatedAt = training.CreatedAt,
            UpdatedAt = training.UpdatedAt
        };
    }

    private TrainerSearchResultDto? MapTrainerToSearchResultDto(TrainerProfile? trainerProfile)
    {
        if (trainerProfile == null) return null;

        return new TrainerSearchResultDto
        {
            Id = trainerProfile.Id,
            DisplayName = trainerProfile.DisplayName,
            AvatarUrl = trainerProfile.AvatarUrl,
            Specializations = trainerProfile.Specializations,
            HourlyRate = trainerProfile.HourlyRate,
            Description = trainerProfile.Description,
            Certifications = trainerProfile.Certifications,
            Languages = trainerProfile.Languages,
            ExperienceYears = trainerProfile.ExperienceYears,
            Rating = trainerProfile.Rating,
            TotalSessions = trainerProfile.TotalSessions
        };
    }

    private FacilitySearchResultDto? MapFacilityToSearchResultDto(Facility? facility)
    {
        if (facility == null) return null;

        return new FacilitySearchResultDto
        {
            Id = facility.Id,
            Name = facility.Name,
            Type = facility.Type,
            Description = facility.Description,
            Capacity = facility.Capacity,
            MaxUsers = facility.MaxUsers,
            PricePerUser = facility.PricePerUser,
            PricePerHour = facility.PricePerHour,
            GrossPricePerHour = facility.GrossPricePerHour,
            VatRate = facility.VatRate,
            UserId = facility.UserId,
            Street = facility.Street,
            City = facility.City,
            State = facility.State,
            PostalCode = facility.PostalCode,
            Country = facility.Country,
            AddressLine2 = facility.AddressLine2,
            Latitude = facility.Latitude,
            Longitude = facility.Longitude,
            CreatedAt = facility.CreatedAt,
            UpdatedAt = facility.UpdatedAt,
            HasAvailability = true, // Could implement proper availability check
            AvailableSlots = 0, // Could calculate available slots
            AvailableTimes = new List<string>() // Could get available times
        };
    }

    private async Task ValidatePaymentAmount(PaymentDto payment, Guid trainingId)
    {
        // Get training to validate cost
        var training = _trainingRepository.GetTraining(trainingId);
        if (training == null)
            throw new ArgumentException("Training not found");

        var expectedCost = training.Price;

        if (payment.Amount != expectedCost)
        {
            throw new InvalidOperationException(
                $"Payment amount ({payment.Amount:C}) does not match training cost ({expectedCost:C})");
        }
    }
    
    public int GetTotalTrainingsCount()
    {
        return _trainingRepository.GetTotalCount();
    }
}