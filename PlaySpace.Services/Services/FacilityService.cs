using PlaySpace.Repositories;
using PlaySpace.Services.Interfaces;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using System.Text.Json;

public class FacilityService : IFacilityService
{
    private readonly IFacilityRepository _facilityRepository;
    private readonly ITimeSlotRepository _timeSlotRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IBusinessProfileAgentRepository _businessProfileAgentRepository;

    public FacilityService(IFacilityRepository facilityRepository, ITimeSlotRepository timeSlotRepository, IBusinessProfileRepository businessProfileRepository, IReservationRepository reservationRepository, IBusinessProfileAgentRepository businessProfileAgentRepository)
    {
        _facilityRepository = facilityRepository;
        _timeSlotRepository = timeSlotRepository;
        _businessProfileRepository = businessProfileRepository;
        _reservationRepository = reservationRepository;
        _businessProfileAgentRepository = businessProfileAgentRepository;
    }

    public List<Facility> GetFacilities(SearchFiltersDto filters)
    {
        return _facilityRepository.GetFacilities(filters);
    }

    public List<FacilitySearchResultDto> SearchFacilities(SearchFiltersDto filters)
    {
        var facilities = _facilityRepository.SearchFacilities(filters);
        var results = new List<FacilitySearchResultDto>();
        
        foreach (var facility in facilities)
        {
            var result = MapToSearchResultDto(facility);
            
            // If a specific date is requested, check availability for that date
            if (filters.Date.HasValue)
            {
                var timeSlots = _timeSlotRepository.GetFacilityTimeSlotsForDate(facility.Id, filters.Date.Value);
                var availableSlots = timeSlots.Where(ts => ts.IsAvailable && !ts.IsBooked).ToList();
                
                result.HasAvailability = availableSlots.Any();
                result.AvailableSlots = availableSlots.Count;
                result.AvailableTimes = availableSlots.Select(ts => ts.Time).OrderBy(t => t).ToList();
                
                // If OnlyAvailable is true and no slots are available, skip this facility
                if (filters.OnlyAvailable == true && !result.HasAvailability)
                {
                    continue;
                }
            }
            else
            {
                // For general search without specific date, indicate if facility has any time slots configured
                var allTimeSlots = _timeSlotRepository.GetFacilityTimeSlots(facility.Id);
                result.HasAvailability = allTimeSlots.Any();
                result.AvailableSlots = allTimeSlots.Count(ts => ts.IsAvailable && !ts.IsBooked);
                result.AvailableTimes = new List<string>(); // Don't populate specific times for general search
            }
            
            results.Add(result);
        }
        
        return results.OrderBy(r => r.Name).ToList();
    }

    public FacilityDto CreateFacility(CreateFacilityDto facilityDto, Guid userId)
    {
        var facility = _facilityRepository.CreateFacility(facilityDto, userId);
        return MapToDto(facility);
    }

    public FacilityDto? GetFacility(Guid id)
    {
        var facility = _facilityRepository.GetFacility(id);
        return facility == null ? null : MapToDto(facility);
    }

    public List<FacilityDto> GetUserFacilities(Guid userId)
    {
        var facilities = _facilityRepository.GetUserFacilities(userId);
        return facilities.Select(MapToDto).ToList();
    }

    public bool DeleteFacility(Guid facilityId, Guid userId)
    {
        var facility = _facilityRepository.GetFacility(facilityId);
        if (facility == null)
        {
            return false;
        }

        // Check if user is the facility owner OR an authorized agent
        var isFacilityOwner = facility.UserId == userId;
        var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
            _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(userId, facility.BusinessProfileId.Value).Result;

        if (!isFacilityOwner && !isAuthorizedAgent)
        {
            throw new UnauthorizedAccessException("You can only delete facilities you own or manage as an agent");
        }

        return _facilityRepository.DeleteFacility(facilityId);
    }

    public FacilityDto? UpdateFacility(Guid facilityId, UpdateFacilityDto facilityDto, Guid userId)
    {
        var existingFacility = _facilityRepository.GetFacility(facilityId);
        if (existingFacility == null)
        {
            return null;
        }

        // Check if user is the facility owner OR an authorized agent
        var isFacilityOwner = existingFacility.UserId == userId;
        var isAuthorizedAgent = existingFacility.BusinessProfileId.HasValue &&
            _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(userId, existingFacility.BusinessProfileId.Value).Result;

        if (!isFacilityOwner && !isAuthorizedAgent)
        {
            throw new UnauthorizedAccessException("You can only update facilities you own or manage as an agent");
        }

        var updatedFacility = _facilityRepository.UpdateFacility(facilityId, facilityDto);
        return updatedFacility == null ? null : MapToDto(updatedFacility);
    }

