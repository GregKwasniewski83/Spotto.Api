namespace PlaySpace.Repositories.Repositories
{
    using PlaySpace.Domain.Models;
    using PlaySpace.Domain.DTOs;
    using PlaySpace.Repositories.Interfaces;
    using PlaySpace.Repositories.Data;
    using System.Collections.Generic;
    using Microsoft.EntityFrameworkCore;

    public class FacilityRepository : IFacilityRepository
    {
        private readonly PlaySpaceDbContext _context;

        public FacilityRepository(PlaySpaceDbContext context)
        {
            _context = context;
        }

        public List<Facility> GetFacilities(SearchFiltersDto filters)
        {
            var query = _context.Facilities
                .Include(f => f.ScheduleTemplates)
                .AsQueryable();
            
            // Filter by type (e.g., "tennis_court", "shooting_range")
            if (!string.IsNullOrEmpty(filters.Type))
            {
                query = query.Where(f => f.Type.ToLower().Contains(filters.Type.ToLower()));
            }
            
            // Filter by city
            if (!string.IsNullOrEmpty(filters.City))
            {
                query = query.Where(f => f.City.ToLower().Contains(filters.City.ToLower()));
            }
            
            // Filter by name
            if (!string.IsNullOrEmpty(filters.Name))
            {
                query = query.Where(f => f.Name.ToLower().Contains(filters.Name.ToLower()));
            }
            
            // Filter by country
            if (!string.IsNullOrEmpty(filters.Country))
            {
                query = query.Where(f => f.Country == filters.Country);
            }
            
            // Filter by state
            if (!string.IsNullOrEmpty(filters.State))
            {
                query = query.Where(f => f.State != null && f.State.ToLower().Contains(filters.State.ToLower()));
            }
            
            // Filter by price range
            if (filters.MinPrice.HasValue)
            {
                query = query.Where(f => f.PricePerHour >= filters.MinPrice.Value);
            }
            
            if (filters.MaxPrice.HasValue)
            {
                query = query.Where(f => f.PricePerHour <= filters.MaxPrice.Value);
            }
            
            // Filter by minimum capacity
            if (filters.MinCapacity.HasValue)
            {
                query = query.Where(f => f.Capacity >= filters.MinCapacity.Value);
            }
            
            return query.OrderBy(f => f.Name).ToList();
        }

        public List<Facility> SearchFacilities(SearchFiltersDto filters)
        {
            // This method is identical to GetFacilities but could be enhanced
            // with additional search logic like distance-based search, etc.
            return GetFacilities(filters);
        }

        public Facility CreateFacility(CreateFacilityDto facilityDto, Guid userId)
        {
            var facility = new Facility
            {
                Id = Guid.NewGuid(),
                Name = facilityDto.Name,
                Type = facilityDto.Type,
                Description = facilityDto.Description,
                Capacity = facilityDto.Capacity,
                MaxUsers = facilityDto.MaxUsers,
                PricePerUser = facilityDto.PricePerUser,
                PricePerHour = facilityDto.PricePerHour,
                GrossPricePerHour = facilityDto.GrossPricePerHour,
                VatRate = facilityDto.VatRate,
                MinBookingSlots = facilityDto.MinBookingSlots,
                UserId = userId,
                BusinessProfileId = facilityDto.BusinessProfileId,
                Street = facilityDto.Street,
                City = facilityDto.City,
                State = facilityDto.State,
                PostalCode = facilityDto.PostalCode,
                Country = facilityDto.Country,
                AddressLine2 = facilityDto.AddressLine2,
                Latitude = facilityDto.Latitude,
                Longitude = facilityDto.Longitude,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Facilities.Add(facility);
            _context.SaveChanges();

            // Create schedule templates if availability is provided
            if (facilityDto.Availability != null)
            {
                UpdateFacilityScheduleTemplates(facility.Id, facilityDto.Availability);
            }

            return facility;
        }

        public Facility? GetFacility(Guid id)
        {
            return _context.Facilities
                .Include(f => f.ScheduleTemplates)
                .FirstOrDefault(f => f.Id == id);
        }

        public List<Facility> GetUserFacilities(Guid userId)
        {
            return _context.Facilities
                .Include(f => f.ScheduleTemplates)
                .Where(f => f.UserId == userId)
                .OrderBy(f => f.Name)
                .ToList();
        }

        public bool DeleteFacility(Guid id)
        {
            var facility = _context.Facilities.FirstOrDefault(f => f.Id == id);
            if (facility == null)
            {
                return false;
            }

            _context.Facilities.Remove(facility);
            _context.SaveChanges();
            return true;
        }

        public Facility? UpdateFacility(Guid id, UpdateFacilityDto facilityDto)
        {
            var facility = _context.Facilities.FirstOrDefault(f => f.Id == id);
            if (facility == null)
            {
                return null;
            }

            facility.Name = facilityDto.Name;
            facility.Type = facilityDto.Type;
            facility.Description = facilityDto.Description;
            facility.Capacity = facilityDto.Capacity;
            facility.MaxUsers = facilityDto.MaxUsers;
            facility.PricePerUser = facilityDto.PricePerUser;
            facility.PricePerHour = facilityDto.PricePerHour;
            facility.GrossPricePerHour = facilityDto.GrossPricePerHour;
            facility.VatRate = facilityDto.VatRate;
            facility.MinBookingSlots = facilityDto.MinBookingSlots;
            facility.BusinessProfileId = facilityDto.BusinessProfileId;
            facility.Street = facilityDto.Street;
            facility.City = facilityDto.City;
            facility.State = facilityDto.State;
            facility.PostalCode = facilityDto.PostalCode;
            facility.Country = facilityDto.Country;
            facility.AddressLine2 = facilityDto.AddressLine2;
            facility.Latitude = facilityDto.Latitude;
            facility.Longitude = facilityDto.Longitude;
            facility.UpdatedAt = DateTime.UtcNow;

            _context.SaveChanges();

            // Update schedule templates if availability is provided
            if (facilityDto.Availability != null)
            {
                UpdateFacilityScheduleTemplates(facility.Id, facilityDto.Availability);
            }

            return facility;
        }

        public void UpdateFacilityScheduleTemplates(Guid facilityId, FacilityAvailabilityDto availability)
        {
            var scheduleTemplates = new List<FacilityScheduleTemplate>();
            var dateAvailabilities = new List<FacilityDateAvailability>();

            // Only remove and update weekdays if provided
            if (availability.Weekdays.Any())
            {
                var existingWeekdays = _context.FacilityScheduleTemplates
                    .Where(st => st.FacilityId == facilityId && st.ScheduleType == ScheduleType.Weekdays)
                    .ToList();
                _context.FacilityScheduleTemplates.RemoveRange(existingWeekdays);

                foreach (var slot in availability.Weekdays)
                {
                    scheduleTemplates.Add(new FacilityScheduleTemplate
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = facilityId,
                        ScheduleType = ScheduleType.Weekdays,
                        Time = slot.Time,
                        IsAvailable = slot.IsAvailable,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Only remove and update Saturday if provided
            if (availability.Saturday.Any())
            {
                var existingSaturday = _context.FacilityScheduleTemplates
                    .Where(st => st.FacilityId == facilityId && st.ScheduleType == ScheduleType.Saturday)
                    .ToList();
                _context.FacilityScheduleTemplates.RemoveRange(existingSaturday);

                foreach (var slot in availability.Saturday)
                {
                    scheduleTemplates.Add(new FacilityScheduleTemplate
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = facilityId,
                        ScheduleType = ScheduleType.Saturday,
                        Time = slot.Time,
                        IsAvailable = slot.IsAvailable,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Only remove and update Sunday if provided
            if (availability.Sunday.Any())
            {
                var existingSunday = _context.FacilityScheduleTemplates
                    .Where(st => st.FacilityId == facilityId && st.ScheduleType == ScheduleType.Sunday)
                    .ToList();
                _context.FacilityScheduleTemplates.RemoveRange(existingSunday);

                foreach (var slot in availability.Sunday)
                {
                    scheduleTemplates.Add(new FacilityScheduleTemplate
                    {
                        Id = Guid.NewGuid(),
                        FacilityId = facilityId,
                        ScheduleType = ScheduleType.Sunday,
                        Time = slot.Time,
                        IsAvailable = slot.IsAvailable,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            // Only remove and update specific dates that are provided
            foreach (var dateEntry in availability.SpecificDates)
            {
                if (DateTime.TryParse(dateEntry.Key, out var specificDate))
                {
                    var utcDate = DateTime.SpecifyKind(specificDate.Date, DateTimeKind.Utc);
                    
                    // Remove existing availabilities for this specific date only
                    var existingDateAvailabilities = _context.FacilityDateAvailabilities
                        .Where(da => da.FacilityId == facilityId && da.Date == utcDate)
                        .ToList();
                    _context.FacilityDateAvailabilities.RemoveRange(existingDateAvailabilities);

                    // Add new availabilities for this specific date
                    foreach (var slot in dateEntry.Value)
                    {
                        dateAvailabilities.Add(new FacilityDateAvailability
                        {
                            Id = Guid.NewGuid(),
                            FacilityId = facilityId,
                            Date = utcDate,
                            Time = slot.Time,
                            IsAvailable = slot.IsAvailable,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            _context.FacilityScheduleTemplates.AddRange(scheduleTemplates);
            _context.FacilityDateAvailabilities.AddRange(dateAvailabilities);
            _context.SaveChanges();
        }

        public FacilityDateTimeSlotsDto? GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date)
        {
            var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            // Get facility with both schedule templates and date availabilities
            var facility = _context.Facilities
                .Include(f => f.ScheduleTemplates)
                .Include(f => f.DateAvailabilities.Where(da => da.Date == utcDate))
                .FirstOrDefault(f => f.Id == facilityId);

            if (facility == null)
                return null;

            // Get facility's existing reservations for this date
            var facilityReservations = _context.Reservations
                .Where(r => r.FacilityId == facilityId &&
                           r.Date == utcDate &&
                           r.Status == "Active")
                .ToList();

            var bookedTimeSlots = facilityReservations
                .SelectMany(r => r.TimeSlots)
                .ToHashSet();

            // Determine schedule type based on date
            var scheduleType = utcDate.DayOfWeek switch
            {
                DayOfWeek.Saturday => ScheduleType.Saturday,
                DayOfWeek.Sunday => ScheduleType.Sunday,
                _ => ScheduleType.Weekdays
            };

            // Get business profile for boundary enforcement
            var businessProfile = _context.BusinessProfiles
                .Include(bp => bp.ScheduleTemplates.Where(st => st.ScheduleType == scheduleType))
                .Include(bp => bp.DateAvailabilities.Where(da => da.Date == utcDate))
                .FirstOrDefault(bp => bp.UserId == facility.UserId);

            // STEP 1: Check if business has date-specific availabilities for this date (highest priority - overrides everything)
            var businessDateAvailabilities = businessProfile?.DateAvailabilities.Where(da => da.Date == utcDate).ToList();
            if (businessDateAvailabilities?.Any() == true)
            {
                // Business date-specific slots override everything - return these as-is
                var timeSlots = businessDateAvailabilities.Select(bda => new TimeSlotItemDto
                {
                    Id = bda.Time,
                    Time = bda.Time,
                    IsAvailable = bda.IsAvailable,
                    IsBooked = bookedTimeSlots.Contains(bda.Time),
                    BookedBy = null
                }).OrderBy(ts => ts.Time).ToList();

                return new FacilityDateTimeSlotsDto
                {
                    Date = utcDate,
                    TimeSlots = timeSlots,
                    IsFromFacilityTemplate = false,
                    IsFromBusinessTemplate = true,
                    TemplateType = "date-specific"
                };
            }

            // STEP 2: Get business schedule templates (base layer - defines boundary)
            var businessTemplates = businessProfile?.ScheduleTemplates.ToList() ?? new List<BusinessScheduleTemplate>();

            if (!businessTemplates.Any())
            {
                // No business templates exist - no availability possible
                return new FacilityDateTimeSlotsDto
                {
                    Date = utcDate,
                    TimeSlots = new List<TimeSlotItemDto>(),
                    IsFromFacilityTemplate = false,
                    IsFromBusinessTemplate = false,
                    TemplateType = null
                };
            }

            // STEP 3: Check for facility date-specific availabilities
            var facilityDateAvailabilities = facility.DateAvailabilities.Where(da => da.Date == utcDate).ToList();

            if (facilityDateAvailabilities.Any())
            {
                // Merge facility date-specific with business template boundary enforcement
                var businessTemplateDict = businessTemplates.ToDictionary(bt => bt.Time);
                var timeSlots = new List<TimeSlotItemDto>();

                foreach (var facilitySlot in facilityDateAvailabilities)
                {
                    // Only include if business template exists for this time
                    if (businessTemplateDict.ContainsKey(facilitySlot.Time))
                    {
                        var businessTemplate = businessTemplateDict[facilitySlot.Time];
                        // BOUNDARY ENFORCEMENT: Facility can only be available if business template is available
                        var isAvailable = facilitySlot.IsAvailable && businessTemplate.IsAvailable;

                        timeSlots.Add(new TimeSlotItemDto
                        {
                            Id = facilitySlot.Time,
                            Time = facilitySlot.Time,
                            IsAvailable = isAvailable,
                            IsBooked = bookedTimeSlots.Contains(facilitySlot.Time),
                            BookedBy = null
                        });
                    }
                }

                // Add business template slots that don't have facility overrides
                foreach (var businessTemplate in businessTemplates)
                {
                    if (!facilityDateAvailabilities.Any(fda => fda.Time == businessTemplate.Time))
                    {
                        timeSlots.Add(new TimeSlotItemDto
                        {
                            Id = businessTemplate.Time,
                            Time = businessTemplate.Time,
                            IsAvailable = businessTemplate.IsAvailable,
                            IsBooked = bookedTimeSlots.Contains(businessTemplate.Time),
                            BookedBy = null
                        });
                    }
                }

                return new FacilityDateTimeSlotsDto
                {
                    Date = utcDate,
                    TimeSlots = timeSlots.OrderBy(ts => ts.Time).ToList(),
                    IsFromFacilityTemplate = false,
                    IsFromBusinessTemplate = false,
                    TemplateType = "date-specific"
                };
            }

            // STEP 4: Check if there are facility-specific templates
            var facilityTemplateSlots = facility.ScheduleTemplates
                .Where(st => st.ScheduleType == scheduleType)
                .ToList();

            if (facilityTemplateSlots.Any())
            {
                // Merge facility templates with business template boundary enforcement
                var businessTemplateDict = businessTemplates.ToDictionary(bt => bt.Time);
                var timeSlots = new List<TimeSlotItemDto>();

                foreach (var facilityTemplate in facilityTemplateSlots)
                {
                    // Only include if business template exists for this time
                    if (businessTemplateDict.ContainsKey(facilityTemplate.Time))
                    {
                        var businessTemplate = businessTemplateDict[facilityTemplate.Time];
                        // BOUNDARY ENFORCEMENT: Facility can only be available if business template is available
                        var isAvailable = facilityTemplate.IsAvailable && businessTemplate.IsAvailable;

                        timeSlots.Add(new TimeSlotItemDto
                        {
                            Id = facilityTemplate.Time,
                            Time = facilityTemplate.Time,
                            IsAvailable = isAvailable,
                            IsBooked = bookedTimeSlots.Contains(facilityTemplate.Time),
                            BookedBy = null
                        });
                    }
                }

                // Add business template slots that don't have facility overrides
                foreach (var businessTemplate in businessTemplates)
                {
                    if (!facilityTemplateSlots.Any(fts => fts.Time == businessTemplate.Time))
                    {
                        timeSlots.Add(new TimeSlotItemDto
                        {
                            Id = businessTemplate.Time,
                            Time = businessTemplate.Time,
                            IsAvailable = businessTemplate.IsAvailable,
                            IsBooked = bookedTimeSlots.Contains(businessTemplate.Time),
                            BookedBy = null
                        });
                    }
                }

                return new FacilityDateTimeSlotsDto
                {
                    Date = utcDate,
                    TimeSlots = timeSlots.OrderBy(ts => ts.Time).ToList(),
                    IsFromFacilityTemplate = true,
                    IsFromBusinessTemplate = false,
                    TemplateType = scheduleType.ToString().ToLower()
                };
            }

            // STEP 5: Fall back to business schedule templates only
            var businessTemplateOnlySlots = businessTemplates.Select(st => new TimeSlotItemDto
            {
                Id = st.Time,
                Time = st.Time,
                IsAvailable = st.IsAvailable,
                IsBooked = bookedTimeSlots.Contains(st.Time),
                BookedBy = null
            }).OrderBy(ts => ts.Time).ToList();

            return new FacilityDateTimeSlotsDto
            {
                Date = utcDate,
                TimeSlots = businessTemplateOnlySlots,
                IsFromFacilityTemplate = false,
                IsFromBusinessTemplate = true,
                TemplateType = scheduleType.ToString().ToLower()
            };
        }
    }
}
