using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using PlaySpace.Domain.Exceptions;

namespace PlaySpace.Services.Services;

public class BusinessProfileService : IBusinessProfileService
{
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IBusinessDateAvailabilityRepository _businessDateAvailabilityRepository;
    private readonly ITPayService _tpayService;
    private readonly ITPayDictionaryRepository _dictionaryRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<BusinessProfileService> _logger;
    private readonly TPayConfiguration _tpayConfig;
    private readonly IFacilityRepository _facilityRepository;
    private readonly ITimeSlotRepository _timeSlotRepository;
    private readonly IFtpStorageService _ftpStorageService;
    private readonly IKSeFApiService _ksefApiService;
    private readonly IBusinessParentChildAssociationService _parentChildAssociationService;

    public BusinessProfileService(IBusinessProfileRepository businessProfileRepository, IBusinessDateAvailabilityRepository businessDateAvailabilityRepository, ITPayService tpayService, ITPayDictionaryRepository dictionaryRepository, IEmailService emailService, ILogger<BusinessProfileService> logger, IOptions<TPayConfiguration> tpayConfig, IFacilityRepository facilityRepository, ITimeSlotRepository timeSlotRepository, IFtpStorageService ftpStorageService, IKSeFApiService ksefApiService, IBusinessParentChildAssociationService parentChildAssociationService)
    {
        _businessProfileRepository = businessProfileRepository;
        _businessDateAvailabilityRepository = businessDateAvailabilityRepository;
        _tpayService = tpayService;
        _ksefApiService = ksefApiService;
        _dictionaryRepository = dictionaryRepository;
        _emailService = emailService;
        _logger = logger;
        _tpayConfig = tpayConfig.Value;
        _facilityRepository = facilityRepository;
        _timeSlotRepository = timeSlotRepository;
        _ftpStorageService = ftpStorageService;
        _parentChildAssociationService = parentChildAssociationService;
    }

    public BusinessProfileDto? GetBusinessProfileByUserId(Guid userId)
    {
        var profile = _businessProfileRepository.GetBusinessProfileByUserId(userId);
        return profile == null ? null : MapToDto(profile);
    }

    public BusinessProfileDto? GetBusinessProfileById(Guid businessProfileId)
    {
        var profile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        return profile == null ? null : MapToDto(profile);
    }

