using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories;

public class TrainingRepository : ITrainingRepository
{
    private readonly PlaySpaceDbContext _context;

    public TrainingRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public List<Training> GetTrainerTrainings(Guid trainerId)
    {
        return _context.Trainings
            .Include(t => t.Sessions)
            .Include(t => t.Facility)
            .Include(t => t.Participants)
                .ThenInclude(p => p.User)
            .Where(t => t.TrainerProfileId == trainerId)
            .OrderBy(t => t.CreatedAt)
            .ToList();
    }

    public Training? GetTraining(Guid id)
    {
        return _context.Trainings
            .Include(t => t.Sessions)
            .Include(t => t.Facility)
            .Include(t => t.TrainerProfile)
            .Include(t => t.Participants)
                .ThenInclude(p => p.User)
            .FirstOrDefault(t => t.Id == id);
    }

    public Training CreateTraining(CreateTrainingDto trainingDto, Guid trainerId)
    {
        var training = new Training
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainerId,
            FacilityId = trainingDto.FacilityId,
            ReservationId = trainingDto.ReservationId,
            Title = trainingDto.Title,
            Description = trainingDto.Description,
            Duration = trainingDto.Duration,
            MaxParticipants = trainingDto.MaxParticipants,
            Price = trainingDto.Price,
            GrossPrice = trainingDto.GrossPrice,
            VatRate = trainingDto.VatRate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Specialization = trainingDto.Specialization
        };

        _context.Trainings.Add(training);

        // Add training sessions
        foreach (var sessionDto in trainingDto.Sessions)
        {
            if (DateTime.TryParse(sessionDto.Date, out var sessionDate))
            {
                var session = new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    TrainingId = training.Id,
                    Date = DateTime.SpecifyKind(sessionDate.Date, DateTimeKind.Utc),
                    StartTime = sessionDto.StartTime,
                    EndTime = sessionDto.EndTime,
                    CurrentParticipants = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.TrainingSessions.Add(session);
            }
        }

