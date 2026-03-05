using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;

namespace PlaySpace.Repositories.Repositories;

public class TimeSlotRepository : ITimeSlotRepository
{
    private readonly PlaySpaceDbContext _context;

    public TimeSlotRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public List<TimeSlot> GetFacilityTimeSlots(Guid facilityId)
    {
        // Get stored facility slots (all-time + date-specific)
        var storedSlots = _context.TimeSlots
            .Where(ts => ts.FacilityId == facilityId)
            .OrderBy(x => x.Time)
            .ToList();
        
        return storedSlots;
    }

    public GetTimeSlotsResponseDto GetFacilityTimeSlotsStructured(Guid facilityId)
    {
        var result = new GetTimeSlotsResponseDto();
        
        // Get business schedule templates (base layer)
        var businessTemplates = GetAllBusinessTemplatesForFacility(facilityId);
        
        // Get facility overrides (all-time + date-specific)
        var facilitySlots = _context.TimeSlots
            .Where(ts => ts.FacilityId == facilityId)
            .ToList();
            
        var facilityAllTimeSlots = facilitySlots
            .Where(ts => ts.IsAllTime)
            .ToList();
            
        var facilityDateSpecificSlots = facilitySlots
            .Where(ts => !ts.IsAllTime && ts.Date.HasValue)
            .ToList();

        // Build AllTimeSlots by merging business templates with facility overrides
        foreach (ScheduleType scheduleType in Enum.GetValues<ScheduleType>())
        {
            var scheduleKey = scheduleType switch
            {
                ScheduleType.Saturday => "saturday",
                ScheduleType.Sunday => "sunday",
                _ => "weekdays"
            };
            
            result.AllTimeSlots[scheduleKey] = new List<TimeSlotItemDto>();
            
            // Get business templates for this schedule type
            var businessTemplatesForSchedule = businessTemplates
                .Where(bt => bt.ScheduleType == scheduleType)
                .ToList();
            
            // Get facility overrides for this schedule type
            var facilityOverridesForSchedule = facilityAllTimeSlots
                .Where(fs => fs.ScheduleType == scheduleType)
                .ToDictionary(fs => fs.Time);
            
            // Create merged slots (business template as base, facility override with boundary enforcement)
            foreach (var businessTemplate in businessTemplatesForSchedule)
            {
                TimeSlotItemDto slotItem;

                if (facilityOverridesForSchedule.ContainsKey(businessTemplate.Time))
                {
                    // Facility override exists
                    var facilityOverride = facilityOverridesForSchedule[businessTemplate.Time];

                    // BOUNDARY ENFORCEMENT: Facility can only be available if business template is available
                    slotItem = new TimeSlotItemDto
                    {
                        Id = facilityOverride.Time,
                        Time = facilityOverride.Time,
                        IsAvailable = facilityOverride.IsAvailable && businessTemplate.IsAvailable,
                        IsBooked = facilityOverride.IsBooked,
                        BookedBy = facilityOverride.BookedByUserId?.ToString()
                    };
                }
                else
                {
                    // Use business template
                    slotItem = new TimeSlotItemDto
                    {
                        Id = businessTemplate.Time,
                        Time = businessTemplate.Time,
                        IsAvailable = businessTemplate.IsAvailable,
                        IsBooked = false, // Business templates don't have booking info
                        BookedBy = null
                    };
                }

                result.AllTimeSlots[scheduleKey].Add(slotItem);
            }

            // DO NOT add facility overrides that don't have business templates
            // Facility cannot create availability where business template doesn't exist
            
            // Sort by time
            result.AllTimeSlots[scheduleKey].Sort((a, b) => string.Compare(a.Time, b.Time, StringComparison.Ordinal));
        }

        // Build DateSpecificSlots
        var dateGroups = facilityDateSpecificSlots.GroupBy(ds => ds.Date?.ToString("yyyy-MM-dd") ?? "");
        foreach (var group in dateGroups)
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                result.DateSpecificSlots[group.Key] = group
                    .Select(ds => new TimeSlotItemDto
                    {
                        Id = ds.Time,
                        Time = ds.Time,
                        IsAvailable = ds.IsAvailable,
                        IsBooked = ds.IsBooked,
                        BookedBy = ds.BookedByUserId?.ToString()
                    })
                    .OrderBy(s => s.Time)
                    .ToList();
            }
        }

        return result;
    }

    public List<TimeSlot> GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        // Determine schedule type based on date
        var scheduleType = utcDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => ScheduleType.Saturday,
            DayOfWeek.Sunday => ScheduleType.Sunday,
            _ => ScheduleType.Weekdays
        };

        // STEP 1: Check if business has date-specific availability for this date (highest priority - overrides everything)
        var businessDateSpecificSlots = GetBusinessDateSpecificSlotsForFacility(facilityId, utcDate);
        if (businessDateSpecificSlots.Any())
        {
            // Business date-specific slots override everything - return these as-is
            return businessDateSpecificSlots.Select(bds => new TimeSlot
            {
                Id = Guid.NewGuid(),
                FacilityId = facilityId,
                Time = bds.Time,
                IsAvailable = bds.IsAvailable,
                IsBooked = false, // Business templates don't track bookings
                BookedByUserId = null,
                Date = utcDate,
                IsAllTime = false,
                ScheduleType = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).OrderBy(x => x.Time).ToList();
        }

        // STEP 2: Get business schedule templates (base layer - defines boundary)
        var businessTemplates = GetBusinessTemplatesForFacility(facilityId, utcDate);

        // STEP 3: Get facility all-time overrides (second layer - can only restrict, not expand)
        var facilityAllTimeSlots = _context.TimeSlots
            .Where(ts => ts.FacilityId == facilityId && ts.IsAllTime && ts.ScheduleType == scheduleType)
            .ToList();

        // STEP 4: Get facility date-specific overrides (third layer - can only restrict, not expand)
        var facilityDateSpecificSlots = _context.TimeSlots
            .Where(ts => ts.FacilityId == facilityId && ts.Date == utcDate)
            .ToList();

        // Build merged slots with business boundary enforcement
        var result = new List<TimeSlot>();
        var facilityOverridesDict = facilityAllTimeSlots.ToDictionary(ts => ts.Time);
        var dateOverridesDict = facilityDateSpecificSlots.ToDictionary(ts => ts.Time);

        // Start with business templates and apply facility overrides (with boundary enforcement)
        foreach (var businessTemplate in businessTemplates)
        {
            TimeSlot effectiveSlot;

            if (dateOverridesDict.ContainsKey(businessTemplate.Time))
            {
                // Facility date-specific override exists
                var facilityDateOverride = dateOverridesDict[businessTemplate.Time];

                // BOUNDARY ENFORCEMENT: Facility can only be available if business template is available
                var isAvailable = facilityDateOverride.IsAvailable && businessTemplate.IsAvailable;

                effectiveSlot = new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    Time = businessTemplate.Time,
                    IsAvailable = isAvailable,
                    IsBooked = facilityDateOverride.IsBooked,
                    BookedByUserId = facilityDateOverride.BookedByUserId,
                    Date = utcDate,
                    IsAllTime = false,
                    ScheduleType = null,
                    CreatedAt = facilityDateOverride.CreatedAt,
                    UpdatedAt = facilityDateOverride.UpdatedAt
                };
            }
            else if (facilityOverridesDict.ContainsKey(businessTemplate.Time))
            {
                // Facility all-time override exists
                var facilityOverride = facilityOverridesDict[businessTemplate.Time];

                // BOUNDARY ENFORCEMENT: Facility can only be available if business template is available
                var isAvailable = facilityOverride.IsAvailable && businessTemplate.IsAvailable;

                effectiveSlot = new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    Time = businessTemplate.Time,
                    IsAvailable = isAvailable,
                    IsBooked = facilityOverride.IsBooked,
                    BookedByUserId = facilityOverride.BookedByUserId,
                    Date = utcDate,
                    IsAllTime = false,
                    ScheduleType = null,
                    CreatedAt = facilityOverride.CreatedAt,
                    UpdatedAt = facilityOverride.UpdatedAt
                };
            }
            else
            {
                // Use business template as base (no facility override)
                effectiveSlot = new TimeSlot
                {
                    Id = Guid.NewGuid(),
                    FacilityId = facilityId,
                    Time = businessTemplate.Time,
                    IsAvailable = businessTemplate.IsAvailable,
                    IsBooked = false,
                    BookedByUserId = null,
                    Date = utcDate,
                    IsAllTime = false,
                    ScheduleType = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            result.Add(effectiveSlot);
        }

        // DO NOT add facility overrides that don't have business templates
        // Facility cannot create availability where business template doesn't exist

        return result.OrderBy(x => x.Time).ToList();
    }

    public void UpdateTimeSlots(Guid facilityId, UpdateTimeSlotsDto updateDto)
    {
        // Get business templates for comparison
        var businessTemplates = GetAllBusinessTemplatesForFacility(facilityId);
        var businessTemplatesDict = businessTemplates
            .GroupBy(bt => bt.ScheduleType)
            .ToDictionary(
                g => g.Key, 
                g => g.ToDictionary(bt => bt.Time, bt => bt)
            );

        // Handle all-time template slots - only store if different from business template
        foreach (var scheduleEntry in updateDto.AllTimeSlots)
        {
            var scheduleTypeKey = scheduleEntry.Key.ToLower();
            var timeSlots = scheduleEntry.Value;

            // Convert string key to ScheduleType enum
            ScheduleType? scheduleType = scheduleTypeKey switch
            {
                "weekdays" => ScheduleType.Weekdays,
                "saturday" => ScheduleType.Saturday,
                "sunday" => ScheduleType.Sunday,
                _ => null
            };

            if (scheduleType == null) continue;

            // Get business templates for this schedule type
            var businessTemplatesForSchedule = businessTemplatesDict.ContainsKey(scheduleType.Value) 
                ? businessTemplatesDict[scheduleType.Value] 
                : new Dictionary<string, BusinessScheduleTemplate>();

            foreach (var timeSlotDto in timeSlots)
            {
                var existingTimeSlot = _context.TimeSlots
                    .FirstOrDefault(ts => ts.FacilityId == facilityId &&
                                        ts.Time == timeSlotDto.Time &&
                                        ts.IsAllTime == true &&
                                        ts.ScheduleType == scheduleType);

                // Check if this slot differs from business template
                bool shouldStore = ShouldStoreFacilityOverride(timeSlotDto, businessTemplatesForSchedule);
                
                if (shouldStore)
                {
                    if (existingTimeSlot != null)
                    {
                        existingTimeSlot.IsAvailable = timeSlotDto.IsAvailable;
                        existingTimeSlot.IsBooked = timeSlotDto.IsBooked;
                        
                        if (timeSlotDto.BookedBy != null && Guid.TryParse(timeSlotDto.BookedBy, out var bookedByGuid))
                        {
                            existingTimeSlot.BookedByUserId = bookedByGuid;
                        }
                        else
                        {
                            existingTimeSlot.BookedByUserId = null;
                        }
                        
                        existingTimeSlot.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        var newTimeSlot = new TimeSlot
                        {
                            Id = Guid.NewGuid(),
                            FacilityId = facilityId,
                            Time = timeSlotDto.Time,
                            IsAvailable = timeSlotDto.IsAvailable,
                            IsBooked = timeSlotDto.IsBooked,
                            Date = null,
                            IsAllTime = true,
                            ScheduleType = scheduleType,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        if (timeSlotDto.BookedBy != null && Guid.TryParse(timeSlotDto.BookedBy, out var bookedByGuid))
                        {
                            newTimeSlot.BookedByUserId = bookedByGuid;
                        }

                        _context.TimeSlots.Add(newTimeSlot);
                    }
                }
                else if (existingTimeSlot != null)
                {
                    // Remove facility override if it now matches business template
                    _context.TimeSlots.Remove(existingTimeSlot);
                }
            }
        }

        // Handle date-specific slots
        foreach (var dateEntry in updateDto.DateSpecificSlots)
        {
            var dateKey = dateEntry.Key;
            var timeSlots = dateEntry.Value;

            if (!DateTime.TryParse(dateKey, out var parsedDate)) continue;
            
            var date = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);

            foreach (var timeSlotDto in timeSlots)
            {
                var existingTimeSlot = _context.TimeSlots
                    .FirstOrDefault(ts => ts.FacilityId == facilityId &&
                                        ts.Time == timeSlotDto.Time &&
                                        ts.Date == date &&
                                        ts.IsAllTime == false);

                // Only store date-specific slots if different from effective template (business + facility override)
                bool shouldStore = ShouldStoreException(facilityId, timeSlotDto, date);
                
                if (shouldStore)
                {
                    if (existingTimeSlot != null)
                    {
                        existingTimeSlot.IsAvailable = timeSlotDto.IsAvailable;
                        existingTimeSlot.IsBooked = timeSlotDto.IsBooked;
                        
                        if (timeSlotDto.BookedBy != null && Guid.TryParse(timeSlotDto.BookedBy, out var bookedByGuid))
                        {
                            existingTimeSlot.BookedByUserId = bookedByGuid;
                        }
                        else
                        {
                            existingTimeSlot.BookedByUserId = null;
                        }
                        
                        existingTimeSlot.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        var newTimeSlot = new TimeSlot
                        {
                            Id = Guid.NewGuid(),
                            FacilityId = facilityId,
                            Time = timeSlotDto.Time,
                            IsAvailable = timeSlotDto.IsAvailable,
                            IsBooked = timeSlotDto.IsBooked,
                            Date = date,
                            IsAllTime = false,
                            ScheduleType = null, // Date-specific slots don't have schedule type
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        if (timeSlotDto.BookedBy != null && Guid.TryParse(timeSlotDto.BookedBy, out var bookedByGuid))
                        {
                            newTimeSlot.BookedByUserId = bookedByGuid;
                        }

                        _context.TimeSlots.Add(newTimeSlot);
                    }
                }
                else if (existingTimeSlot != null)
                {
                    // Remove exception if it now matches the effective template
                    _context.TimeSlots.Remove(existingTimeSlot);
                }
            }
        }

        _context.SaveChanges();
    }
    
    private bool ShouldStoreFacilityOverride(TimeSlotItemDto timeSlotDto, Dictionary<string, BusinessScheduleTemplate> businessTemplatesForSchedule)
    {
        // If no business template exists for this time, store the facility slot
        if (!businessTemplatesForSchedule.ContainsKey(timeSlotDto.Time))
        {
            return true;
        }
        
        var businessTemplate = businessTemplatesForSchedule[timeSlotDto.Time];
        
        // Store if facility slot differs from business template
        return timeSlotDto.IsAvailable != businessTemplate.IsAvailable ||
               timeSlotDto.IsBooked != false || // Business templates are never booked
               timeSlotDto.BookedBy != null; // Business templates have no bookings
    }

    private bool ShouldStoreException(Guid facilityId, TimeSlotItemDto timeSlotDto, DateTime? date)
    {
        // For date-specific slots: check against merged template (business + facility overrides)
        var effectiveTemplate = GetEffectiveTemplate(facilityId, timeSlotDto.Time, date);
        
        // Store if different from effective template
        return timeSlotDto.IsAvailable != effectiveTemplate.IsAvailable || 
               timeSlotDto.IsBooked != effectiveTemplate.IsBooked ||
               (timeSlotDto.BookedBy != null && timeSlotDto.BookedBy != effectiveTemplate.BookedByUserId?.ToString());
    }
    
    private TimeSlot GetEffectiveTemplate(Guid facilityId, string time, DateTime? date = null)
    {
        // Determine schedule type based on date (default to weekdays)
        ScheduleType scheduleType = ScheduleType.Weekdays;
        if (date.HasValue)
        {
            scheduleType = date.Value.DayOfWeek switch
            {
                DayOfWeek.Saturday => ScheduleType.Saturday,
                DayOfWeek.Sunday => ScheduleType.Sunday,
                _ => ScheduleType.Weekdays
            };
        }

        // Get business template first (defines the boundary)
        var facility = _context.Facilities.FirstOrDefault(f => f.Id == facilityId);
        BusinessScheduleTemplate? businessTemplate = null;

        if (facility != null)
        {
            var businessProfile = _context.BusinessProfiles
                .Include(bp => bp.ScheduleTemplates)
                .FirstOrDefault(bp => bp.UserId == facility.UserId);

            if (businessProfile != null)
            {
                businessTemplate = businessProfile.ScheduleTemplates
                    .FirstOrDefault(st => st.Time == time && st.ScheduleType == scheduleType);
            }
        }

        // If no business template exists, slot is unavailable
        if (businessTemplate == null)
        {
            return new TimeSlot
            {
                IsAvailable = false,
                IsBooked = false,
                BookedByUserId = null
            };
        }

        // Check facility all-time override for the specific schedule type
        var facilityOverride = _context.TimeSlots
            .FirstOrDefault(ts => ts.FacilityId == facilityId &&
                                ts.Time == time &&
                                ts.IsAllTime &&
                                ts.ScheduleType == scheduleType);

        if (facilityOverride != null)
        {
            // BOUNDARY ENFORCEMENT: Facility can only be available if business template is available
            return new TimeSlot
            {
                IsAvailable = facilityOverride.IsAvailable && businessTemplate.IsAvailable,
                IsBooked = facilityOverride.IsBooked,
                BookedByUserId = facilityOverride.BookedByUserId
            };
        }

        // Return business template as effective template
        return new TimeSlot
        {
            IsAvailable = businessTemplate.IsAvailable,
            IsBooked = false,
            BookedByUserId = null
        };
    }
    
    
    private List<BusinessScheduleTemplate> GetBusinessTemplatesForFacility(Guid facilityId, DateTime date)
    {
        var facility = _context.Facilities.FirstOrDefault(f => f.Id == facilityId);
        if (facility == null) return new List<BusinessScheduleTemplate>();
        
        var businessProfile = _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .FirstOrDefault(bp => bp.UserId == facility.UserId);
            
        if (businessProfile == null) return new List<BusinessScheduleTemplate>();
        
        // Determine schedule type based on date
        var scheduleType = date.DayOfWeek switch
        {
            DayOfWeek.Saturday => PlaySpace.Domain.Models.ScheduleType.Saturday,
            DayOfWeek.Sunday => PlaySpace.Domain.Models.ScheduleType.Sunday,
            _ => PlaySpace.Domain.Models.ScheduleType.Weekdays
        };
        
        return businessProfile.ScheduleTemplates
            .Where(st => st.ScheduleType == scheduleType)
            .Select(st => new BusinessScheduleTemplate
            {
                Time = st.Time,
                IsAvailable = st.IsAvailable
            })
            .ToList();
    }
    
    private List<BusinessScheduleTemplate> GetAllBusinessTemplatesForFacility(Guid facilityId)
    {
        var facility = _context.Facilities.FirstOrDefault(f => f.Id == facilityId);
        if (facility == null) return new List<BusinessScheduleTemplate>();

        var businessProfile = _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .FirstOrDefault(bp => bp.UserId == facility.UserId);

        if (businessProfile == null) return new List<BusinessScheduleTemplate>();

        return businessProfile.ScheduleTemplates.ToList();
    }

    private List<BusinessDateAvailability> GetBusinessDateSpecificSlotsForFacility(Guid facilityId, DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        var facility = _context.Facilities.FirstOrDefault(f => f.Id == facilityId);
        if (facility == null) return new List<BusinessDateAvailability>();

        var businessProfile = _context.BusinessProfiles
            .Include(bp => bp.DateAvailabilities.Where(da => da.Date == utcDate))
            .FirstOrDefault(bp => bp.UserId == facility.UserId);

        if (businessProfile == null) return new List<BusinessDateAvailability>();

        return businessProfile.DateAvailabilities.Where(da => da.Date == utcDate).ToList();
    }

    public TimeSlot? GetTimeSlot(Guid facilityId, string time, DateTime? date)
    {
        if (date == null)
        {
            return _context.TimeSlots
                .FirstOrDefault(ts => ts.FacilityId == facilityId &&
                                    ts.Time == time &&
                                    ts.IsAllTime);
        }
        
        var utcDate = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
        
        // First try to get date-specific slot
        var dateSpecificSlot = _context.TimeSlots
            .FirstOrDefault(ts => ts.FacilityId == facilityId &&
                                ts.Time == time &&
                                ts.Date == utcDate);
        
        if (dateSpecificSlot != null)
        {
            return dateSpecificSlot;
        }
        
        // Fall back to all-time template
        var allTimeSlot = _context.TimeSlots
            .FirstOrDefault(ts => ts.FacilityId == facilityId &&
                                ts.Time == time &&
                                ts.IsAllTime);
        
        if (allTimeSlot != null)
        {
            // Return virtual slot based on all-time template
            return new TimeSlot
            {
                Id = allTimeSlot.Id,
                FacilityId = allTimeSlot.FacilityId,
                Time = allTimeSlot.Time,
                IsAvailable = allTimeSlot.IsAvailable,
                IsBooked = allTimeSlot.IsBooked,
                BookedByUserId = allTimeSlot.BookedByUserId,
                Date = utcDate,
                IsAllTime = false,
                CreatedAt = allTimeSlot.CreatedAt,
                UpdatedAt = allTimeSlot.UpdatedAt
            };
        }
        
        return null;
    }
}