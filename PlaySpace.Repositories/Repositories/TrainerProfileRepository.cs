using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;

namespace PlaySpace.Repositories.Repositories;

public class TrainerProfileRepository : ITrainerProfileRepository
{
    private readonly PlaySpaceDbContext _context;

    public TrainerProfileRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public TrainerProfile? GetTrainerProfile(Guid userId)
    {
        return _context.TrainerProfiles
            .Include(tp => tp.ScheduleTemplates)
            .Include(tp => tp.DateAvailabilities)
            .FirstOrDefault(tp => tp.UserId == userId);
    }

    public TrainerProfile? GetTrainerProfileById(Guid trainerProfileId)
    {
        return _context.TrainerProfiles
            .Include(tp => tp.ScheduleTemplates)
            .Include(tp => tp.DateAvailabilities)
            .FirstOrDefault(tp => tp.Id == trainerProfileId);
    }

    public TrainerProfile CreateTrainerProfile(CreateTrainerProfileDto profileDto, Guid userId)
    {
        var trainerProfile = new TrainerProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TrainerType = profileDto.TrainerType,
            Nip = profileDto.Nip,
            CompanyName = profileDto.CompanyName,
            DisplayName = profileDto.DisplayName,
            Address = profileDto.Address,
            City = profileDto.City,
            PostalCode = profileDto.PostalCode,
            Specializations = profileDto.Specializations,
            HourlyRate = profileDto.HourlyRate,
            VatRate = profileDto.VatRate,
            GrossHourlyRate = profileDto.GrossHourlyRate,
            Description = profileDto.Description,
            Certifications = profileDto.Certifications,
            Languages = profileDto.Languages,
            ExperienceYears = profileDto.ExperienceYears,
            Rating = profileDto.Rating,
            TotalSessions = profileDto.TotalSessions,
            AssociatedBusinessIds = profileDto.AssociatedBusinessIds,
            Email = profileDto.Email,
            PhoneNumber = profileDto.PhoneNumber,
            PhoneCountry = profileDto.PhoneCountry,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TrainerProfiles.Add(trainerProfile);
        _context.SaveChanges();

        // Create schedule templates if availability is provided
        if (profileDto.Availability != null)
        {
            UpdateTrainerScheduleTemplates(trainerProfile.Id, profileDto.Availability);
        }

        return trainerProfile;
    }