        _context.SaveChanges();
        return GetTraining(training.Id)!;
    }

    public Training? UpdateTraining(Guid id, UpdateTrainingDto trainingDto)
    {
        var training = _context.Trainings
            .Include(t => t.Sessions)
            .FirstOrDefault(t => t.Id == id);

        if (training == null)
            return null;

        // Update training properties
        training.FacilityId = trainingDto.FacilityId;
        training.ReservationId = trainingDto.ReservationId;
        training.Title = trainingDto.Title;
        training.Description = trainingDto.Description;
        training.Duration = trainingDto.Duration;
        training.MaxParticipants = trainingDto.MaxParticipants;
        training.Price = trainingDto.Price;
        training.GrossPrice = trainingDto.GrossPrice;
        training.VatRate = trainingDto.VatRate;
        training.UpdatedAt = DateTime.UtcNow;
        training.Specialization = trainingDto.Specialization;

        // Remove existing sessions
        _context.TrainingSessions.RemoveRange(training.Sessions);

        // Add new sessions
        foreach (var sessionDto in trainingDto.Sessions)
        {
            if (DateTime.TryParse(sessionDto.Date, out var sessionDate))
            {
                var session = new TrainingSession
                {
                    Id = Guid.NewGuid(),
                    TrainingId = training.Id,
                    Date = DateTime.SpecifyKind(sessionDate.Date, DateTimeKind.Utc),
                    StartTime = sessionDto.StartTime,
                    EndTime = sessionDto.EndTime,
                    CurrentParticipants = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.TrainingSessions.Add(session);
            }
        }

        _context.SaveChanges();
        return GetTraining(training.Id)!;
    }

    public bool DeleteTraining(Guid id)
    {
        var training = _context.Trainings
            .Include(t => t.Sessions)
            .FirstOrDefault(t => t.Id == id);

        if (training == null)
            return false;

        // Remove sessions first (cascade should handle this, but being explicit)
        _context.TrainingSessions.RemoveRange(training.Sessions);
        _context.Trainings.Remove(training);
        
        _context.SaveChanges();
        return true;
    }

    public TrainingParticipant? JoinTraining(Guid trainingId, Guid userId, string? notes, Guid paymentId)
    {
        // Check if user is already a participant
        var existingParticipant = _context.TrainingParticipants
            .FirstOrDefault(tp => tp.TrainingId == trainingId && tp.UserId == userId);

        if (existingParticipant != null)
        {
            // If cancelled, reactivate; otherwise throw exception for active participant
            if (existingParticipant.Status == "Cancelled")
            {
                existingParticipant.Status = "Active";
                existingParticipant.JoinedAt = DateTime.UtcNow;
                existingParticipant.Notes = notes;
                existingParticipant.PaymentId = paymentId;
                existingParticipant.UpdatedAt = DateTime.UtcNow;
                _context.SaveChanges();
                return existingParticipant;
            }
            throw new InvalidOperationException("You are already enrolled in this training");
        }

        // Check if training has available spots
        var training = GetTraining(trainingId);
        if (training == null) return null;

        var activeParticipants = training.Participants.Count(p => p.Status == "Active");
        if (activeParticipants >= training.MaxParticipants)
        {
            return null; // Training is full
        }

        // Create new participant
        var participant = new TrainingParticipant
        {
            Id = Guid.NewGuid(),
            TrainingId = trainingId,
            UserId = userId,
            PaymentId = paymentId,
            JoinedAt = DateTime.UtcNow,
            Status = "Active",
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TrainingParticipants.Add(participant);
        _context.SaveChanges();

        return GetParticipant(trainingId, userId);
    }

    public bool LeaveTraining(Guid trainingId, Guid userId)
    {
        var participant = _context.TrainingParticipants
            .FirstOrDefault(tp => tp.TrainingId == trainingId && tp.UserId == userId);

        if (participant == null || participant.Status != "Active")
            return false;

        participant.Status = "Cancelled";
        participant.UpdatedAt = DateTime.UtcNow;
        
        _context.SaveChanges();
        return true;
    }

    public bool UpdateParticipantStatus(Guid trainingId, Guid userId, string status, string? notes)
    {
        var participant = _context.TrainingParticipants
            .FirstOrDefault(tp => tp.TrainingId == trainingId && tp.UserId == userId);

        if (participant == null)
            return false;

        participant.Status = status;
        participant.Notes = notes;
        participant.UpdatedAt = DateTime.UtcNow;
        
        _context.SaveChanges();
        return true;
    }

    public TrainingParticipant? GetParticipant(Guid trainingId, Guid userId)
    {
        return _context.TrainingParticipants
            .Include(tp => tp.User)
            .Include(tp => tp.Training)
            .FirstOrDefault(tp => tp.TrainingId == trainingId && tp.UserId == userId);
    }

    public List<TrainingParticipant> GetTrainingParticipants(Guid trainingId)
    {
        return _context.TrainingParticipants
            .Include(tp => tp.User)
            .Where(tp => tp.TrainingId == trainingId)
            .OrderBy(tp => tp.JoinedAt)
            .ToList();
    }

    public List<Training> GetUserTrainings(Guid userId)
    {
        return _context.TrainingParticipants
            .Include(tp => tp.Training)
                .ThenInclude(t => t.Sessions)
            .Include(tp => tp.Training)
                .ThenInclude(t => t.Facility)
            .Include(tp => tp.Training)
                .ThenInclude(t => t.TrainerProfile)
            .Include(tp => tp.Training)
                .ThenInclude(t => t.Participants)
                    .ThenInclude(p => p.User)
            .Where(tp => tp.UserId == userId && tp.Status == "Active")
            .Select(tp => tp.Training!)
            .OrderBy(t => t.CreatedAt)
            .ToList();
    }

    public List<Training> SearchTrainings(TrainingSearchDto searchDto)
    {
        var query = _context.Trainings
            .Include(t => t.Sessions)
            .Include(t => t.Facility)
            .Include(t => t.TrainerProfile)
            .Include(t => t.Participants)
                .ThenInclude(p => p.User)
            .AsQueryable();

        // Filter by facility city
        if (!string.IsNullOrWhiteSpace(searchDto.City))
        {
            query = query.Where(t => t.Facility!.City.ToLower().Contains(searchDto.City.ToLower()));
        }

        // Filter by date (check if any session is on the search date or in the future)
        if (!string.IsNullOrWhiteSpace(searchDto.Date))
        {
            DateTime searchDate;
            // Try multiple date formats to handle different input formats
            string[] formats = { "yyyy-MM-dd", "yyyy.MM.dd", "MM/dd/yyyy", "dd/MM/yyyy" };
            
            if (DateTime.TryParseExact(searchDto.Date, formats, null, System.Globalization.DateTimeStyles.None, out searchDate))
            {
                var searchDateUtc = DateTime.SpecifyKind(searchDate.Date, DateTimeKind.Utc);
                query = query.Where(t => t.Sessions.Any(s => s.Date.Date >= searchDateUtc));
            }
        }

        // Filter by price range
        if (searchDto.MinPrice.HasValue)
        {
            query = query.Where(t => t.Price >= searchDto.MinPrice.Value);
        }

        if (searchDto.MaxPrice.HasValue)
        {
            query = query.Where(t => t.Price <= searchDto.MaxPrice.Value);
        }

        // Filter by max participants
        if (searchDto.MaxParticipants.HasValue)
        {
            query = query.Where(t => t.MaxParticipants <= searchDto.MaxParticipants.Value);
        }

        // Execute query first without the complex specializations filter
        var results = query
            .OrderBy(t => t.CreatedAt)
            .ToList();

        // Filter by activity (match against training specialization, trainer specializations, or training title)
        // Done in memory because TrainerProfile.Specializations is stored as JSON and can't be translated by EF
        // Skip filter if "Wszystkie kategorie" (All categories) is passed
        if (!string.IsNullOrWhiteSpace(searchDto.Activity) &&
            !searchDto.Activity.Equals("Wszystkie kategorie", StringComparison.OrdinalIgnoreCase) &&
            !searchDto.Activity.Equals("All categories", StringComparison.OrdinalIgnoreCase))
        {
            var activityLower = searchDto.Activity.ToLower();
            results = results.Where(t =>
                t.Title.ToLower().Contains(activityLower) ||
                (t.Specialization != null && t.Specialization.ToLower().Contains(activityLower)) ||
                (t.TrainerProfile?.Specializations?.Any(s => s.ToLower().Contains(activityLower)) == true))
                .ToList();
        }

        return results;
    }

    public List<Training> GetUpcomingTrainings()
    {
        var currentUtc = DateTime.UtcNow;
        
        return _context.Trainings
            .Include(t => t.TrainerProfile)
            .Include(t => t.Facility)
            .Include(t => t.Sessions)
            .Include(t => t.Participants)
            .Where(t => t.Sessions.Any(s => s.Date.Date >= currentUtc.Date)) // Only trainings with future sessions
            .OrderBy(t => t.Sessions.Min(s => s.Date)) // Order by earliest session date
            .ToList();
    }

    public List<Training> GetUserUpcomingTrainings(Guid userId)
    {
        var currentUtc = DateTime.UtcNow;
        
        return _context.Trainings
            .Include(t => t.TrainerProfile)
            .Include(t => t.Facility)
            .Include(t => t.Sessions)
            .Include(t => t.Participants)
                .ThenInclude(p => p.User)
            .Where(t => t.Participants.Any(p => p.UserId == userId && p.Status == "Active")) // User is active participant
            .Where(t => t.Sessions.Any(s => s.Date.Date >= currentUtc.Date)) // Only trainings with future sessions
            .OrderBy(t => t.Sessions.Min(s => s.Date)) // Order by earliest session date
            .ToList();
    }
    
    public int GetTotalCount()
    {
        return _context.Trainings.Count();
    }
}