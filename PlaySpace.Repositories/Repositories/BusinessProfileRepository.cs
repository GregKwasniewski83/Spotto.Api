using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;

namespace PlaySpace.Repositories.Repositories;

public class BusinessProfileRepository : IBusinessProfileRepository
{
    private readonly PlaySpaceDbContext _context;

    public BusinessProfileRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public BusinessProfile? GetBusinessProfileByUserId(Guid userId)
    {
        return _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .Include(bp => bp.ParentBusinessProfile)
            .FirstOrDefault(bp => bp.UserId == userId);
    }

    public BusinessProfile? GetBusinessProfileById(Guid businessProfileId)
    {
        return _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .Include(bp => bp.ParentBusinessProfile)
            .FirstOrDefault(bp => bp.Id == businessProfileId);
    }

    public async Task<BusinessProfile?> GetBusinessProfileByIdAsync(Guid businessProfileId)
    {
        return await _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .Include(bp => bp.ParentBusinessProfile)
            .FirstOrDefaultAsync(bp => bp.Id == businessProfileId);
    }

    public BusinessProfile CreateBusinessProfile(CreateBusinessProfileDto profileDto, Guid userId)
    {
        var businessProfile = new BusinessProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Nip = profileDto.Nip!, // NIP is validated in service (required for standalone, inherited from parent for child)
            CompanyName = profileDto.CompanyName,
            DisplayName = profileDto.DisplayName,
            Address = profileDto.Address,
            City = profileDto.City,
            PostalCode = profileDto.PostalCode,
            Latitude = profileDto.Latitude,
            Longitude = profileDto.Longitude,
            AvatarUrl = profileDto.AvatarUrl,
            
            // Facility plan URL from FTP upload or will be processed later for base64 data
            FacilityPlanUrl = profileDto.FacilityPlanUrl,
            FacilityPlanFileName = profileDto.FacilityPlanFileName,
            FacilityPlanFileType = profileDto.FacilityPlanFileType,
            
            // TPay registration fields
            Email = profileDto.Email,
            PhoneNumber = profileDto.PhoneNumber,
            PhoneCountry = profileDto.PhoneCountry,
            Regon = profileDto.Regon,
            Krs = profileDto.Krs,
            LegalForm = profileDto.LegalForm,
            CategoryId = profileDto.CategoryId,
            Mcc = profileDto.Mcc,
            Website = profileDto.Website,
            WebsiteDescription = profileDto.WebsiteDescription,
            ContactPersonName = profileDto.ContactPersonName,
            ContactPersonSurname = profileDto.ContactPersonSurname,

            // Note: Parent-child relationship is managed via BusinessParentChildAssociation table
            // ParentBusinessProfileId and permissions are set only when association is confirmed by parent

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.BusinessProfiles.Add(businessProfile);
        _context.SaveChanges();

        // Create schedule templates
        // Note: Using a simple Console.WriteLine since we don't have ILogger here
        Console.WriteLine($"[BusinessProfileRepository] Creating schedule templates for profile {businessProfile.Id}. Weekdays: {profileDto.WeekdaysSchedule.Count}, Saturday: {profileDto.SaturdaySchedule.Count}, Sunday: {profileDto.SundaySchedule.Count}");
        CreateScheduleTemplates(businessProfile.Id, profileDto);

        return GetBusinessProfileByUserId(userId)!;
    }