    public TrainerProfile? UpdateTrainerProfile(Guid userId, UpdateTrainerProfileDto profileDto)
    {
        var trainerProfile = GetTrainerProfile(userId);
        if (trainerProfile == null)
            return null;

        trainerProfile.Nip = profileDto.Nip;
        trainerProfile.CompanyName = profileDto.CompanyName;
        trainerProfile.DisplayName = profileDto.DisplayName;
        trainerProfile.Address = profileDto.Address;
        trainerProfile.City = profileDto.City;
        trainerProfile.PostalCode = profileDto.PostalCode;
        trainerProfile.Specializations = profileDto.Specializations;
        trainerProfile.HourlyRate = profileDto.HourlyRate;
        trainerProfile.VatRate = profileDto.VatRate;
        trainerProfile.GrossHourlyRate = profileDto.GrossHourlyRate;
        trainerProfile.Description = profileDto.Description;
        trainerProfile.Certifications = profileDto.Certifications;
        trainerProfile.Languages = profileDto.Languages;
        trainerProfile.ExperienceYears = profileDto.ExperienceYears;
        trainerProfile.Rating = profileDto.Rating;
        trainerProfile.TotalSessions = profileDto.TotalSessions;
        trainerProfile.AssociatedBusinessIds = profileDto.AssociatedBusinessIds;
        
        // Update TPay registration fields if provided
        if (!string.IsNullOrEmpty(profileDto.Email))
            trainerProfile.Email = profileDto.Email;
        if (!string.IsNullOrEmpty(profileDto.PhoneNumber))
            trainerProfile.PhoneNumber = profileDto.PhoneNumber;
        if (!string.IsNullOrEmpty(profileDto.PhoneCountry))
            trainerProfile.PhoneCountry = profileDto.PhoneCountry;
        if (!string.IsNullOrEmpty(profileDto.Regon))
            trainerProfile.Regon = profileDto.Regon;
        if (!string.IsNullOrEmpty(profileDto.Krs))
            trainerProfile.Krs = profileDto.Krs;
        if (profileDto.LegalForm.HasValue)
            trainerProfile.LegalForm = profileDto.LegalForm;
        if (profileDto.CategoryId.HasValue)
            trainerProfile.CategoryId = profileDto.CategoryId;
        if (!string.IsNullOrEmpty(profileDto.Mcc))
            trainerProfile.Mcc = profileDto.Mcc;
        if (!string.IsNullOrEmpty(profileDto.Website))
            trainerProfile.Website = profileDto.Website;
        if (!string.IsNullOrEmpty(profileDto.WebsiteDescription))
            trainerProfile.WebsiteDescription = profileDto.WebsiteDescription;
        if (!string.IsNullOrEmpty(profileDto.ContactPersonName))
            trainerProfile.ContactPersonName = profileDto.ContactPersonName;
        if (!string.IsNullOrEmpty(profileDto.ContactPersonSurname))
            trainerProfile.ContactPersonSurname = profileDto.ContactPersonSurname;
            
        trainerProfile.UpdatedAt = DateTime.UtcNow;

        // Update schedule templates if availability is provided
        if (profileDto.Availability != null)
        {
            UpdateTrainerScheduleTemplates(trainerProfile.Id, profileDto.Availability);
        }

        _context.SaveChanges();
        
        // Return the updated trainer profile with fresh schedule templates
        return GetTrainerProfile(userId);
    }

    public void UpdateTrainerProfile(TrainerProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();
    }

    public bool DeleteTrainerProfile(Guid userId)
    {
        var trainerProfile = GetTrainerProfile(userId);
        if (trainerProfile == null)
            return false;

        // Delete associated schedule templates
        var scheduleTemplates = _context.TrainerScheduleTemplates
            .Where(st => st.TrainerProfileId == trainerProfile.Id)
            .ToList();
        _context.TrainerScheduleTemplates.RemoveRange(scheduleTemplates);

        _context.TrainerProfiles.Remove(trainerProfile);
        _context.SaveChanges();
        return true;
    }

    public bool TrainerProfileExists(Guid userId)
    {
        return _context.TrainerProfiles.Any(tp => tp.UserId == userId);
    }

    public bool NipExists(string nip, Guid? excludeUserId = null)
    {
        var query = _context.TrainerProfiles.Where(tp => tp.Nip == nip);
        if (excludeUserId.HasValue)
        {
            query = query.Where(tp => tp.UserId != excludeUserId);
        }
        return query.Any();
    }