    private FacilityDto MapToDto(Facility facility)
    {
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
            MinBookingSlots = facility.MinBookingSlots,
            UserId = facility.UserId,
            BusinessProfileId = facility.BusinessProfileId,
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
            Availability = MapScheduleTemplatesToAvailabilityWithFallback(facility)
        };
    }

    private FacilitySearchResultDto MapToSearchResultDto(Facility facility)
    {
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
            MinBookingSlots = facility.MinBookingSlots,
            UserId = facility.UserId,
            BusinessProfileId = facility.BusinessProfileId,
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
            Availability = MapScheduleTemplatesToAvailabilityWithFallback(facility),
            HasAvailability = false,
            AvailableSlots = 0,
            AvailableTimes = new List<string>()
        };
    }

    public void UpdateFacilityScheduleTemplates(Guid facilityId, FacilityAvailabilityDto availability, Guid userId)
    {
        // Verify that the user owns this facility OR is an authorized agent
        var facility = _facilityRepository.GetFacility(facilityId);
        if (facility == null)
        {
            throw new UnauthorizedAccessException("Facility not found");
        }

        // Check if user is the facility owner OR an authorized agent
        var isFacilityOwner = facility.UserId == userId;
        var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
            _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(userId, facility.BusinessProfileId.Value).Result;

        if (!isFacilityOwner && !isAuthorizedAgent)
        {
            throw new UnauthorizedAccessException("You don't have permission to update this facility's schedule");
        }

        _facilityRepository.UpdateFacilityScheduleTemplates(facilityId, availability);
    }

    public FacilityDateTimeSlotsDto? GetFacilityTimeSlotsForDate(Guid facilityId, DateTime date)
    {
        return _facilityRepository.GetFacilityTimeSlotsForDate(facilityId, date);
    }

    private FacilityAvailabilityDto MapScheduleTemplatesToAvailabilityWithFallback(Facility facility)
    {
        var availability = new FacilityAvailabilityDto();

        // If facility has schedule templates, use them
        if (facility.ScheduleTemplates.Any())
        {
            return MapScheduleTemplatesToAvailability(facility.ScheduleTemplates);
        }

        // Fallback to business templates if no facility templates exist
        var businessProfile = _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);
        if (businessProfile?.ScheduleTemplates?.Any() == true)
        {
            // Group business templates by schedule type
            var groupedTemplates = businessProfile.ScheduleTemplates.GroupBy(st => st.ScheduleType);

            foreach (var group in groupedTemplates)
            {
                var timeSlots = group.Select(template => new TimeSlotItemDto
                {
                    Id = template.Time,
                    Time = template.Time,
                    IsAvailable = template.IsAvailable,
                    IsBooked = false,
                    BookedBy = null
                }).OrderBy(ts => ts.Time).ToList();

                switch (group.Key)
                {
                    case ScheduleType.Weekdays:
                        availability.Weekdays = timeSlots;
                        break;
                    case ScheduleType.Saturday:
                        availability.Saturday = timeSlots;
                        break;
                    case ScheduleType.Sunday:
                        availability.Sunday = timeSlots;
                        break;
                }
            }
        }

        return availability;
    }

    private FacilityAvailabilityDto MapScheduleTemplatesToAvailability(List<FacilityScheduleTemplate> scheduleTemplates)
    {
        var availability = new FacilityAvailabilityDto();

        // Group templates by schedule type - only weekly templates, no specific dates
        var groupedTemplates = scheduleTemplates.GroupBy(st => st.ScheduleType);

        foreach (var group in groupedTemplates)
        {
            var timeSlots = group.Select(template => new TimeSlotItemDto
            {
                Id = template.Time,
                Time = template.Time,
                IsAvailable = template.IsAvailable,
                IsBooked = false,
                BookedBy = null
            }).OrderBy(ts => ts.Time).ToList();

            switch (group.Key)
            {
                case ScheduleType.Weekdays:
                    availability.Weekdays = timeSlots;
                    break;
                case ScheduleType.Saturday:
                    availability.Saturday = timeSlots;
                    break;
                case ScheduleType.Sunday:
                    availability.Sunday = timeSlots;
                    break;
            }
        }

        // Don't include specific dates in general facility data
        // Use the separate endpoint /api/facility/{id}/timeslots/{date} for specific dates

        return availability;
    }

    public FacilityDateTimeSlotsWithBookingsDto? GetFacilityTimeSlotsWithBookings(Guid facilityId, DateTime date, Guid userId)
    {
        // Get facility
        var facility = _facilityRepository.GetFacility(facilityId);
        if (facility == null)
        {
            return null;
        }

        // Verify that user owns this facility OR is an authorized agent
        var isFacilityOwner = facility.UserId == userId;
        var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
            _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(userId, facility.BusinessProfileId.Value).Result;

        if (!isFacilityOwner && !isAuthorizedAgent)
        {
            throw new UnauthorizedAccessException("You don't have permission to view bookings for this facility");
        }

        // Get timeslots for the date (using refactored logic with business boundary enforcement)
        var timeSlotsDto = _facilityRepository.GetFacilityTimeSlotsForDate(facilityId, date);
        if (timeSlotsDto == null)
        {
            return null;
        }

        // Get all reservations for this facility and date
        var reservations = _reservationRepository.GetReservationsForFacilityAndDate(facilityId, date);

        // Map reservations to summary DTOs
        var reservationSummaries = reservations.Select(r => new ReservationSummaryDto
        {
            Id = r.Id,
            UserId = r.UserId,
            UserName = r.User?.FirstName != null && r.User?.LastName != null
                ? $"{r.User.FirstName} {r.User.LastName}"
                : null,
            UserEmail = r.User?.Email,
            GuestName = r.GuestName,
            GuestPhone = r.GuestPhone,
            GuestEmail = r.GuestEmail,
            TimeSlots = r.TimeSlots,
            DetailedTimeSlots = r.Slots.Select(s => new ReservationSlotDetailDto
            {
                Id = s.Id,
                TimeSlot = s.TimeSlot,
                SlotPrice = s.SlotPrice,
                Status = s.Status,
                CancelledAt = s.CancelledAt,
                CancellationReason = s.CancellationReason
            }).ToList(),
            TotalPrice = r.TotalPrice,
            RemainingPrice = r.RemainingPrice,
            Status = r.Status,
            TrainerProfileId = r.TrainerProfileId,
            TrainerDisplayName = r.TrainerProfile?.DisplayName,
            PaymentId = r.PaymentId,
            PaymentDetails = r.Payment != null ? new PaymentDetailsDto
            {
                Id = r.Payment.Id,
                Amount = r.Payment.Amount,
                Status = r.Payment.Status,
                TPayStatus = r.Payment.TPayStatus,
                TPayCompletedAt = r.Payment.TPayCompletedAt,
                IsRefunded = r.Payment.IsRefunded,
                RefundedAmount = r.Payment.RefundedAmount,
                RefundedAt = r.Payment.RefundedAt,
                IsPaid = r.Payment.TPayStatus == "correct" || r.Payment.Status == "Completed",
                PaymentMethod = r.Payment.PaymentMethod
            } : null,
            ProductPurchaseId = r.ProductPurchaseId,
            ProductPurchaseDetails = r.ProductPurchase != null ? new ProductPurchaseSummaryDto
            {
                Id = r.ProductPurchase.Id,
                ProductId = r.ProductPurchase.ProductId,
                ProductTitle = r.ProductPurchase.ProductTitle,
                ProductSubtitle = r.ProductPurchase.ProductSubtitle,
                BusinessName = r.ProductPurchase.BusinessName,
                BusinessProfileId = r.ProductPurchase.BusinessProfileId,
                TotalUsage = r.ProductPurchase.TotalUsage,
                RemainingUsage = r.ProductPurchase.RemainingUsage,
                Status = r.ProductPurchase.Status,
                ExpiryDate = r.ProductPurchase.ExpiryDate,
                AppliesToAllFacilities = r.ProductPurchase.AppliesToAllFacilities,
                FacilityIds = !string.IsNullOrEmpty(r.ProductPurchase.FacilityIds)
                    ? JsonSerializer.Deserialize<List<string>>(r.ProductPurchase.FacilityIds)
                    : null
            } : null,
            CreatedAt = r.CreatedAt
        }).ToList();

        return new FacilityDateTimeSlotsWithBookingsDto
        {
            FacilityId = facility.Id,
            FacilityName = facility.Name,
            Date = date,
            TimeSlots = timeSlotsDto.TimeSlots,
            Reservations = reservationSummaries,
            IsFromFacilityTemplate = timeSlotsDto.IsFromFacilityTemplate,
            IsFromBusinessTemplate = timeSlotsDto.IsFromBusinessTemplate,
            TemplateType = timeSlotsDto.TemplateType
        };
    }
}
