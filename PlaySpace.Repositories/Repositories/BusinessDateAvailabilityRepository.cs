using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;

namespace PlaySpace.Repositories.Repositories;

public class BusinessDateAvailabilityRepository : IBusinessDateAvailabilityRepository
{
    private readonly PlaySpaceDbContext _context;

    public BusinessDateAvailabilityRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public BusinessDateAvailabilityDto? GetBusinessDateAvailability(Guid businessProfileId, DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        
        // Get business profile with templates
        var businessProfile = _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .FirstOrDefault(bp => bp.Id == businessProfileId);
            
        if (businessProfile == null)
        {
            return null;
        }

        // Determine schedule type for the date
        var scheduleType = GetScheduleTypeForDate(utcDate);
        
        // Get base schedule template for this day type
        var templateSlots = businessProfile.ScheduleTemplates
            .Where(st => st.ScheduleType == scheduleType)
            .OrderBy(st => st.Time)
            .ToList();

        // Get date-specific overrides
        var dateOverrides = _context.BusinessDateAvailabilities
            .Where(bda => bda.BusinessProfileId == businessProfileId && bda.Date == utcDate)
            .ToDictionary(bda => bda.Time);

        // Build merged availability
        var timeSlots = new List<BusinessDateAvailabilitySlotDto>();
        
        // Add template slots, with overrides if they exist
        foreach (var template in templateSlots)
        {
            bool isFromTemplate = true;
            bool isAvailable = template.IsAvailable;
            
            if (dateOverrides.ContainsKey(template.Time))
            {
                isFromTemplate = false;
                isAvailable = dateOverrides[template.Time].IsAvailable;
                dateOverrides.Remove(template.Time); // Remove so we don't add it again
            }
            
            timeSlots.Add(new BusinessDateAvailabilitySlotDto
            {
                Time = template.Time,
                IsAvailable = isAvailable,
                IsFromTemplate = isFromTemplate
            });
        }
        
        // Add any date-specific slots that don't have templates
        foreach (var dateOverride in dateOverrides.Values)
        {
            timeSlots.Add(new BusinessDateAvailabilitySlotDto
            {
                Time = dateOverride.Time,
                IsAvailable = dateOverride.IsAvailable,
                IsFromTemplate = false
            });
        }

        return new BusinessDateAvailabilityDto
        {
            Date = utcDate,
            TimeSlots = timeSlots.OrderBy(t => t.Time).ToList(),
            TemplateType = GetTemplateTypeName(scheduleType)
        };
    }

    public BusinessDateAvailabilityDto CreateBusinessDateAvailability(Guid businessProfileId, CreateBusinessDateAvailabilityDto dto)
    {
        var utcDate = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
        
        // Remove existing date-specific availability for this date
        var existing = _context.BusinessDateAvailabilities
            .Where(bda => bda.BusinessProfileId == businessProfileId && bda.Date == utcDate)
            .ToList();
        _context.BusinessDateAvailabilities.RemoveRange(existing);

        // Add new date-specific availability
        foreach (var slot in dto.TimeSlots.Where(s => !s.IsFromTemplate))
        {
            var availability = new BusinessDateAvailability
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = businessProfileId,
                Date = utcDate,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.BusinessDateAvailabilities.Add(availability);
        }

        _context.SaveChanges();
        
        return GetBusinessDateAvailability(businessProfileId, utcDate)!;
    }

    public BusinessDateAvailabilityDto? UpdateBusinessDateAvailability(Guid businessProfileId, UpdateBusinessDateAvailabilityDto dto)
    {
        return CreateBusinessDateAvailability(businessProfileId, new CreateBusinessDateAvailabilityDto
        {
            Date = dto.Date,
            TimeSlots = dto.TimeSlots
        });
    }

    public bool DeleteBusinessDateAvailability(Guid businessProfileId, DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        
        var existing = _context.BusinessDateAvailabilities
            .Where(bda => bda.BusinessProfileId == businessProfileId && bda.Date == utcDate)
            .ToList();
            
        if (!existing.Any())
        {
            return false;
        }

        _context.BusinessDateAvailabilities.RemoveRange(existing);
        _context.SaveChanges();
        return true;
    }

    public List<BusinessDateAvailability> GetBusinessDateAvailabilities(Guid businessProfileId, DateTime startDate, DateTime endDate)
    {
        var utcStartDate = DateTime.SpecifyKind(startDate.Date, DateTimeKind.Utc);
        var utcEndDate = DateTime.SpecifyKind(endDate.Date, DateTimeKind.Utc);
        
        return _context.BusinessDateAvailabilities
            .Where(bda => bda.BusinessProfileId == businessProfileId && 
                         bda.Date >= utcStartDate && 
                         bda.Date <= utcEndDate)
            .OrderBy(bda => bda.Date)
            .ThenBy(bda => bda.Time)
            .ToList();
    }

    private ScheduleType GetScheduleTypeForDate(DateTime date)
    {
        return date.DayOfWeek switch
        {
            DayOfWeek.Saturday => ScheduleType.Saturday,
            DayOfWeek.Sunday => ScheduleType.Sunday,
            _ => ScheduleType.Weekdays
        };
    }

    private string GetTemplateTypeName(ScheduleType scheduleType)
    {
        return scheduleType switch
        {
            ScheduleType.Saturday => "saturday",
            ScheduleType.Sunday => "sunday",
            _ => "weekdays"
        };
    }
}