    public async Task UpdateAvatarAsync(Guid userId, string avatarUrl)
    {
        var trainerProfile = await _context.TrainerProfiles
            .FirstOrDefaultAsync(tp => tp.UserId == userId);
        
        if (trainerProfile != null)
        {
            trainerProfile.AvatarUrl = avatarUrl;
            trainerProfile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public void UpdateTrainerScheduleTemplates(Guid trainerProfileId, TrainerAvailabilityDto availability)
    {
        // Remove existing schedule templates
        var existingTemplates = _context.TrainerScheduleTemplates
            .Where(st => st.TrainerProfileId == trainerProfileId)
            .ToList();
        _context.TrainerScheduleTemplates.RemoveRange(existingTemplates);

        // Remove existing date availabilities
        var existingDateAvailabilities = _context.TrainerDateAvailabilities
            .Where(da => da.TrainerProfileId == trainerProfileId)
            .ToList();
        _context.TrainerDateAvailabilities.RemoveRange(existingDateAvailabilities);

        var scheduleTemplates = new List<TrainerScheduleTemplate>();
        var dateAvailabilities = new List<TrainerDateAvailability>();

        // Add weekdays
        foreach (var slot in availability.Weekdays)
        {
            scheduleTemplates.Add(new TrainerScheduleTemplate
            {
                Id = Guid.NewGuid(),
                TrainerProfileId = trainerProfileId,
                ScheduleType = ScheduleType.Weekdays,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                AssociatedBusinessId = slot.AssociatedBusinessId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Add Saturday
        foreach (var slot in availability.Saturday)
        {
            scheduleTemplates.Add(new TrainerScheduleTemplate
            {
                Id = Guid.NewGuid(),
                TrainerProfileId = trainerProfileId,
                ScheduleType = ScheduleType.Saturday,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                AssociatedBusinessId = slot.AssociatedBusinessId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Add Sunday
        foreach (var slot in availability.Sunday)
        {
            scheduleTemplates.Add(new TrainerScheduleTemplate
            {
                Id = Guid.NewGuid(),
                TrainerProfileId = trainerProfileId,
                ScheduleType = ScheduleType.Sunday,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                AssociatedBusinessId = slot.AssociatedBusinessId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Add specific dates
        foreach (var dateEntry in availability.SpecificDates)
        {
            if (DateTime.TryParse(dateEntry.Key, out var specificDate))
            {
                foreach (var slot in dateEntry.Value)
                {
                    dateAvailabilities.Add(new TrainerDateAvailability
                    {
                        Id = Guid.NewGuid(),
                        TrainerProfileId = trainerProfileId,
                        Date = DateTime.SpecifyKind(specificDate.Date, DateTimeKind.Utc),
                        Time = slot.Time,
                        IsAvailable = slot.IsAvailable,
                        AssociatedBusinessId = slot.AssociatedBusinessId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        _context.TrainerScheduleTemplates.AddRange(scheduleTemplates);
        _context.TrainerDateAvailabilities.AddRange(dateAvailabilities);
        _context.SaveChanges();
    }

    public bool AssociateBusinessProfile(Guid trainerUserId, string businessProfileId)
    {
        var trainerProfile = GetTrainerProfile(trainerUserId);
        if (trainerProfile == null)
            return false;

        // Check if already associated
        if (trainerProfile.AssociatedBusinessIds.Contains(businessProfileId))
            return true; // Already associated

        // Add the business profile ID
        trainerProfile.AssociatedBusinessIds.Add(businessProfileId);
        trainerProfile.UpdatedAt = DateTime.UtcNow;

        // Explicitly mark the AssociatedBusinessIds property as modified
        _context.Entry(trainerProfile).Property(e => e.AssociatedBusinessIds).IsModified = true;
        _context.SaveChanges();
        return true;
    }

    public bool DisassociateBusinessProfile(Guid trainerUserId, string businessProfileId)
    {
        var trainerProfile = GetTrainerProfile(trainerUserId);
        if (trainerProfile == null)
            return false;

        // Remove the business profile ID
        var removed = trainerProfile.AssociatedBusinessIds.Remove(businessProfileId);
        if (removed)
        {
            trainerProfile.UpdatedAt = DateTime.UtcNow;
            // Explicitly mark the AssociatedBusinessIds property as modified
            _context.Entry(trainerProfile).Property(e => e.AssociatedBusinessIds).IsModified = true;
            _context.SaveChanges();
        }

        return removed;
    }

    public BusinessAssociationResultDto AssociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds)
    {
        var result = new BusinessAssociationResultDto();
        
        // Get trainer profile with proper EF tracking
        var trainerProfile = _context.TrainerProfiles
            .FirstOrDefault(tp => tp.UserId == trainerUserId);
        
        if (trainerProfile == null)
        {
            // All fail if trainer profile doesn't exist
            foreach (var id in businessProfileIds)
            {
                result.Failed.Add(new BusinessAssociationErrorDto
                {
                    BusinessProfileId = id,
                    Error = "Trainer profile not found"
                });
            }
            return result;
        }

        bool hasChanges = false;
        foreach (var businessProfileId in businessProfileIds)
        {
            try
            {
                // Check if already associated
                if (trainerProfile.AssociatedBusinessIds.Contains(businessProfileId))
                {
                    result.Successful.Add(businessProfileId); // Already associated = success
                    continue;
                }

                // Validate business profile exists
                if (Guid.TryParse(businessProfileId, out var businessGuid))
                {
                    var businessProfile = _context.BusinessProfiles.FirstOrDefault(bp => bp.Id == businessGuid);
                    if (businessProfile == null)
                    {
                        result.Failed.Add(new BusinessAssociationErrorDto
                        {
                            BusinessProfileId = businessProfileId,
                            Error = "Business profile not found"
                        });
                        continue;
                    }
                }
                else
                {
                    result.Failed.Add(new BusinessAssociationErrorDto
                    {
                        BusinessProfileId = businessProfileId,
                        Error = "Invalid business profile ID format"
                    });
                    continue;
                }

                // Add association
                trainerProfile.AssociatedBusinessIds.Add(businessProfileId);
                result.Successful.Add(businessProfileId);
                hasChanges = true;
            }
            catch (Exception ex)
            {
                result.Failed.Add(new BusinessAssociationErrorDto
                {
                    BusinessProfileId = businessProfileId,
                    Error = ex.Message
                });
            }
        }

        if (hasChanges)
        {
            trainerProfile.UpdatedAt = DateTime.UtcNow;
            // Explicitly mark the AssociatedBusinessIds property as modified
            _context.Entry(trainerProfile).Property(e => e.AssociatedBusinessIds).IsModified = true;
            _context.SaveChanges();
        }

        return result;
    }

    public BusinessAssociationResultDto DisassociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds)
    {
        var result = new BusinessAssociationResultDto();
        
        // Get trainer profile with proper EF tracking
        var trainerProfile = _context.TrainerProfiles
            .FirstOrDefault(tp => tp.UserId == trainerUserId);
        
        if (trainerProfile == null)
        {
            // All fail if trainer profile doesn't exist
            foreach (var id in businessProfileIds)
            {
                result.Failed.Add(new BusinessAssociationErrorDto
                {
                    BusinessProfileId = id,
                    Error = "Trainer profile not found"
                });
            }
            return result;
        }

        bool hasChanges = false;
        foreach (var businessProfileId in businessProfileIds)
        {
            try
            {
                // Try to remove the association
                var removed = trainerProfile.AssociatedBusinessIds.Remove(businessProfileId);
                if (removed)
                {
                    result.Successful.Add(businessProfileId);
                    hasChanges = true;
                }
                else
                {
                    result.Failed.Add(new BusinessAssociationErrorDto
                    {
                        BusinessProfileId = businessProfileId,
                        Error = "Business profile was not associated"
                    });
                }
            }
            catch (Exception ex)
            {
                result.Failed.Add(new BusinessAssociationErrorDto
                {
                    BusinessProfileId = businessProfileId,
                    Error = ex.Message
                });
            }
        }

        if (hasChanges)
        {
            trainerProfile.UpdatedAt = DateTime.UtcNow;
            // Explicitly mark the AssociatedBusinessIds property as modified
            _context.Entry(trainerProfile).Property(e => e.AssociatedBusinessIds).IsModified = true;
            _context.SaveChanges();
        }

        return result;
    }

    public List<string> GetAssociatedBusinessIds(Guid trainerUserId)
    {
        var trainerProfile = GetTrainerProfile(trainerUserId);
        return trainerProfile?.AssociatedBusinessIds ?? new List<string>();
    }

    public List<(TrainerProfile trainer, List<string> availableSlots)> FindAvailableTrainers(DateTime date, List<string> timeSlots)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var result = new List<(TrainerProfile trainer, List<string> availableSlots)>();
        
        // Determine schedule type based on date
        var scheduleType = utcDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => ScheduleType.Saturday,
            DayOfWeek.Sunday => ScheduleType.Sunday,
            _ => ScheduleType.Weekdays
        };

        // Get all trainer profiles with their schedule templates and date availabilities
        var trainers = _context.TrainerProfiles
            .Include(tp => tp.ScheduleTemplates.Where(st => st.ScheduleType == scheduleType))
            .Include(tp => tp.DateAvailabilities.Where(da => da.Date == utcDate))
            .ToList();

        foreach (var trainer in trainers)
        {
            var availableSlots = new List<string>();
            
            // Get trainer's existing reservations for this date
            var trainerReservations = _context.Reservations
                .Where(r => r.TrainerProfileId == trainer.Id && 
                           r.Date == utcDate && 
                           r.Status == "Active")
                .ToList();

            var bookedTimeSlots = trainerReservations
                .SelectMany(r => r.TimeSlots)
                .ToHashSet();
            
            foreach (var requestedTimeSlot in timeSlots)
            {
                bool isAvailable = false;

                // First, check if there's a specific date availability for this date and time
                var dateAvailability = trainer.DateAvailabilities
                    .FirstOrDefault(da => da.Time == requestedTimeSlot);

                if (dateAvailability != null)
                {
                    // Use specific date availability (overrides weekly template)
                    isAvailable = dateAvailability.IsAvailable;
                }
                else
                {
                    // Fall back to weekly schedule template
                    var scheduleTemplate = trainer.ScheduleTemplates
                        .FirstOrDefault(st => st.Time == requestedTimeSlot && st.IsAvailable);
                    
                    isAvailable = scheduleTemplate != null;
                }

                // Check if the slot is available and not already booked
                if (isAvailable && !bookedTimeSlots.Contains(requestedTimeSlot))
                {
                    availableSlots.Add(requestedTimeSlot);
                }
            }

            // Only include trainers who are available for ALL requested time slots
            if (availableSlots.Count == timeSlots.Count)
            {
                result.Add((trainer, availableSlots));
            }
        }

        return result.OrderByDescending(x => x.availableSlots.Count)
                    .ThenByDescending(x => x.trainer.Rating)
                    .ToList();
    }

    public TrainerDateTimeSlotsDto? GetMyTimeSlotsForDate(Guid userId, DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Get trainer profile with both schedule templates and date availabilities by userId
        var trainer = _context.TrainerProfiles
            .Include(tp => tp.ScheduleTemplates)
            .Include(tp => tp.DateAvailabilities.Where(da => da.Date == utcDate))
            .FirstOrDefault(tp => tp.UserId == userId);

        if (trainer == null)
            return null;

        // Get confirmed associations with their colors and business names
        var associations = _context.TrainerBusinessAssociations
            .Include(tba => tba.BusinessProfile)
            .Where(tba => tba.TrainerProfileId == trainer.Id && tba.Status == AssociationStatus.Confirmed)
            .ToDictionary(tba => tba.BusinessProfileId, tba => new { tba.Color, BusinessName = tba.BusinessProfile?.DisplayName ?? tba.BusinessProfile?.CompanyName });

        // Get reservations for this trainer and date
        var trainerReservations = _context.Reservations
            .Where(r => r.TrainerProfileId == trainer.Id &&
                       r.Date == utcDate &&
                       r.Status == "Active")
            .ToList();

        // Extract all booked time slots from reservations
        var bookedTimeSlots = trainerReservations
            .SelectMany(r => r.TimeSlots)
            .ToHashSet(); // Use HashSet for faster lookups

        // Check if there are date-specific availabilities for this date
        var dateAvailabilities = trainer.DateAvailabilities.Where(da => da.Date == utcDate).ToList();

        if (dateAvailabilities.Any())
        {
            // Return date-specific time slots with business info
            var timeSlots = dateAvailabilities.Select(da => {
                var businessInfo = da.AssociatedBusinessId.HasValue && associations.ContainsKey(da.AssociatedBusinessId.Value)
                    ? associations[da.AssociatedBusinessId.Value]
                    : null;

                return new TimeSlotItemDto
                {
                    Id = da.Time,
                    Time = da.Time,
                    IsAvailable = da.IsAvailable,
                    IsBooked = bookedTimeSlots.Contains(da.Time),
                    BookedBy = null,
                    AssociatedBusinessId = da.AssociatedBusinessId,
                    AssociatedBusinessName = businessInfo?.BusinessName,
                    Color = businessInfo?.Color
                };
            }).OrderBy(ts => ts.Time).ToList();

            return new TrainerDateTimeSlotsDto
            {
                Date = utcDate,
                TimeSlots = timeSlots,
                IsFromTemplate = false,
                TemplateType = null
            };
        }
        else
        {
            // Fall back to weekly schedule templates
            var scheduleType = utcDate.DayOfWeek switch
            {
                DayOfWeek.Saturday => ScheduleType.Saturday,
                DayOfWeek.Sunday => ScheduleType.Sunday,
                _ => ScheduleType.Weekdays
            };

            var templateSlots = trainer.ScheduleTemplates
                .Where(st => st.ScheduleType == scheduleType)
                .ToList();

            var timeSlots = templateSlots.Select(st => {
                var businessInfo = st.AssociatedBusinessId.HasValue && associations.ContainsKey(st.AssociatedBusinessId.Value)
                    ? associations[st.AssociatedBusinessId.Value]
                    : null;

                return new TimeSlotItemDto
                {
                    Id = st.Time,
                    Time = st.Time,
                    IsAvailable = st.IsAvailable,
                    IsBooked = bookedTimeSlots.Contains(st.Time),
                    BookedBy = null,
                    AssociatedBusinessId = st.AssociatedBusinessId,
                    AssociatedBusinessName = businessInfo?.BusinessName,
                    Color = businessInfo?.Color
                };
            }).OrderBy(ts => ts.Time).ToList();

            return new TrainerDateTimeSlotsDto
            {
                Date = utcDate,
                TimeSlots = timeSlots,
                IsFromTemplate = true,
                TemplateType = scheduleType.ToString().ToLower()
            };
        }
    }

    public List<(TrainerProfile trainer, List<string> availableSlots)> FindAvailableTrainersForBusiness(Guid businessProfileId, DateTime date, List<string> timeSlots)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var result = new List<(TrainerProfile trainer, List<string> availableSlots)>();

        // Determine schedule type based on date
        var scheduleType = utcDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => ScheduleType.Saturday,
            DayOfWeek.Sunday => ScheduleType.Sunday,
            _ => ScheduleType.Weekdays
        };

        // Get all trainer profiles that have confirmed associations with this business
        // and their schedule templates/date availabilities assigned to this business
        var trainers = _context.TrainerProfiles
            .Include(tp => tp.ScheduleTemplates.Where(st => st.ScheduleType == scheduleType && st.AssociatedBusinessId == businessProfileId))
            .Include(tp => tp.DateAvailabilities.Where(da => da.Date == utcDate && da.AssociatedBusinessId == businessProfileId))
            .Where(tp => _context.TrainerBusinessAssociations
                .Any(tba => tba.TrainerProfileId == tp.Id &&
                           tba.BusinessProfileId == businessProfileId &&
                           tba.Status == AssociationStatus.Confirmed))
            .ToList();

        foreach (var trainer in trainers)
        {
            var availableSlots = new List<string>();

            // Get trainer's existing reservations for this date at this business
            var trainerReservations = _context.Reservations
                .Where(r => r.TrainerProfileId == trainer.Id &&
                           r.Date == utcDate &&
                           r.Status == "Active")
                .ToList();

            var bookedTimeSlots = trainerReservations
                .SelectMany(r => r.TimeSlots)
                .ToHashSet();

            foreach (var requestedTimeSlot in timeSlots)
            {
                bool isAvailable = false;

                // First, check date-specific availability for this business
                var dateAvailability = trainer.DateAvailabilities
                    .FirstOrDefault(da => da.Time == requestedTimeSlot);

                if (dateAvailability != null)
                {
                    isAvailable = dateAvailability.IsAvailable;
                }
                else
                {
                    // Fall back to schedule template for this business
                    var scheduleTemplate = trainer.ScheduleTemplates
                        .FirstOrDefault(st => st.Time == requestedTimeSlot && st.IsAvailable);

                    isAvailable = scheduleTemplate != null;
                }

                if (isAvailable && !bookedTimeSlots.Contains(requestedTimeSlot))
                {
                    availableSlots.Add(requestedTimeSlot);
                }
            }

            // Only include trainers who are available for ALL requested time slots
            if (availableSlots.Count == timeSlots.Count)
            {
                result.Add((trainer, availableSlots));
            }
        }

        return result.OrderByDescending(x => x.availableSlots.Count)
                    .ThenByDescending(x => x.trainer.Rating)
                    .ToList();
    }

    public async Task UpdateTrainerScheduleTemplatesWithBusinessAsync(Guid trainerProfileId, ScheduleType scheduleType, List<SetTrainerTimeSlotDto> timeSlots)
    {
        // Get existing templates for this schedule type
        var existingTemplates = await _context.Set<TrainerScheduleTemplate>()
            .Where(st => st.TrainerProfileId == trainerProfileId && st.ScheduleType == scheduleType)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var slotDto in timeSlots)
        {
            var existingTemplate = existingTemplates.FirstOrDefault(st => st.Time == slotDto.Time);

            if (existingTemplate != null)
            {
                // Update existing template
                existingTemplate.IsAvailable = slotDto.IsAvailable;
                existingTemplate.AssociatedBusinessId = slotDto.AssociatedBusinessId;
                existingTemplate.UpdatedAt = now;
            }
            else
            {
                // Create new template
                var newTemplate = new TrainerScheduleTemplate
                {
                    Id = Guid.NewGuid(),
                    TrainerProfileId = trainerProfileId,
                    ScheduleType = scheduleType,
                    Time = slotDto.Time,
                    IsAvailable = slotDto.IsAvailable,
                    AssociatedBusinessId = slotDto.AssociatedBusinessId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.Set<TrainerScheduleTemplate>().Add(newTemplate);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task UpdateTrainerDateAvailabilityWithBusinessAsync(Guid trainerProfileId, DateTime date, List<SetTrainerTimeSlotDto> timeSlots)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Get existing date availabilities for this date
        var existingAvailabilities = await _context.Set<TrainerDateAvailability>()
            .Where(da => da.TrainerProfileId == trainerProfileId && da.Date == utcDate)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var slotDto in timeSlots)
        {
            var existingAvailability = existingAvailabilities.FirstOrDefault(da => da.Time == slotDto.Time);

            if (existingAvailability != null)
            {
                // Update existing availability
                existingAvailability.IsAvailable = slotDto.IsAvailable;
                existingAvailability.AssociatedBusinessId = slotDto.AssociatedBusinessId;
                existingAvailability.UpdatedAt = now;
            }
            else
            {
                // Create new availability
                var newAvailability = new TrainerDateAvailability
                {
                    Id = Guid.NewGuid(),
                    TrainerProfileId = trainerProfileId,
                    Date = utcDate,
                    Time = slotDto.Time,
                    IsAvailable = slotDto.IsAvailable,
                    AssociatedBusinessId = slotDto.AssociatedBusinessId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.Set<TrainerDateAvailability>().Add(newAvailability);
            }
        }

        await _context.SaveChangesAsync();
    }
}