    public BusinessProfile? UpdateBusinessProfile(Guid businessProfileId, UpdateBusinessProfileDto profileDto)
    {
        var existingProfile = _context.BusinessProfiles
            .Include(bp => bp.ScheduleTemplates)
            .FirstOrDefault(bp => bp.Id == businessProfileId);

        if (existingProfile == null)
        {
            return null;
        }

        // Update basic info
        existingProfile.Nip = profileDto.Nip;
        existingProfile.CompanyName = profileDto.CompanyName;
        existingProfile.DisplayName = profileDto.DisplayName;
        existingProfile.Address = profileDto.Address;
        existingProfile.City = profileDto.City;
        existingProfile.PostalCode = profileDto.PostalCode;
        existingProfile.Latitude = profileDto.Latitude;
        existingProfile.Longitude = profileDto.Longitude;
        if (!string.IsNullOrEmpty(profileDto.AvatarUrl))
        {
            existingProfile.AvatarUrl = profileDto.AvatarUrl;
        }
        existingProfile.TermsAndConditionsUrl = profileDto.TermsAndConditionsUrl;

        // Facility plan file fields
        if (!string.IsNullOrEmpty(profileDto.FacilityPlanFileName))
        {
            existingProfile.FacilityPlanFileName = profileDto.FacilityPlanFileName;
            existingProfile.FacilityPlanFileType = profileDto.FacilityPlanFileType;
            existingProfile.FacilityPlanUrl = profileDto.FacilityPlanUrl;
        }
        
        // Update TPay registration fields
        existingProfile.Email = profileDto.Email;
        existingProfile.PhoneNumber = profileDto.PhoneNumber;
        existingProfile.PhoneCountry = profileDto.PhoneCountry;
        existingProfile.Regon = profileDto.Regon;
        existingProfile.Krs = profileDto.Krs;
        existingProfile.LegalForm = profileDto.LegalForm;
        existingProfile.CategoryId = profileDto.CategoryId;
        existingProfile.Mcc = profileDto.Mcc;
        existingProfile.Website = profileDto.Website;
        existingProfile.WebsiteDescription = profileDto.WebsiteDescription;
        existingProfile.ContactPersonName = profileDto.ContactPersonName;
        existingProfile.ContactPersonSurname = profileDto.ContactPersonSurname;

        // Note: Parent-child relationship permissions are managed via BusinessParentChildAssociation table

        existingProfile.UpdatedAt = DateTime.UtcNow;

        // Remove existing schedule templates
        _context.BusinessScheduleTemplates.RemoveRange(existingProfile.ScheduleTemplates);

        // Create new schedule templates
        CreateScheduleTemplates(existingProfile.Id, profileDto);

        _context.SaveChanges();
        return GetBusinessProfileById(businessProfileId)!;
    }

    public bool DeleteBusinessProfile(Guid businessProfileId)
    {
        var profile = _context.BusinessProfiles.FirstOrDefault(bp => bp.Id == businessProfileId);
        if (profile == null)
        {
            return false;
        }

        _context.BusinessProfiles.Remove(profile);
        _context.SaveChanges();
        return true;
    }

    public bool BusinessProfileExists(Guid userId)
    {
        return _context.BusinessProfiles.Any(bp => bp.UserId == userId);
    }

    public bool NipExists(string nip, Guid? excludeBusinessProfileId = null)
    {
        var query = _context.BusinessProfiles.Where(bp => bp.Nip == nip);

        if (excludeBusinessProfileId.HasValue)
        {
            // Exclude the profile being updated and any profiles in the same parent-child family
            // (parent and children share the same NIP by design)
            var profile = _context.BusinessProfiles
                .AsNoTracking()
                .FirstOrDefault(bp => bp.Id == excludeBusinessProfileId.Value);

            if (profile != null)
            {
                // Collect all IDs in this family
                var familyIds = new List<Guid> { profile.Id };

                if (profile.ParentBusinessProfileId.HasValue)
                {
                    // This is a child — exclude parent and all siblings
                    familyIds.Add(profile.ParentBusinessProfileId.Value);
                    familyIds.AddRange(_context.BusinessProfiles
                        .Where(bp => bp.ParentBusinessProfileId == profile.ParentBusinessProfileId)
                        .Select(bp => bp.Id));
                }
                else
                {
                    // This is a parent — exclude all children
                    familyIds.AddRange(_context.BusinessProfiles
                        .Where(bp => bp.ParentBusinessProfileId == profile.Id)
                        .Select(bp => bp.Id));
                }

                query = query.Where(bp => !familyIds.Contains(bp.Id));
            }
        }

        return query.Any();
    }