    public async Task<BusinessProfileDto> CreateBusinessProfile(CreateBusinessProfileDto profileDto, Guid userId)
    {
        _logger.LogInformation("Starting CreateBusinessProfile for user {UserId}, CompanyName: {CompanyName}, HasFacilityPlan: {HasFacilityPlan}", 
            userId, profileDto.CompanyName, !string.IsNullOrEmpty(profileDto.FacilityPlanUrl));
        _logger.LogInformation("Schedule data received - WeekdaysSchedule: {WeekdaysCount} slots, SaturdaySchedule: {SaturdayCount} slots, SundaySchedule: {SundayCount} slots, DateSpecificAvailability: {DateSpecificCount} entries",
            profileDto.WeekdaysSchedule.Count, profileDto.SaturdaySchedule.Count, profileDto.SundaySchedule.Count, profileDto.DateSpecificAvailability.Count);

        // Check if business profile already exists
        _logger.LogInformation("Checking if business profile already exists for user {UserId}", userId);
        if (_businessProfileRepository.BusinessProfileExists(userId))
        {
            throw new InvalidOperationException("Business profile already exists for this user");
        }

        // Validate parent business profile if specified (association request will be created after profile creation)
        BusinessProfile? parentProfile = null;
        if (profileDto.ParentBusinessProfileId.HasValue)
        {
            parentProfile = _businessProfileRepository.GetBusinessProfileById(profileDto.ParentBusinessProfileId.Value);
            if (parentProfile == null)
            {
                throw new InvalidOperationException("Parent business profile not found");
            }

            _logger.LogInformation("Parent business profile validated. ParentId: {ParentId}. Association request will be created after profile creation.",
                profileDto.ParentBusinessProfileId);
        }

        // NIP validation: required for standalone profiles, optional for child profiles (will use parent's NIP)
        if (string.IsNullOrWhiteSpace(profileDto.Nip))
        {
            if (parentProfile == null)
            {
                throw new InvalidOperationException("NIP is required when creating a standalone business profile");
            }

            // Child profile without NIP - will use parent's NIP
            _logger.LogInformation("Child profile will use parent's NIP: {ParentNip}", parentProfile.Nip);
            profileDto.Nip = parentProfile.Nip;
        }
        else
        {
            // Check if NIP already exists (only if NIP was provided, not inherited from parent)
            _logger.LogInformation("Checking if NIP {Nip} already exists", profileDto.Nip);
            if (_businessProfileRepository.NipExists(profileDto.Nip))
            {
                // Allow same NIP if it belongs to the parent (child using parent's NIP)
                if (parentProfile == null || parentProfile.Nip != profileDto.Nip)
                {
                    throw new InvalidOperationException("A business profile with this NIP already exists");
                }
                _logger.LogInformation("NIP {Nip} belongs to parent profile, allowing for child profile", profileDto.Nip);
            }
        }

        // Try TPay registration FIRST if requested and required fields are provided
        // Skip TPay auto-registration if parent business profile is specified (child will use parent's TPay)
        TPayBusinessRegistrationResponse? tpayResponse = null;
        var shouldRegisterWithTPay = profileDto.AutoRegisterWithTPay && !profileDto.ParentBusinessProfileId.HasValue;

        _logger.LogInformation("Checking TPay registration requirements for user {UserId}. AutoRegisterWithTPay: {AutoRegisterWithTPay}, ParentBusinessProfileId: {ParentProfileId}, ShouldRegister: {ShouldRegister}",
            userId, profileDto.AutoRegisterWithTPay, profileDto.ParentBusinessProfileId, shouldRegisterWithTPay);

        if (profileDto.ParentBusinessProfileId.HasValue && profileDto.AutoRegisterWithTPay)
        {
            _logger.LogInformation("Skipping TPay auto-registration for user {UserId} because parent business profile is specified. Child will use parent's TPay integration after association is confirmed.", userId);
        }

        if (shouldRegisterWithTPay)
        {
            _logger.LogInformation("TPay auto-registration requested. Checking if can register with TPay for user {UserId}...", userId);
            var canRegister = await CanRegisterWithTPay(profileDto);
            _logger.LogInformation("CanRegisterWithTPay result for user {UserId}: {CanRegister}", userId, canRegister);

            if (canRegister)
            {
                _logger.LogInformation("Attempting TPay registration for user {UserId} BEFORE profile creation", userId);

                var tpayRequest = BuildTPayRegistrationRequest(profileDto);

                // Log phone numbers for debugging
                _logger.LogDebug("TPay registration phone numbers - Main: {MainPhone}, Address: {AddressPhone}",
                    tpayRequest.phone?.phoneNumber,
                    tpayRequest.address?.FirstOrDefault()?.phone);

                // Log complete TPay registration request for debugging
                _logger.LogInformation("BusinessProfile sending TPay business registration request: {TPayRequest}",
                    System.Text.Json.JsonSerializer.Serialize(tpayRequest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                try
                {
                    _logger.LogInformation("Calling TPay RegisterBusinessAsync for user {UserId}...", userId);

                    // Let TPay exceptions bubble up - this will prevent profile creation if TPay fails
                    tpayResponse = await _tpayService.RegisterBusinessAsync(tpayRequest);

                    _logger.LogInformation("TPay registration successful for user {UserId}. Merchant ID: {MerchantId}, Verification Status: {VerificationStatus}",
                        userId, tpayResponse.id, tpayResponse.verificationStatus);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "TPay registration failed for user {UserId}. Error: {ErrorMessage}, InnerException: {InnerException}",
                        userId, ex.Message, ex.InnerException?.Message);

                    // Re-throw the exception to prevent profile creation if TPay fails
                    throw;
                }
            }
            else
            {
                _logger.LogWarning("TPay auto-registration was requested but validation failed for user {UserId}. Missing required fields or invalid dictionary values", userId);
                throw new InvalidOperationException("TPay auto-registration was requested but required fields are missing or invalid");
            }
        }

        // Process facility plan file if provided
        if (!string.IsNullOrEmpty(profileDto.FacilityPlanUrl) && profileDto.FacilityPlanUrl.StartsWith("data:"))
        {
            _logger.LogInformation("Processing facility plan file (base64 data) for user {UserId}", userId);
            var facilityPlanUrl = await ProcessFacilityPlanFile(profileDto.FacilityPlanUrl, profileDto.FacilityPlanFileName);
            _logger.LogInformation("Facility plan file processed successfully. New URL: {FacilityPlanUrl}", facilityPlanUrl);
            // Update the DTO with processed file URL
            profileDto.FacilityPlanUrl = facilityPlanUrl;
        }
        else if (!string.IsNullOrEmpty(profileDto.FacilityPlanUrl))
        {
            _logger.LogInformation("Facility plan URL already processed: {FacilityPlanUrl}", profileDto.FacilityPlanUrl);
        }

        // Create business profile only AFTER TPay registration succeeds (or if TPay not requested)
        _logger.LogInformation("Creating business profile in repository for user {UserId}", userId);
        var profile = _businessProfileRepository.CreateBusinessProfile(profileDto, userId);
        _logger.LogInformation("Business profile created in repository with ID: {ProfileId}", profile.Id);
        
        // Update the facility plan URL in the database if it was processed
        if (!string.IsNullOrEmpty(profileDto.FacilityPlanUrl) && !profileDto.FacilityPlanUrl.StartsWith("data:"))
        {
            _logger.LogInformation("Updating facility plan URL in profile for user {UserId}, URL: {FacilityPlanUrl}", userId, profileDto.FacilityPlanUrl);
            profile.FacilityPlanUrl = profileDto.FacilityPlanUrl;
            // Save changes through repository
            _logger.LogInformation("Calling UpdateBusinessProfile to save facility plan URL for profile {ProfileId}", profile.Id);
            _businessProfileRepository.UpdateBusinessProfile(profile.Id, new UpdateBusinessProfileDto 
            { 
                Nip = profile.Nip,
                CompanyName = profile.CompanyName,
                DisplayName = profile.DisplayName,
                Address = profile.Address,
                City = profile.City,
                PostalCode = profile.PostalCode,
                Latitude = profile.Latitude,
                Longitude = profile.Longitude,
                FacilityPlanUrl = profile.FacilityPlanUrl,
                FacilityPlanFileName = profile.FacilityPlanFileName,
                FacilityPlanFileType = profile.FacilityPlanFileType,
                Email = profile.Email,
                PhoneNumber = profile.PhoneNumber,
                PhoneCountry = profile.PhoneCountry,
                Regon = profile.Regon,
                Krs = profile.Krs,
                LegalForm = profile.LegalForm,
                CategoryId = profile.CategoryId,
                Mcc = profile.Mcc,
                Website = profile.Website,
                WebsiteDescription = profile.WebsiteDescription,
                ContactPersonName = profile.ContactPersonName,
                ContactPersonSurname = profile.ContactPersonSurname,
                WeekdaysSchedule = profileDto.WeekdaysSchedule,
                SaturdaySchedule = profileDto.SaturdaySchedule,
                SundaySchedule = profileDto.SundaySchedule,
                DateSpecificAvailability = profileDto.DateSpecificAvailability
            });
            _logger.LogInformation("Facility plan URL update completed for user {UserId}", userId);
        }
        
        // Handle date-specific availability
        _logger.LogInformation("Handling date-specific availability for profile {ProfileId}", profile.Id);
        HandleDateSpecificAvailability(profile.Id, profileDto.DateSpecificAvailability);
        _logger.LogInformation("Date-specific availability handling completed for profile {ProfileId}", profile.Id);
        
        // Update profile with TPay data if registration was successful
        if (tpayResponse != null)
        {
            // Extract POS ID from the first website if available
            var posId = tpayResponse.website?.FirstOrDefault()?.posId;

            _businessProfileRepository.UpdateTPayMerchantData(
                profile.Id,
                tpayResponse.id,
                tpayResponse.id,
                posId,
                tpayResponse.activationLink,
                tpayResponse.verificationStatus);

            // Get the updated profile with TPay data
            profile = _businessProfileRepository.GetBusinessProfileByUserId(userId)!;
            
            _logger.LogInformation("Business profile created successfully with TPay integration. Profile ID: {ProfileId}, Merchant ID: {MerchantId}", 
                profile.Id, tpayResponse.id);
        }
        else
        {
            _logger.LogInformation("Business profile created successfully without TPay integration. Profile ID: {ProfileId}", profile.Id);
        }
        
        _logger.LogInformation("Mapping profile to DTO for user {UserId}", userId);
        var businessProfileDto = MapToDto(profile);
        _logger.LogInformation("Profile mapped to DTO successfully. Final DTO has FacilityPlanUrl: {FacilityPlanUrl}", businessProfileDto.FacilityPlanUrl);
        
        // Send email notification (async, don't wait) - capture variables for lambda
        _logger.LogInformation("Starting email notification process for user {UserId}", userId);
        var emailToSend = businessProfileDto.Email;
        var profileDtoForEmail = businessProfileDto;
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendBusinessProfileCreatedEmailAsync(profileDtoForEmail);
                _logger.LogInformation("Business profile creation email sent successfully to {Email}", emailToSend);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send business profile creation email to {Email}: {Error}", emailToSend, ex.Message);
            }
        });

        // Create parent-child association request if parent was specified
        if (profileDto.ParentBusinessProfileId.HasValue)
        {
            try
            {
                _logger.LogInformation("Creating parent-child association request for child {ChildProfileId} with parent {ParentProfileId}",
                    profile.Id, profileDto.ParentBusinessProfileId.Value);

                await _parentChildAssociationService.RequestAssociationAsync(
                    profile.Id,
                    profileDto.ParentBusinessProfileId.Value);

                _logger.LogInformation("Parent-child association request created successfully. Confirmation email sent to parent.");
            }
            catch (Exception ex)
            {
                // Log but don't fail the profile creation - association can be requested later
                _logger.LogWarning(ex, "Failed to create parent-child association request for child {ChildProfileId} with parent {ParentProfileId}: {Error}",
                    profile.Id, profileDto.ParentBusinessProfileId.Value, ex.Message);
            }
        }

        _logger.LogInformation("CreateBusinessProfile completed successfully for user {UserId}, returning DTO with ID: {ProfileId}", userId, businessProfileDto.Id);
        return businessProfileDto;
    }

    public async Task<BusinessProfileDto?> UpdateBusinessProfile(Guid businessProfileId, UpdateBusinessProfileDto profileDto)
    {
        // Check if NIP already exists for another business profile
        if (_businessProfileRepository.NipExists(profileDto.Nip, businessProfileId))
        {
            throw new InvalidOperationException("A business profile with this NIP already exists");
        }

        // Process facility plan file if provided
        if (!string.IsNullOrEmpty(profileDto.FacilityPlanUrl) && profileDto.FacilityPlanUrl.StartsWith("data:"))
        {
            var facilityPlanUrl = await ProcessFacilityPlanFile(profileDto.FacilityPlanUrl, profileDto.FacilityPlanFileName);
            profileDto.FacilityPlanUrl = facilityPlanUrl;
        }

        var profile = _businessProfileRepository.UpdateBusinessProfile(businessProfileId, profileDto);
        if (profile == null)
        {
            return null;
        }
        
        // Handle date-specific availability
        HandleDateSpecificAvailability(profile.Id, profileDto.DateSpecificAvailability);
        
        return MapToDto(profile);
    }

    public bool DeleteBusinessProfile(Guid businessProfileId)
    {
        return _businessProfileRepository.DeleteBusinessProfile(businessProfileId);
    }

    public List<ScheduleSlotDto> GetScheduleForDate(Guid businessProfileId, DateTime date)
    {
        var profile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (profile == null)
        {
            return new List<ScheduleSlotDto>();
        }

        var scheduleType = GetScheduleTypeForDate(date);
        var templates = profile.ScheduleTemplates
            .Where(st => st.ScheduleType == scheduleType)
            .OrderBy(st => st.Time)
            .ToList();

        return templates.Select(t => new ScheduleSlotDto
        {
            Id = t.Time,
            Time = t.Time,
            IsAvailable = t.IsAvailable,
            IsBooked = false // Schedule templates don't have booking info
        }).ToList();
    }

    public async Task<string> UploadAvatarAsync(Guid businessProfileId, IFormFile avatar)
    {
        // Check if business profile exists
        var profile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (profile == null)
        {
            throw new InvalidOperationException("Business profile not found");
        }

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(profile.AvatarUrl))
        {
            // Extract the file path from the URL (e.g., "/uploads/avatars/file.jpg" -> "uploads/avatars/file.jpg")
            var oldFilePath = profile.AvatarUrl.TrimStart('/');
            await _ftpStorageService.DeleteFileAsync(oldFilePath);
        }

        // Generate unique filename
        var fileExtension = Path.GetExtension(avatar.FileName);
        var fileName = $"{profile.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}{fileExtension}";

        // Upload to FTP server
        using (var stream = avatar.OpenReadStream())
        {
            var avatarUrl = await _ftpStorageService.UploadFileAsync(stream, "avatars", fileName);
            await _businessProfileRepository.UpdateAvatarAsync(businessProfileId, avatarUrl);
            return avatarUrl;
        }
    }

    private BusinessProfileDto MapToDto(BusinessProfile profile)
    {
        return new BusinessProfileDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            Nip = profile.Nip,
            CompanyName = profile.CompanyName,
            DisplayName = profile.DisplayName,
            Address = profile.Address,
            City = profile.City,
            PostalCode = profile.PostalCode,
            Latitude = profile.Latitude,
            Longitude = profile.Longitude,
            AvatarUrl = profile.AvatarUrl,
            TermsAndConditionsUrl = profile.TermsAndConditionsUrl,

            // Facility plan file
            FacilityPlanUrl = profile.FacilityPlanUrl,
            FacilityPlanFileName = profile.FacilityPlanFileName,
            FacilityPlanFileType = profile.FacilityPlanFileType,
            WeekdaysSchedule = profile.ScheduleTemplates
                .Where(st => st.ScheduleType == ScheduleType.Weekdays)
                .Select(MapScheduleToDto)
                .OrderBy(s => s.Time)
                .ToList(),
            SaturdaySchedule = profile.ScheduleTemplates
                .Where(st => st.ScheduleType == ScheduleType.Saturday)
                .Select(MapScheduleToDto)
                .OrderBy(s => s.Time)
                .ToList(),
            SundaySchedule = profile.ScheduleTemplates
                .Where(st => st.ScheduleType == ScheduleType.Sunday)
                .Select(MapScheduleToDto)
                .OrderBy(s => s.Time)
                .ToList(),
            DateSpecificAvailability = GetDateSpecificAvailabilityForProfile(profile.Id),
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt,
            
            // TPay registration fields
            Email = profile.Email,
            PhoneNumber = profile.PhoneNumber,
            PhoneCountry = profile.PhoneCountry,
            Regon = profile.Regon,
            Krs = profile.Krs,
            LegalForm = profile.LegalForm,
            CategoryId = profile.CategoryId,
            Mcc = profile.Mcc,
            Website = profile.Website,
            WebsiteDescription = profile.WebsiteDescription,
            ContactPersonName = profile.ContactPersonName,
            ContactPersonSurname = profile.ContactPersonSurname,
            
            // TPay merchant data
            TPayMerchantId = profile.TPayMerchantId,
            TPayAccountId = profile.TPayAccountId,
            TPayPosId = profile.TPayPosId,
            TPayActivationLink = profile.TPayActivationLink,
            TPayVerificationStatus = profile.TPayVerificationStatus,
            TPayRegisteredAt = profile.TPayRegisteredAt,

            // KSeF fields
            KSeFEnabled = profile.KSeFEnabled,
            KSeFTokenConfigured = !string.IsNullOrEmpty(profile.KSeFToken),
            KSeFEnvironment = profile.KSeFEnvironment,
            KSeFRegisteredAt = profile.KSeFRegisteredAt,
            KSeFLastSyncAt = profile.KSeFLastSyncAt,

            // Parent-child relationship
            ParentBusinessProfileId = profile.ParentBusinessProfileId,
            ParentBusinessProfileName = profile.ParentBusinessProfile?.DisplayName,
            UseParentTPay = profile.UseParentTPay,
            UseParentNipForInvoices = profile.UseParentNipForInvoices,

            // Effective TPay/Invoice info (resolved from parent if applicable)
            EffectiveTPayMerchantId = GetEffectiveTPayMerchantId(profile),
            EffectiveNipForInvoices = GetEffectiveNipForInvoices(profile),
            EffectiveCompanyNameForInvoices = GetEffectiveCompanyNameForInvoices(profile)
        };
    }

    /// <summary>
    /// Gets the effective TPay MerchantId - from parent if UseParentTPay is true, otherwise own
    /// </summary>
    private string? GetEffectiveTPayMerchantId(BusinessProfile profile)
    {
        if (profile.UseParentTPay && profile.ParentBusinessProfile != null)
        {
            return profile.ParentBusinessProfile.TPayMerchantId;
        }
        return profile.TPayMerchantId;
    }

    /// <summary>
    /// Gets the effective NIP for invoices - from parent if UseParentNipForInvoices is true, otherwise own
    /// </summary>
    private string? GetEffectiveNipForInvoices(BusinessProfile profile)
    {
        if (profile.UseParentNipForInvoices && profile.ParentBusinessProfile != null)
        {
            return profile.ParentBusinessProfile.Nip;
        }
        return profile.Nip;
    }

    /// <summary>
    /// Gets the effective company name for invoices - from parent if UseParentNipForInvoices is true, otherwise own
    /// </summary>
    private string? GetEffectiveCompanyNameForInvoices(BusinessProfile profile)
    {
        if (profile.UseParentNipForInvoices && profile.ParentBusinessProfile != null)
        {
            return profile.ParentBusinessProfile.CompanyName;
        }
        return profile.CompanyName;
    }

    private ScheduleSlotDto MapScheduleToDto(BusinessScheduleTemplate template)
    {
        return new ScheduleSlotDto
        {
            Id = template.Time,
            Time = template.Time,
            IsAvailable = template.IsAvailable,
            IsBooked = false // Templates don't have booking status
        };
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

    public BusinessDateAvailabilityDto? GetDateAvailability(Guid businessProfileId, DateTime date)
    {
        var profile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (profile == null)
        {
            return null;
        }

        return _businessDateAvailabilityRepository.GetBusinessDateAvailability(profile.Id, date);
    }


    private void HandleDateSpecificAvailability(Guid businessProfileId, Dictionary<string, List<BusinessDateAvailabilitySlotDto>> dateSpecificAvailability)
    {
        if (dateSpecificAvailability == null || !dateSpecificAvailability.Any())
        {
            return;
        }

        foreach (var dateEntry in dateSpecificAvailability)
        {
            if (!DateTime.TryParse(dateEntry.Key, out var date))
            {
                continue; // Skip invalid dates
            }

            var createDto = new CreateBusinessDateAvailabilityDto
            {
                Date = date,
                TimeSlots = dateEntry.Value
            };

            _businessDateAvailabilityRepository.CreateBusinessDateAvailability(businessProfileId, createDto);
        }
    }

    private Dictionary<string, List<BusinessDateAvailabilitySlotDto>> GetDateSpecificAvailabilityForProfile(Guid businessProfileId)
    {
        var result = new Dictionary<string, List<BusinessDateAvailabilitySlotDto>>();
        
        // Get date-specific availability for a reasonable time range (e.g., next 90 days)
        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(90);
        
        var availabilities = _businessDateAvailabilityRepository.GetBusinessDateAvailabilities(businessProfileId, startDate, endDate);
        
        var groupedByDate = availabilities.GroupBy(a => a.Date.ToString("yyyy-MM-dd"));
        
        foreach (var group in groupedByDate)
        {
            result[group.Key] = group.Select(a => new BusinessDateAvailabilitySlotDto
            {
                Time = a.Time,
                IsAvailable = a.IsAvailable,
                IsFromTemplate = false // These are all date-specific overrides
            })
            .OrderBy(s => s.Time)
            .ToList();
        }
        
        return result;
    }

    public async Task<BusinessProfileDto> RegisterWithTPayAsync(Guid businessProfileId, TPayBusinessRegistrationRequest request)
    {
        _logger.LogInformation("Manual TPay registration requested for business profile {BusinessProfileId}", businessProfileId);

        var profile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (profile == null)
        {
            _logger.LogWarning("TPay registration failed: Business profile not found {BusinessProfileId}", businessProfileId);
            throw new NotFoundException("Business profile not found");
        }

        if (!string.IsNullOrEmpty(profile.TPayMerchantId))
        {
            _logger.LogWarning("TPay registration failed: Business profile {BusinessProfileId} is already registered with TPay (Merchant ID: {MerchantId})",
                profile.Id, profile.TPayMerchantId);
            throw new InvalidOperationException("Business is already registered with TPay");
        }

        try
        {
            _logger.LogInformation("Calling TPay registration API for business profile {BusinessProfileId}", profile.Id);
            var response = await _tpayService.RegisterBusinessAsync(request);

            _logger.LogInformation("TPay registration API call successful for business profile {BusinessProfileId}. Merchant ID: {MerchantId}",
                profile.Id, response.id);

            return await UpdateTPayMerchantDataAsync(businessProfileId, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual TPay registration failed for business profile {BusinessProfileId}. Error: {ErrorMessage}", 
                profile.Id, ex.Message);
            throw;
        }
    }

    public async Task<BusinessProfileDto> UpdateTPayMerchantDataAsync(Guid businessProfileId, TPayBusinessRegistrationResponse response)
    {
        _logger.LogInformation("Updating TPay merchant data for business profile {BusinessProfileId} with merchant ID {MerchantId}", businessProfileId, response.id);

        var profile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (profile == null)
        {
            _logger.LogWarning("Cannot update TPay merchant data: Business profile not found {BusinessProfileId}", businessProfileId);
            throw new NotFoundException("Business profile not found");
        }

        try
        {
            // Extract POS ID from the first website if available
            var posId = response.website?.FirstOrDefault()?.posId;

            _businessProfileRepository.UpdateTPayMerchantData(
                businessProfileId,
                response.id,
                response.id, // The response.id is the account ID
                posId,
                response.activationLink,
                response.verificationStatus);

            _logger.LogInformation("TPay merchant data updated successfully for business profile {BusinessProfileId}. Merchant ID: {MerchantId}, POS ID: {PosId}, Verification Status: {VerificationStatus}",
                profile.Id, response.id, posId, response.verificationStatus);

            // Get the updated profile
            var updatedProfile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
            return MapToDto(updatedProfile!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update TPay merchant data for business profile {BusinessProfileId}. Error: {ErrorMessage}", 
                profile.Id, ex.Message);
            throw;
        }
    }

    private async Task<bool> CanRegisterWithTPay(CreateBusinessProfileDto profileDto)
    {
        // Check if all required TPay fields are provided
        if (string.IsNullOrEmpty(profileDto.Email) ||
            string.IsNullOrEmpty(profileDto.PhoneNumber) ||
            string.IsNullOrEmpty(profileDto.Nip))
        {
            return false;
        }

        // Validate legal form if provided
        if (profileDto.LegalForm.HasValue)
        {
            var legalForm = await _dictionaryRepository.GetLegalFormByIdAsync(profileDto.LegalForm.Value);
            if (legalForm == null || !legalForm.IsActive)
            {
                return false;
            }
        }

        // Validate category if provided
        if (profileDto.CategoryId.HasValue)
        {
            var category = await _dictionaryRepository.GetCategoryByIdAsync(profileDto.CategoryId.Value);
            if (category == null || !category.IsActive)
            {
                return false;
            }
        }

        return true;
    }

    private TPayBusinessRegistrationRequest BuildTPayRegistrationRequest(CreateBusinessProfileDto profileDto)
    {
        var request = new TPayBusinessRegistrationRequest
        {
            offerCode = _tpayConfig.OfferCode,
            email = profileDto.Email!,
            phone = new BusinessPhone
            {
                phoneNumber = FormatPhoneNumber(profileDto.PhoneNumber!),
                phoneCountry = profileDto.PhoneCountry ?? "PL"
            },
            taxId = FormatNip(profileDto.Nip),
            regon = FormatRegon(profileDto.Regon ?? ""),
            krs = FormatKrs(profileDto.Krs ?? ""),
            legalForm = profileDto.LegalForm ?? 1,
            categoryId = profileDto.CategoryId ?? 78,
            mcc = profileDto.Mcc ?? "5722",
            merchantApiConsent = false,
            website = new List<BusinessWebsite>(),
            address = new List<BusinessAddress>
            {
                new BusinessAddress
                {
                    friendlyName = "Adres główny",
                    name = profileDto.CompanyName,
                    street = profileDto.Address,
                    houseNumber = "1", // Default - could be extracted from address
                    roomNumber = "",
                    postalCode = profileDto.PostalCode,
                    city = profileDto.City,
                    country = "PL",
                    phone = FormatPhoneNumber(profileDto.PhoneNumber!),
                    isMain = true,
                    isCorrespondence = true,
                    isInvoice = true
                }
            },
            person = new List<BusinessPerson>()
        };

        // Add website if provided
        if (!string.IsNullOrEmpty(profileDto.Website))
        {
            request.website.Add(new BusinessWebsite
            {
                name = profileDto.DisplayName,
                friendlyName = profileDto.DisplayName,
                description = profileDto.WebsiteDescription ?? "Usługi sportowe",
                url = profileDto.Website
            });
        }

        // Add contact person with fallbacks (always included)
        request.person.Add(new BusinessPerson
        {
            name = profileDto.ContactPersonName ?? profileDto.DisplayName.Split(' ').FirstOrDefault() ?? profileDto.DisplayName,
            surname = profileDto.ContactPersonSurname ?? (profileDto.DisplayName.Split(' ').Length > 1 ? string.Join(" ", profileDto.DisplayName.Split(' ').Skip(1)) : ""),
            isRepresentative = true,
            isContactPerson = true,
            contact = new List<PersonContact>
            {
                new PersonContact
                {
                    type = 1, // Email type
                    contact = profileDto.Email!
                }
            }
        });

        return request;
    }

    private TPayBusinessRegistrationRequest BuildTPayRegistrationRequest(BusinessProfile profile)
    {
        var request = new TPayBusinessRegistrationRequest
        {
            offerCode = _tpayConfig.OfferCode,
            email = profile.Email!,
            phone = new BusinessPhone
            {
                phoneNumber = FormatPhoneNumber(profile.PhoneNumber!),
                phoneCountry = profile.PhoneCountry ?? "PL"
            },
            taxId = FormatNip(profile.Nip),
            regon = FormatRegon(profile.Regon ?? ""),
            krs = FormatKrs(profile.Krs ?? ""),
            legalForm = profile.LegalForm ?? 1,
            categoryId = profile.CategoryId ?? 78,
            mcc = profile.Mcc ?? "5722",
            merchantApiConsent = false,
            website = new List<BusinessWebsite>(),
            address = new List<BusinessAddress>
            {
                new BusinessAddress
                {
                    friendlyName = "Adres główny",
                    name = profile.CompanyName,
                    street = profile.Address,
                    houseNumber = "1", // Default - could be extracted from address
                    roomNumber = "",
                    postalCode = profile.PostalCode,
                    city = profile.City,
                    country = "PL",
                    phone = FormatPhoneNumber(profile.PhoneNumber!),
                    isMain = true,
                    isCorrespondence = true,
                    isInvoice = true
                }
            },
            person = new List<BusinessPerson>()
        };

        // Add website if provided
        if (!string.IsNullOrEmpty(profile.Website))
        {
            request.website.Add(new BusinessWebsite
            {
                name = profile.DisplayName,
                friendlyName = profile.DisplayName,
                description = profile.WebsiteDescription ?? "Usługi sportowe",
                url = profile.Website
            });
        }

        // Add contact person with fallbacks (always included)
        request.person.Add(new BusinessPerson
        {
            name = profile.ContactPersonName ?? profile.DisplayName.Split(' ').FirstOrDefault() ?? profile.DisplayName,
            surname = profile.ContactPersonSurname ?? (profile.DisplayName.Split(' ').Length > 1 ? string.Join(" ", profile.DisplayName.Split(' ').Skip(1)) : ""),
            isRepresentative = true,
            isContactPerson = true,
            contact = new List<PersonContact>
            {
                new PersonContact
                {
                    type = 1, // Email type
                    contact = profile.Email!
                }
            }
        });

        return request;
    }

    private string FormatPhoneNumber(string phoneNumber)
    {
        // Remove all non-digit characters
        var digitsOnly = new string(phoneNumber.Where(char.IsDigit).ToArray());
        
        // TPay expects exactly 9 or 14 digits
        // For Polish numbers: 9 digits (without country code) or 14 digits (with +48)
        
        if (digitsOnly.Length == 9)
        {
            // Already 9 digits, perfect for Poland
            return digitsOnly;
        }
        else if (digitsOnly.Length == 11 && digitsOnly.StartsWith("48"))
        {
            // Remove country code 48 to get 9 digits
            return digitsOnly.Substring(2);
        }
        else if (digitsOnly.Length == 12 && digitsOnly.StartsWith("048"))
        {
            // Remove country code 048 to get 9 digits  
            return digitsOnly.Substring(3);
        }
        else if (digitsOnly.Length >= 9)
        {
            // Take last 9 digits
            return digitsOnly.Substring(digitsOnly.Length - 9);
        }
        else
        {
            // Pad with leading zeros if less than 9 digits (edge case)
            return digitsOnly.PadLeft(9, '0');
        }
    }

    private string FormatNip(string nip)
    {
        if (string.IsNullOrWhiteSpace(nip))
            return "";

        // Remove all non-digit characters
        var digitsOnly = new string(nip.Where(char.IsDigit).ToArray());
        
        // NIP must be exactly 10 digits
        if (digitsOnly.Length == 10)
        {
            return digitsOnly;
        }
        else if (digitsOnly.Length > 10)
        {
            // Take first 10 digits
            return digitsOnly.Substring(0, 10);
        }
        else
        {
            // Pad with leading zeros
            return digitsOnly.PadLeft(10, '0');
        }
    }

    private string FormatRegon(string regon)
    {
        if (string.IsNullOrWhiteSpace(regon))
            return "";

        // Remove all non-digit characters
        var digitsOnly = new string(regon.Where(char.IsDigit).ToArray());
        
        // REGON can be 9 or 14 digits, but TPay seems to prefer 9 digits
        if (digitsOnly.Length >= 14)
        {
            // Take first 9 digits from 14-digit REGON
            return digitsOnly.Substring(0, 9);
        }
        else if (digitsOnly.Length >= 9)
        {
            // Take exactly 9 digits
            return digitsOnly.Substring(0, 9);
        }
        else
        {
            // Pad to 9 digits
            return digitsOnly.PadLeft(9, '0');
        }
    }

    private string FormatKrs(string krs)
    {
        if (string.IsNullOrWhiteSpace(krs))
            return "";

        // Remove all non-digit characters
        var digitsOnly = new string(krs.Where(char.IsDigit).ToArray());
        
        // KRS must be exactly 10 digits (with leading zeros)
        if (digitsOnly.Length >= 10)
        {
            return digitsOnly.Substring(0, 10);
        }
        else
        {
            return digitsOnly.PadLeft(10, '0');
        }
    }

    private async Task<string> ProcessFacilityPlanFile(string base64Data, string? fileName)
    {
        try
        {
            // Extract the base64 data after the comma (remove data:image/jpeg;base64, part)
            var commaIndex = base64Data.IndexOf(',');
            if (commaIndex == -1)
                throw new ArgumentException("Invalid base64 data format");

            var actualBase64 = base64Data.Substring(commaIndex + 1);
            var fileData = Convert.FromBase64String(actualBase64);

            // Generate a unique filename
            var fileExtension = GetFileExtensionFromBase64(base64Data);
            var uniqueFileName = $"facility_plan_{Guid.NewGuid()}{fileExtension}";

            // Upload to FTP server
            var facilityPlanUrl = await _ftpStorageService.UploadFileAsync(fileData, "facility-plans", uniqueFileName);
            return facilityPlanUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing facility plan file");
            throw new InvalidOperationException("Failed to process facility plan file", ex);
        }
    }

    private string GetFileExtensionFromBase64(string base64Data)
    {
        if (base64Data.StartsWith("data:image/jpeg") || base64Data.StartsWith("data:image/jpg"))
            return ".jpg";
        if (base64Data.StartsWith("data:image/png"))
            return ".png";
        if (base64Data.StartsWith("data:image/gif"))
            return ".gif";
        if (base64Data.StartsWith("data:application/pdf"))
            return ".pdf";
        
        return ".jpg"; // Default fallback
    }

    public async Task<FacilityPlanUploadResult> ProcessFacilityPlanUploadAsync(IFormFile facilityPlan)
    {
        try
        {
            _logger.LogInformation("Processing facility plan upload: FileName={FileName}, ContentType={ContentType}, Size={Size}",
                facilityPlan.FileName, facilityPlan.ContentType, facilityPlan.Length);

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "application/pdf" };
            if (!allowedTypes.Contains(facilityPlan.ContentType.ToLower()))
                throw new ArgumentException("Invalid file type. Only JPEG, PNG, GIF images and PDF files are allowed.");

            // Validate file size (max 10MB for facility plans)
            if (facilityPlan.Length > 10 * 1024 * 1024)
                throw new ArgumentException("File size too large. Maximum size is 10MB.");

            // Generate a unique filename
            var fileExtension = GetFileExtensionFromContentType(facilityPlan.ContentType);
            var uniqueFileName = $"facility_plan_{Guid.NewGuid()}{fileExtension}";

            // Upload to FTP server
            using (var stream = facilityPlan.OpenReadStream())
            {
                var uploadedUrl = await _ftpStorageService.UploadFileAsync(stream, "facility-plans", uniqueFileName);

                _logger.LogInformation("Facility plan uploaded successfully: {FileName} -> {UniqueFileName}",
                    facilityPlan.FileName, uniqueFileName);

                // Return the result
                return new FacilityPlanUploadResult
                {
                    Url = uploadedUrl,
                    FileName = facilityPlan.FileName ?? uniqueFileName,
                    FileType = facilityPlan.ContentType
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing facility plan file upload: FileName={FileName}, Size={Size}",
                facilityPlan?.FileName ?? "Unknown", facilityPlan?.Length ?? 0);
            throw new InvalidOperationException("Failed to process facility plan file", ex);
        }
    }

    private string GetFileExtensionFromContentType(string contentType)
    {
        return contentType.ToLower() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "application/pdf" => ".pdf",
            _ => ".jpg" // Default fallback
        };
    }

    public BusinessProfileWithFacilitiesDto? GetBusinessProfileWithFacilities(Guid businessProfileId)
    {
        // Get business profile
        var profile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (profile == null)
        {
            return null;
        }

        // Get all facilities for the business profile owner
        var facilities = _facilityRepository.GetUserFacilities(profile.UserId);

        // Map to DTO
        var result = new BusinessProfileWithFacilitiesDto
        {
            Id = profile.Id,
            UserId = profile.UserId,
            CompanyName = profile.CompanyName,
            DisplayName = profile.DisplayName,
            AvatarUrl = profile.AvatarUrl,
            ScheduleInfo = new BusinessScheduleInfoDto
            {
                Weekdays = profile.ScheduleTemplates
                    .Where(st => st.ScheduleType == ScheduleType.Weekdays)
                    .Select(st => new ScheduleSlotDto
                    {
                        Id = st.Time,
                        Time = st.Time,
                        IsAvailable = st.IsAvailable,
                        IsBooked = false
                    })
                    .OrderBy(s => s.Time)
                    .ToList(),
                Saturday = profile.ScheduleTemplates
                    .Where(st => st.ScheduleType == ScheduleType.Saturday)
                    .Select(st => new ScheduleSlotDto
                    {
                        Id = st.Time,
                        Time = st.Time,
                        IsAvailable = st.IsAvailable,
                        IsBooked = false
                    })
                    .OrderBy(s => s.Time)
                    .ToList(),
                Sunday = profile.ScheduleTemplates
                    .Where(st => st.ScheduleType == ScheduleType.Sunday)
                    .Select(st => new ScheduleSlotDto
                    {
                        Id = st.Time,
                        Time = st.Time,
                        IsAvailable = st.IsAvailable,
                        IsBooked = false
                    })
                    .OrderBy(s => s.Time)
                    .ToList(),
                DateSpecificAvailability = profile.DateAvailabilities
                    .GroupBy(da => da.Date.ToString("yyyy-MM-dd"))
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(da => new BusinessDateAvailabilitySlotDto
                        {
                            Time = da.Time,
                            IsAvailable = da.IsAvailable
                        }).OrderBy(s => s.Time).ToList()
                    )
            },
            Facilities = facilities.Select(f => new FacilityWithTimeSlotsDto
            {
                Id = f.Id,
                Name = f.Name,
                Type = f.Type,
                Description = f.Description,
                Capacity = f.Capacity,
                MaxUsers = f.MaxUsers,
                PricePerUser = f.PricePerUser,
                PricePerHour = f.PricePerHour,
                GrossPricePerHour = f.GrossPricePerHour,
                VatRate = f.VatRate,
                City = f.City,
                TimeSlots = _timeSlotRepository.GetFacilityTimeSlotsStructured(f.Id)
            }).ToList()
        };

        return result;
    }

    // KSeF integration methods
    public async Task<KSeFConfigurationDto> GetKSeFConfigurationAsync(Guid businessProfileId)
    {
        var profile = await _businessProfileRepository.GetBusinessProfileWithKSeFAsync(businessProfileId);
        if (profile == null)
        {
            throw new NotFoundException($"Business profile {businessProfileId} not found");
        }

        return new KSeFConfigurationDto
        {
            IsConfigured = !string.IsNullOrEmpty(profile.KSeFToken),
            IsEnabled = profile.KSeFEnabled,
            Environment = profile.KSeFEnvironment,
            RegisteredAt = profile.KSeFRegisteredAt,
            LastSyncAt = profile.KSeFLastSyncAt,
            StatusMessage = profile.KSeFEnabled
                ? "KSeF integration is active"
                : "KSeF integration is disabled"
        };
    }

    public async Task<bool> ConfigureKSeFAsync(Guid businessProfileId, ConfigureKSeFDto configDto)
    {
        // Validate environment
        if (configDto.Environment != "Test" && configDto.Environment != "Production")
        {
            throw new ValidationException("Environment must be either 'Test' or 'Production'");
        }

        // Validate token is not empty
        if (string.IsNullOrWhiteSpace(configDto.Token))
        {
            throw new ValidationException("KSeF token is required");
        }

        var success = await _businessProfileRepository.UpdateKSeFCredentialsAsync(
            businessProfileId,
            configDto.Token,
            configDto.Environment);

        if (!success)
        {
            throw new NotFoundException($"Business profile {businessProfileId} not found");
        }

        _logger.LogInformation("KSeF credentials configured for business profile {BusinessProfileId}, Environment: {Environment}",
            businessProfileId, configDto.Environment);

        return true;
    }

    public async Task<bool> UpdateKSeFStatusAsync(Guid businessProfileId, bool enabled)
    {
        var success = await _businessProfileRepository.UpdateKSeFStatusAsync(businessProfileId, enabled);
        if (!success)
        {
            throw new NotFoundException($"Business profile {businessProfileId} not found");
        }

        _logger.LogInformation("KSeF integration {Status} for business profile {BusinessProfileId}",
            enabled ? "enabled" : "disabled", businessProfileId);

        return true;
    }

    public async Task<KSeFConnectionTestDto> TestKSeFConnectionAsync(Guid businessProfileId)
    {
        var profile = await _businessProfileRepository.GetBusinessProfileWithKSeFAsync(businessProfileId);
        if (profile == null)
        {
            throw new NotFoundException($"Business profile {businessProfileId} not found");
        }

        if (!profile.KSeFEnabled)
        {
            return new KSeFConnectionTestDto
            {
                IsSuccessful = false,
                Message = "KSeF integration is disabled for this business",
                TestedAt = DateTime.UtcNow
            };
        }

        if (string.IsNullOrEmpty(profile.KSeFToken))
        {
            return new KSeFConnectionTestDto
            {
                IsSuccessful = false,
                Message = "KSeF token is not configured",
                TestedAt = DateTime.UtcNow
            };
        }

        // Test actual connection to KSeF API
        var testResult = await _ksefApiService.TestConnectionAsync(
            profile.Nip,
            profile.KSeFToken,
            profile.KSeFEnvironment);

        if (testResult)
        {
            return new KSeFConnectionTestDto
            {
                IsSuccessful = true,
                Message = $"Successfully connected to KSeF {profile.KSeFEnvironment} environment",
                TestedAt = DateTime.UtcNow
            };
        }
        else
        {
            return new KSeFConnectionTestDto
            {
                IsSuccessful = false,
                Message = "Failed to connect to KSeF API. Please check your credentials and try again.",
                TestedAt = DateTime.UtcNow
            };
        }
    }
}