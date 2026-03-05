using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class SearchRepository : ISearchRepository
{
    private readonly PlaySpaceDbContext _context;

    public SearchRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public SearchResponseDto SearchBusinesses(SearchCriteriaDto criteria)
    {
        var businessProfiles = _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .AsQueryable();

        // Apply location filter if provided (case-insensitive)
        if (!string.IsNullOrEmpty(criteria.Location))
        {
            var searchTerm = criteria.Location.ToLower().Trim();
            businessProfiles = businessProfiles.Where(bp =>
                bp.City.ToLower().Contains(searchTerm) ||
                bp.Address.ToLower().Contains(searchTerm) ||
                bp.CompanyName.ToLower().Contains(searchTerm) ||
                bp.DisplayName.ToLower().Contains(searchTerm));
        }

        var results = new List<BusinessSearchResultDto>();

        foreach (var businessProfile in businessProfiles.ToList())
        {
            var facilities = _context.Facilities
                .Where(f => f.UserId == businessProfile.UserId)
                .AsQueryable();

            // Apply facility type filter if provided
            if (!string.IsNullOrEmpty(criteria.FacilityType))
            {
                // Normalize the search term - replace + with space and underscore, trim
                var normalizedSearchTerm = criteria.FacilityType
                    .Replace("+", " ")
                    .Replace("_", " ")
                    .Trim()
                    .ToLower();

                var searchTermWithUnderscore = normalizedSearchTerm.Replace(" ", "_").ToLower();
                var originalTerm = criteria.FacilityType.ToLower();

                // Load facilities to memory first for complex string operations
                var facilitiesForFiltering = facilities.ToList();

                // Filter in memory with flexible matching
                var filteredFacilities = facilitiesForFiltering.Where(f => 
                {
                    var facilityType = f.Type.ToLower();
                    var facilityTypeNormalized = facilityType.Replace("_", " ");

                    return facilityType.Contains(originalTerm) ||
                           facilityType.Contains(searchTermWithUnderscore) ||
                           facilityTypeNormalized.Contains(normalizedSearchTerm);
                }).ToList();

                // Convert back to queryable for the rest of the logic
                facilities = filteredFacilities.AsQueryable();
            }

            var facilitiesList = facilities.ToList();
            var totalFacilitiesCount = facilitiesList.Count;

            if (totalFacilitiesCount == 0) continue; // Skip businesses with no matching facilities

            int availableFacilitiesCount = 0;

            // Check availability for each facility
            foreach (var facility in facilitiesList)
            {
                bool isAvailable = IsFacilityAvailable(facility.Id, businessProfile, criteria.Date, criteria.Time);
                if (isAvailable)
                {
                    availableFacilitiesCount++;
                }
            }

            // Only include businesses that have at least one available facility (or no time/date criteria)
            if (availableFacilitiesCount > 0 || (criteria.Date == null && criteria.Time == null))
            {
                results.Add(new BusinessSearchResultDto
                {
                    Id = businessProfile.Id,
                    CompanyName = businessProfile.CompanyName,
                    DisplayName = businessProfile.DisplayName,
                    Address = businessProfile.Address,
                    City = businessProfile.City,
                    PostalCode = businessProfile.PostalCode,
                    Latitude = businessProfile.Latitude,
                    Longitude = businessProfile.Longitude,
                    AvatarUrl = businessProfile.AvatarUrl,
                    
                    // Facility plan fields
                    FacilityPlanUrl = businessProfile.FacilityPlanUrl,
                    FacilityPlanFileName = businessProfile.FacilityPlanFileName,
                    FacilityPlanFileType = businessProfile.FacilityPlanFileType,
                    
                    AvailableFacilitiesCount = availableFacilitiesCount,
                    TotalFacilitiesCount = totalFacilitiesCount
                });
            }
        }

        return new SearchResponseDto
        {
            Results = results.OrderByDescending(r => r.AvailableFacilitiesCount).ToList(),
            TotalBusinesses = results.Count
        };
    }

    public SearchResponseDto SearchBusinessesByLocation(LocationSearchCriteriaDto criteria)
    {
        // Get all business profiles with coordinates
        var businessProfiles = _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .Where(bp => bp.Latitude.HasValue && bp.Longitude.HasValue)
            .ToList();

        var results = new List<BusinessSearchResultDto>();

        foreach (var businessProfile in businessProfiles)
        {
            // Calculate distance using Haversine formula
            var distance = CalculateDistance(
                criteria.Latitude, criteria.Longitude,
                businessProfile.Latitude!.Value, businessProfile.Longitude!.Value);

            // Skip if outside radius
            if (distance > criteria.Radius) continue;

            var facilities = _context.Facilities
                .Where(f => f.UserId == businessProfile.UserId)
                .AsQueryable();

            // Apply facility type filter if provided
            if (!string.IsNullOrEmpty(criteria.FacilityType))
            {
                var normalizedSearchTerm = criteria.FacilityType
                    .Replace("+", " ")
                    .Replace("_", " ")
                    .Trim()
                    .ToLower();

                var searchTermWithUnderscore = normalizedSearchTerm.Replace(" ", "_").ToLower();
                var originalTerm = criteria.FacilityType.ToLower();

                var facilitiesForFiltering = facilities.ToList();
                var filteredFacilities = facilitiesForFiltering.Where(f => 
                {
                    var facilityType = f.Type.ToLower();
                    var facilityTypeNormalized = facilityType.Replace("_", " ");

                    return facilityType.Contains(originalTerm) ||
                           facilityType.Contains(searchTermWithUnderscore) ||
                           facilityTypeNormalized.Contains(normalizedSearchTerm);
                }).ToList();

                facilities = filteredFacilities.AsQueryable();
            }

            var facilitiesList = facilities.ToList();
            var totalFacilitiesCount = facilitiesList.Count;

            if (totalFacilitiesCount == 0) continue; // Skip businesses with no matching facilities

            int availableFacilitiesCount = 0;

            // Check availability for each facility if date is provided
            if (criteria.Date.HasValue)
            {
                foreach (var facility in facilitiesList)
                {
                    bool isAvailable = IsFacilityAvailableForDate(facility.Id, businessProfile, criteria.Date.Value);
                    if (isAvailable)
                    {
                        availableFacilitiesCount++;
                    }
                }
            }
            else
            {
                // If no date specified, consider all facilities as potentially available
                availableFacilitiesCount = totalFacilitiesCount;
            }

            // Only include businesses that have at least one available facility (or no date criteria)
            if (availableFacilitiesCount > 0 || !criteria.Date.HasValue)
            {
                results.Add(new BusinessSearchResultDto
                {
                    Id = businessProfile.Id,
                    CompanyName = businessProfile.CompanyName,
                    DisplayName = businessProfile.DisplayName,
                    Address = businessProfile.Address,
                    City = businessProfile.City,
                    PostalCode = businessProfile.PostalCode,
                    Latitude = businessProfile.Latitude,
                    Longitude = businessProfile.Longitude,
                    AvatarUrl = businessProfile.AvatarUrl,
                    
                    // Facility plan fields
                    FacilityPlanUrl = businessProfile.FacilityPlanUrl,
                    FacilityPlanFileName = businessProfile.FacilityPlanFileName,
                    FacilityPlanFileType = businessProfile.FacilityPlanFileType,
                    
                    AvailableFacilitiesCount = availableFacilitiesCount,
                    TotalFacilitiesCount = totalFacilitiesCount,
                    Distance = Math.Round(distance, 2) // Round to 2 decimal places
                });
            }
        }

        return new SearchResponseDto
        {
            Results = results.OrderBy(r => r.Distance).ToList(), // Order by distance (closest first)
            TotalBusinesses = results.Count
        };
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Haversine formula to calculate distance between two points on Earth
        const double R = 6371; // Earth's radius in kilometers
        
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }

    private bool IsFacilityAvailableForDate(Guid facilityId, BusinessProfile businessProfile, DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        
        // Determine schedule type based on date
        var scheduleType = utcDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => ScheduleType.Saturday,
            DayOfWeek.Sunday => ScheduleType.Sunday,
            _ => ScheduleType.Weekdays
        };

        // Check if there are any available time slots for this facility on this date
        // Check date-specific slots first
        var dateSpecificSlots = _context.TimeSlots
            .Where(ts => ts.FacilityId == facilityId && 
                        ts.Date == utcDate && 
                        !ts.IsAllTime &&
                        ts.IsAvailable && !ts.IsBooked)
            .Any();

        if (dateSpecificSlots) return true;

        // Check facility all-time overrides
        var facilityOverrides = _context.TimeSlots
            .Where(ts => ts.FacilityId == facilityId && 
                        ts.IsAllTime &&
                        ts.ScheduleType == scheduleType &&
                        ts.IsAvailable && !ts.IsBooked)
            .Any();

        if (facilityOverrides) return true;

        // Fall back to business profile templates
        var businessTemplates = businessProfile.ScheduleTemplates
            .Where(st => st.ScheduleType == scheduleType && st.IsAvailable)
            .Any();

        return businessTemplates;
    }

    private bool IsFacilityAvailable(Guid facilityId, BusinessProfile businessProfile, DateTime? date, string? time)
    {
        // If no date/time criteria, consider it available
        if (date == null || string.IsNullOrEmpty(time))
        {
            return true;
        }

        var utcDate = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
        
        // Determine schedule type based on date
        var scheduleType = utcDate.DayOfWeek switch
        {
            DayOfWeek.Saturday => ScheduleType.Saturday,
            DayOfWeek.Sunday => ScheduleType.Sunday,
            _ => ScheduleType.Weekdays
        };

        // Check date-specific overrides first (highest precedence)
        var dateSpecificSlot = _context.TimeSlots
            .FirstOrDefault(ts => ts.FacilityId == facilityId && 
                                ts.Date == utcDate && 
                                ts.Time == time &&
                                !ts.IsAllTime);

        if (dateSpecificSlot != null)
        {
            return dateSpecificSlot.IsAvailable && !dateSpecificSlot.IsBooked;
        }

        // Check facility all-time override (second precedence)
        var facilityOverride = _context.TimeSlots
            .FirstOrDefault(ts => ts.FacilityId == facilityId && 
                                ts.Time == time && 
                                ts.IsAllTime &&
                                ts.ScheduleType == scheduleType);

        if (facilityOverride != null)
        {
            return facilityOverride.IsAvailable && !facilityOverride.IsBooked;
        }

        // Fall back to business profile template (lowest precedence)
        var businessTemplate = businessProfile.ScheduleTemplates
            .FirstOrDefault(st => st.Time == time && st.ScheduleType == scheduleType);

        if (businessTemplate != null)
        {
            return businessTemplate.IsAvailable; // Business templates are never booked
        }

        // Default: unavailable if no template found
        return false;
    }

    public List<ParentBusinessSearchResultDto> SearchParentBusinesses(string? query, string? city, bool? hasTpay, int limit)
    {
        var businessProfiles = _context.BusinessProfiles.AsQueryable();

        // Filter by search query (name, display name)
        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchTerm = query.ToLower().Trim();
            businessProfiles = businessProfiles.Where(bp =>
                bp.CompanyName.ToLower().Contains(searchTerm) ||
                bp.DisplayName.ToLower().Contains(searchTerm));
        }

        // Filter by city
        if (!string.IsNullOrWhiteSpace(city))
        {
            var cityTerm = city.ToLower().Trim();
            businessProfiles = businessProfiles.Where(bp =>
                bp.City.ToLower().Contains(cityTerm));
        }

        // Filter by TPay availability
        if (hasTpay.HasValue)
        {
            if (hasTpay.Value)
            {
                businessProfiles = businessProfiles.Where(bp =>
                    bp.TPayMerchantId != null && bp.TPayMerchantId != "");
            }
            else
            {
                businessProfiles = businessProfiles.Where(bp =>
                    bp.TPayMerchantId == null || bp.TPayMerchantId == "");
            }
        }

        return businessProfiles
            .OrderBy(bp => bp.DisplayName)
            .Take(limit)
            .Select(bp => new ParentBusinessSearchResultDto
            {
                Id = bp.Id,
                CompanyName = bp.CompanyName,
                DisplayName = bp.DisplayName,
                City = bp.City,
                AvatarUrl = bp.AvatarUrl,
                Nip = bp.Nip,
                HasTPay = !string.IsNullOrEmpty(bp.TPayMerchantId)
            })
            .ToList();
    }
}