    public async Task UpdateAvatarAsync(Guid businessProfileId, string avatarUrl)
    {
        var profile = _context.BusinessProfiles.FirstOrDefault(bp => bp.Id == businessProfileId);
        if (profile != null)
        {
            profile.AvatarUrl = avatarUrl;
            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    private void CreateScheduleTemplates(Guid businessProfileId, CreateBusinessProfileDto profileDto)
    {
        var templates = new List<BusinessScheduleTemplate>();
        Console.WriteLine($"[CreateScheduleTemplates] Processing schedules for profile {businessProfileId}. Input data - Weekdays: {profileDto.WeekdaysSchedule.Count}, Saturday: {profileDto.SaturdaySchedule.Count}, Sunday: {profileDto.SundaySchedule.Count}");

        // Weekdays schedule
        foreach (var slot in profileDto.WeekdaysSchedule)
        {
            templates.Add(new BusinessScheduleTemplate
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = businessProfileId,
                ScheduleType = ScheduleType.Weekdays,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Saturday schedule
        foreach (var slot in profileDto.SaturdaySchedule)
        {
            templates.Add(new BusinessScheduleTemplate
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = businessProfileId,
                ScheduleType = ScheduleType.Saturday,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Sunday schedule
        foreach (var slot in profileDto.SundaySchedule)
        {
            templates.Add(new BusinessScheduleTemplate
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = businessProfileId,
                ScheduleType = ScheduleType.Sunday,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        Console.WriteLine($"[CreateScheduleTemplates] Created {templates.Count} schedule templates for profile {businessProfileId}. Adding to database...");
        _context.BusinessScheduleTemplates.AddRange(templates);
        _context.SaveChanges();
        Console.WriteLine($"[CreateScheduleTemplates] Successfully saved {templates.Count} schedule templates to database for profile {businessProfileId}");
    }

    private void CreateScheduleTemplates(Guid businessProfileId, UpdateBusinessProfileDto profileDto)
    {
        var templates = new List<BusinessScheduleTemplate>();

        // Weekdays schedule
        foreach (var slot in profileDto.WeekdaysSchedule)
        {
            templates.Add(new BusinessScheduleTemplate
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = businessProfileId,
                ScheduleType = ScheduleType.Weekdays,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Saturday schedule
        foreach (var slot in profileDto.SaturdaySchedule)
        {
            templates.Add(new BusinessScheduleTemplate
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = businessProfileId,
                ScheduleType = ScheduleType.Saturday,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        // Sunday schedule
        foreach (var slot in profileDto.SundaySchedule)
        {
            templates.Add(new BusinessScheduleTemplate
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = businessProfileId,
                ScheduleType = ScheduleType.Sunday,
                Time = slot.Time,
                IsAvailable = slot.IsAvailable,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        _context.BusinessScheduleTemplates.AddRange(templates);
        _context.SaveChanges();
    }

    public void UpdateTPayMerchantData(Guid businessProfileId, string merchantId, string accountId, string? posId, string? activationLink, int verificationStatus)
    {
        var profile = _context.BusinessProfiles.FirstOrDefault(bp => bp.Id == businessProfileId);
        if (profile == null)
        {
            throw new InvalidOperationException("Business profile not found");
        }

        profile.TPayMerchantId = merchantId;
        profile.TPayAccountId = accountId;
        profile.TPayPosId = posId;
        profile.TPayActivationLink = activationLink;
        profile.TPayVerificationStatus = verificationStatus;
        profile.TPayRegisteredAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        _context.SaveChanges();
    }

    // KSeF management methods
    public async Task<bool> UpdateKSeFCredentialsAsync(Guid businessProfileId, string token, string environment)
    {
        var profile = await _context.BusinessProfiles.FindAsync(businessProfileId);
        if (profile == null)
        {
            return false;
        }

        profile.KSeFToken = token;
        profile.KSeFEnvironment = environment;
        profile.KSeFRegisteredAt = DateTime.UtcNow;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateKSeFStatusAsync(Guid businessProfileId, bool enabled)
    {
        var profile = await _context.BusinessProfiles.FindAsync(businessProfileId);
        if (profile == null)
        {
            return false;
        }

        profile.KSeFEnabled = enabled;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<BusinessProfile?> GetBusinessProfileWithKSeFAsync(Guid businessProfileId)
    {
        return await _context.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == businessProfileId);
    }

    public async Task UpdateKSeFLastSyncAsync(Guid businessProfileId)
    {
        var profile = await _context.BusinessProfiles.FindAsync(businessProfileId);
        if (profile != null)
        {
            profile.KSeFLastSyncAt = DateTime.UtcNow;
            profile.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    // Parent-child relationship management
    public async Task<bool> UpdateParentChildRelationshipAsync(Guid childBusinessProfileId, Guid? parentBusinessProfileId, bool useParentTPay, bool useParentNipForInvoices)
    {
        var profile = await _context.BusinessProfiles.FindAsync(childBusinessProfileId);
        if (profile == null)
        {
            return false;
        }

        profile.ParentBusinessProfileId = parentBusinessProfileId;
        profile.UseParentTPay = useParentTPay;
        profile.UseParentNipForInvoices = useParentNipForInvoices;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ClearParentChildRelationshipAsync(Guid childBusinessProfileId)
    {
        var profile = await _context.BusinessProfiles.FindAsync(childBusinessProfileId);
        if (profile == null)
        {
            return false;
        }

        profile.ParentBusinessProfileId = null;
        profile.UseParentTPay = false;
        profile.UseParentNipForInvoices = false;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}