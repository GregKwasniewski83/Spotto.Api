using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class TrainerProfileService : ITrainerProfileService
{
    private readonly ITrainerProfileRepository _trainerProfileRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly ITrainerBusinessAssociationRepository _associationRepository;
    private readonly ITPayService _tpayService;
    private readonly ITPayDictionaryRepository _dictionaryRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<TrainerProfileService> _logger;
    private readonly TPayConfiguration _tpayConfig;
    private readonly IFtpStorageService _ftpStorageService;

    public TrainerProfileService(
        ITrainerProfileRepository trainerProfileRepository,
        IBusinessProfileRepository businessProfileRepository,
        ITrainerBusinessAssociationRepository associationRepository,
        ITPayService tpayService,
        ITPayDictionaryRepository dictionaryRepository,
        IEmailService emailService,
        ILogger<TrainerProfileService> logger,
        IOptions<TPayConfiguration> tpayConfig,
        IFtpStorageService ftpStorageService)
    {
        _trainerProfileRepository = trainerProfileRepository;
        _businessProfileRepository = businessProfileRepository;
        _associationRepository = associationRepository;
        _tpayService = tpayService;
        _dictionaryRepository = dictionaryRepository;
        _emailService = emailService;
        _logger = logger;
        _tpayConfig = tpayConfig.Value;
        _ftpStorageService = ftpStorageService;
    }

    public TrainerProfileDto? GetTrainerProfile(Guid userId)
    {
        var profile = _trainerProfileRepository.GetTrainerProfile(userId);
        return profile == null ? null : MapToDto(profile);
    }

    public TrainerProfileDto? GetTrainerProfileById(Guid trainerProfileId)
    {
        var profile = _trainerProfileRepository.GetTrainerProfileById(trainerProfileId);
        return profile == null ? null : MapToDto(profile);
    }

    public async Task<TrainerProfileDto> CreateTrainerProfile(CreateTrainerProfileDto profileDto, Guid userId)
    {
        // Check if trainer profile already exists
        if (_trainerProfileRepository.TrainerProfileExists(userId))
        {
            throw new InvalidOperationException("Trainer profile already exists for this user");
        }

        if (profileDto.TrainerType == TrainerType.Independent)
        {
            // Independent trainers require company fields and a unique NIP
            if (string.IsNullOrWhiteSpace(profileDto.Nip))
                throw new InvalidOperationException("NIP is required for independent trainers");
            if (string.IsNullOrWhiteSpace(profileDto.CompanyName))
                throw new InvalidOperationException("Company name is required for independent trainers");
            if (string.IsNullOrWhiteSpace(profileDto.Address))
                throw new InvalidOperationException("Address is required for independent trainers");
            if (string.IsNullOrWhiteSpace(profileDto.City))
                throw new InvalidOperationException("City is required for independent trainers");
            if (string.IsNullOrWhiteSpace(profileDto.PostalCode))
                throw new InvalidOperationException("Postal code is required for independent trainers");

            // Check if NIP already exists
            if (_trainerProfileRepository.NipExists(profileDto.Nip))
                throw new InvalidOperationException("A trainer profile with this NIP already exists");
        }

        // Try TPay registration FIRST — only for independent trainers
        TPayBusinessRegistrationResponse? tpayResponse = null;
        if (profileDto.TrainerType == TrainerType.Independent)
        {
            if (profileDto.AutoRegisterWithTPay &&
                !string.IsNullOrEmpty(profileDto.Email) &&
                !string.IsNullOrEmpty(profileDto.PhoneNumber))
            {
                _logger.LogInformation("Attempting TPay registration for user {UserId} BEFORE trainer profile creation", userId);

                var tpayRequest = BuildTPayRegistrationRequest(profileDto);

                // Log phone numbers for debugging
                _logger.LogDebug("TPay registration phone numbers - Main: {MainPhone}, Address: {AddressPhone}",
                    tpayRequest.phone?.phoneNumber,
                    tpayRequest.address?.FirstOrDefault()?.phone);

                // Log complete TPay registration request for debugging
                _logger.LogInformation("Sending TPay business registration request: {TPayRequest}",
                    System.Text.Json.JsonSerializer.Serialize(tpayRequest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // Let TPay exceptions bubble up - this will prevent profile creation if TPay fails
                tpayResponse = await _tpayService.RegisterBusinessAsync(tpayRequest);

                _logger.LogInformation("TPay registration successful for user {UserId}. Merchant ID: {MerchantId}, Verification Status: {VerificationStatus}",
                    userId, tpayResponse.id, tpayResponse.verificationStatus);
            }
            else if (profileDto.AutoRegisterWithTPay)
            {
                _logger.LogWarning("TPay auto-registration was requested but required fields are missing for user {UserId}", userId);
                throw new InvalidOperationException("TPay auto-registration was requested but required fields (Email, PhoneNumber) are missing");
            }
        }
        else
        {
            _logger.LogInformation("Skipping TPay registration for employee trainer {UserId}", userId);
        }

        // Create trainer profile only AFTER TPay registration succeeds (or if TPay not requested)
        var profile = _trainerProfileRepository.CreateTrainerProfile(profileDto, userId);

        // Update profile with TPay data if registration was successful
        if (tpayResponse != null)
        {
            // Update profile with TPay merchant data
            profile.TPayMerchantId = tpayResponse.id;
            profile.TPayAccountId = tpayResponse.website?.FirstOrDefault()?.accountId;
            profile.TPayPosId = tpayResponse.website?.FirstOrDefault()?.posId;
            profile.TPayActivationLink = tpayResponse.activationLink;
            profile.TPayVerificationStatus = tpayResponse.verificationStatus;
            profile.TPayRegisteredAt = DateTime.UtcNow;
            
            // Copy TPay registration fields to profile
            profile.Email = profileDto.Email;
            profile.PhoneNumber = profileDto.PhoneNumber;
            profile.PhoneCountry = profileDto.PhoneCountry;
            profile.Regon = profileDto.Regon;
            profile.Krs = profileDto.Krs;
            profile.LegalForm = profileDto.LegalForm;
            profile.CategoryId = profileDto.CategoryId;
            profile.Mcc = profileDto.Mcc;
            profile.Website = profileDto.Website;
            profile.WebsiteDescription = profileDto.WebsiteDescription;
            profile.ContactPersonName = profileDto.ContactPersonName;
            profile.ContactPersonSurname = profileDto.ContactPersonSurname;
            
            _trainerProfileRepository.UpdateTrainerProfile(profile);
            
            _logger.LogInformation("Trainer profile created successfully with TPay integration. Profile ID: {ProfileId}, Merchant ID: {MerchantId}", 
                profile.Id, tpayResponse.id);
        }
        else
        {
            _logger.LogInformation("Trainer profile created successfully without TPay integration. Profile ID: {ProfileId}", profile.Id);
        }

        var trainerProfileDto = MapToDto(profile);
        
        // Send email notification (async, don't wait) - capture variables for lambda
        var emailToSend = trainerProfileDto.Email;
        var profileDtoForEmail = trainerProfileDto;
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendTrainerProfileCreatedEmailAsync(profileDtoForEmail);
                _logger.LogInformation("Trainer profile creation email sent successfully to {Email}", emailToSend);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send trainer profile creation email to {Email}: {Error}", emailToSend, ex.Message);
            }
        });

        return trainerProfileDto;
    }

    public async Task<TrainerProfileDto?> UpdateTrainerProfile(Guid userId, UpdateTrainerProfileDto profileDto)
    {
        // Check if NIP already exists for another user
        if (_trainerProfileRepository.NipExists(profileDto.Nip, userId))
        {
            throw new InvalidOperationException("A trainer profile with this NIP already exists");
        }

        var updatedProfile = _trainerProfileRepository.UpdateTrainerProfile(userId, profileDto);
        if (updatedProfile == null)
            return null;

        // Handle TPay registration update if requested and required fields are provided
        if (profileDto.UpdateTPayRegistration && 
            !string.IsNullOrEmpty(profileDto.Email) && 
            !string.IsNullOrEmpty(profileDto.PhoneNumber))
        {
            try
            {
                _logger.LogInformation("Updating TPay registration for trainer profile {TrainerProfileId} for user {UserId}", updatedProfile.Id, userId);
                
                // Update TPay registration fields in the profile first
                updatedProfile.Email = profileDto.Email;
                updatedProfile.PhoneNumber = profileDto.PhoneNumber;
                updatedProfile.PhoneCountry = profileDto.PhoneCountry;
                updatedProfile.Regon = profileDto.Regon;
                updatedProfile.Krs = profileDto.Krs;
                updatedProfile.LegalForm = profileDto.LegalForm;
                updatedProfile.CategoryId = profileDto.CategoryId;
                updatedProfile.Mcc = profileDto.Mcc;
                updatedProfile.Website = profileDto.Website;
                updatedProfile.WebsiteDescription = profileDto.WebsiteDescription;
                updatedProfile.ContactPersonName = profileDto.ContactPersonName;
                updatedProfile.ContactPersonSurname = profileDto.ContactPersonSurname;
                
                _trainerProfileRepository.UpdateTrainerProfile(updatedProfile);

                // If trainer doesn't have TPay merchant ID yet, register with TPay
                if (string.IsNullOrEmpty(updatedProfile.TPayMerchantId))
                {
                    var tpayRequest = BuildTPayRegistrationRequest(updatedProfile);
                    var response = await _tpayService.RegisterBusinessAsync(tpayRequest);
                    
                    _logger.LogInformation("TPay registration successful for trainer {UserId} with merchant ID {MerchantId}", userId, response.id);
                    
                    // Update profile with TPay merchant data
                    updatedProfile.TPayMerchantId = response.id;
                    updatedProfile.TPayAccountId = response.website?.FirstOrDefault()?.accountId;
                    updatedProfile.TPayPosId = response.website?.FirstOrDefault()?.posId;
                    updatedProfile.TPayActivationLink = response.activationLink;
                    updatedProfile.TPayVerificationStatus = response.verificationStatus;
                    updatedProfile.TPayRegisteredAt = DateTime.UtcNow;
                    
                    _trainerProfileRepository.UpdateTrainerProfile(updatedProfile);
                }
                else
                {
                    _logger.LogInformation("Trainer profile {TrainerProfileId} already has TPay merchant ID {MerchantId}, skipping registration", 
                        updatedProfile.Id, updatedProfile.TPayMerchantId);
                }
                
                _logger.LogInformation("TPay registration update completed for trainer profile {TrainerProfileId}", updatedProfile.Id);
            }
            catch (TPayException ex)
            {
                _logger.LogWarning(ex, "TPay registration update failed for trainer profile {TrainerProfileId}, but profile update will continue. Error: {Error}", 
                    updatedProfile.Id, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during TPay registration update for trainer profile {TrainerProfileId}, but profile update will continue", 
                    updatedProfile.Id);
            }
        }

        return MapToDto(updatedProfile);
    }

    public bool DeleteTrainerProfile(Guid userId)
    {
        return _trainerProfileRepository.DeleteTrainerProfile(userId);
    }

    public async Task<string> UploadAvatarAsync(Guid userId, IFormFile avatar)
    {
        // Validate file
        if (avatar == null || avatar.Length == 0)
        {
            throw new ArgumentException("No file provided");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var fileExtension = Path.GetExtension(avatar.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(fileExtension))
        {
            throw new InvalidOperationException("Only image files (jpg, jpeg, png, gif) are allowed");
        }

        // Check if trainer profile exists
        var trainerProfile = _trainerProfileRepository.GetTrainerProfile(userId);
        if (trainerProfile == null)
        {
            throw new InvalidOperationException("Trainer profile not found");
        }

        // Generate unique filename
        var fileName = $"trainer-{userId}-{Guid.NewGuid()}{fileExtension}";

        // Upload to FTP server
        using (var stream = avatar.OpenReadStream())
        {
            var avatarUrl = await _ftpStorageService.UploadFileAsync(stream, "trainers", fileName);
            await _trainerProfileRepository.UpdateAvatarAsync(userId, avatarUrl);
            return avatarUrl;
        }
    }

    public bool AssociateBusinessProfile(Guid trainerUserId, string businessProfileId)
    {
        // Validate that the business profile exists
        if (Guid.TryParse(businessProfileId, out var businessGuid))
        {
            var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessGuid);
            if (businessProfile == null)
            {
                throw new ArgumentException("Business profile not found");
            }
        }
        else
        {
            throw new ArgumentException("Invalid business profile ID format");
        }

        return _trainerProfileRepository.AssociateBusinessProfile(trainerUserId, businessProfileId);
    }

    public bool DisassociateBusinessProfile(Guid trainerUserId, string businessProfileId)
    {
        return _trainerProfileRepository.DisassociateBusinessProfile(trainerUserId, businessProfileId);
    }

    public BusinessAssociationResultDto AssociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds)
    {
        return _trainerProfileRepository.AssociateMultipleBusinessProfiles(trainerUserId, businessProfileIds);
    }

    public BusinessAssociationResultDto DisassociateMultipleBusinessProfiles(Guid trainerUserId, List<string> businessProfileIds)
    {
        return _trainerProfileRepository.DisassociateMultipleBusinessProfiles(trainerUserId, businessProfileIds);
    }

    public List<BusinessProfileDto> GetAssociatedBusinessProfiles(Guid trainerUserId)
    {
        var businessIds = _trainerProfileRepository.GetAssociatedBusinessIds(trainerUserId);
        var businessProfiles = new List<BusinessProfileDto>();

        foreach (var businessId in businessIds)
        {
            if (Guid.TryParse(businessId, out var businessGuid))
            {
                var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessGuid);
                if (businessProfile != null)
                {
                    businessProfiles.Add(MapBusinessProfileToDto(businessProfile));
                }
            }
        }

        return businessProfiles;
    }

    public List<AvailableTrainerDto> FindAvailableTrainers(DateTime date, List<string> timeSlots)
    {
        var availableTrainersData = _trainerProfileRepository.FindAvailableTrainers(date, timeSlots);
        
        return availableTrainersData.Select(x => new AvailableTrainerDto
        {
            Id = x.trainer.Id,
            UserId = x.trainer.UserId,
            CompanyName = x.trainer.CompanyName,
            DisplayName = x.trainer.DisplayName,
            Specializations = x.trainer.Specializations,
            HourlyRate = x.trainer.HourlyRate,
            Description = x.trainer.Description,
            Certifications = x.trainer.Certifications,
            Languages = x.trainer.Languages,
            ExperienceYears = x.trainer.ExperienceYears,
            Rating = x.trainer.Rating,
            TotalSessions = x.trainer.TotalSessions,
            AvatarUrl = x.trainer.AvatarUrl,
            AvailableTimeSlots = x.availableSlots
        }).ToList();
    }

    public TrainerDateTimeSlotsDto? GetMyTimeSlotsForDate(Guid userId, DateTime date)
    {
        return _trainerProfileRepository.GetMyTimeSlotsForDate(userId, date);
    }

    public List<AvailableTrainerDto> FindAvailableTrainersForBusiness(Guid businessProfileId, DateTime date, List<string> timeSlots)
    {
        // This would need a new repository method that filters by AssociatedBusinessId
        var availableTrainersData = _trainerProfileRepository.FindAvailableTrainersForBusiness(businessProfileId, date, timeSlots);

        return availableTrainersData.Select(x => new AvailableTrainerDto
        {
            Id = x.trainer.Id,
            UserId = x.trainer.UserId,
            CompanyName = x.trainer.CompanyName,
            DisplayName = x.trainer.DisplayName,
            Specializations = x.trainer.Specializations,
            HourlyRate = x.trainer.HourlyRate,
            Description = x.trainer.Description,
            Certifications = x.trainer.Certifications,
            Languages = x.trainer.Languages,
            ExperienceYears = x.trainer.ExperienceYears,
            Rating = x.trainer.Rating,
            TotalSessions = x.trainer.TotalSessions,
            AvatarUrl = x.trainer.AvatarUrl,
            AvailableTimeSlots = x.availableSlots
        }).ToList();
    }

    public async Task UpdateTrainerScheduleWithBusinessAsync(Guid userId, UpdateTrainerScheduleWithBusinessDto dto)
    {
        var trainerProfile = _trainerProfileRepository.GetTrainerProfile(userId);
        if (trainerProfile == null)
        {
            throw new NotFoundException("TrainerProfile", userId.ToString());
        }

        // Parse schedule type
        if (!Enum.TryParse<ScheduleType>(dto.ScheduleType, true, out var scheduleType))
        {
            throw new BusinessRuleException($"Invalid schedule type: {dto.ScheduleType}. Valid values are: weekdays, saturday, sunday");
        }

        // Validate business assignments
        foreach (var slot in dto.TimeSlots.Where(s => s.AssociatedBusinessId.HasValue))
        {
            var businessId = slot.AssociatedBusinessId!.Value;

            // Check if association is confirmed
            var isConfirmed = await _associationRepository.IsAssociationConfirmedAsync(trainerProfile.Id, businessId);
            if (!isConfirmed)
            {
                throw new BusinessRuleException($"No confirmed association with business {businessId}. Please request and confirm association first.");
            }

            // Validate time falls within business operating hours
            await ValidateTimeSlotAgainstBusinessHoursAsync(businessId, scheduleType, slot.Time);
        }

        // Update the schedule templates
        await _trainerProfileRepository.UpdateTrainerScheduleTemplatesWithBusinessAsync(
            trainerProfile.Id,
            scheduleType,
            dto.TimeSlots);

        _logger.LogInformation("Updated trainer {TrainerId} schedule for {ScheduleType} with {SlotCount} slots",
            trainerProfile.Id, dto.ScheduleType, dto.TimeSlots.Count);
    }

    public async Task UpdateTrainerDateAvailabilityWithBusinessAsync(Guid userId, UpdateTrainerDateAvailabilityWithBusinessDto dto)
    {
        var trainerProfile = _trainerProfileRepository.GetTrainerProfile(userId);
        if (trainerProfile == null)
        {
            throw new NotFoundException("TrainerProfile", userId.ToString());
        }

        // Determine schedule type based on date
        var scheduleType = dto.Date.DayOfWeek switch
        {
            DayOfWeek.Saturday => ScheduleType.Saturday,
            DayOfWeek.Sunday => ScheduleType.Sunday,
            _ => ScheduleType.Weekdays
        };

        // Validate business assignments
        foreach (var slot in dto.TimeSlots.Where(s => s.AssociatedBusinessId.HasValue))
        {
            var businessId = slot.AssociatedBusinessId!.Value;

            // Check if association is confirmed
            var isConfirmed = await _associationRepository.IsAssociationConfirmedAsync(trainerProfile.Id, businessId);
            if (!isConfirmed)
            {
                throw new BusinessRuleException($"No confirmed association with business {businessId}. Please request and confirm association first.");
            }

            // Validate time falls within business operating hours
            await ValidateTimeSlotAgainstBusinessHoursAsync(businessId, scheduleType, slot.Time);
        }

        // Update the date-specific availability
        await _trainerProfileRepository.UpdateTrainerDateAvailabilityWithBusinessAsync(
            trainerProfile.Id,
            dto.Date,
            dto.TimeSlots);

        _logger.LogInformation("Updated trainer {TrainerId} availability for {Date} with {SlotCount} slots",
            trainerProfile.Id, dto.Date.ToString("yyyy-MM-dd"), dto.TimeSlots.Count);
    }

    private async Task ValidateTimeSlotAgainstBusinessHoursAsync(Guid businessId, ScheduleType scheduleType, string time)
    {
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessId);
        if (businessProfile == null)
        {
            throw new NotFoundException("BusinessProfile", businessId.ToString());
        }

        // Find the business schedule for this day type
        var businessSchedule = businessProfile.ScheduleTemplates
            .Where(st => st.ScheduleType == scheduleType && st.IsAvailable)
            .Select(st => st.Time)
            .ToList();

        if (!businessSchedule.Contains(time))
        {
            var dayTypeName = scheduleType switch
            {
                ScheduleType.Weekdays => "weekdays",
                ScheduleType.Saturday => "Saturday",
                ScheduleType.Sunday => "Sunday",
                _ => "unknown"
            };

            throw new BusinessRuleException(
                $"Time slot {time} is not within {businessProfile.DisplayName}'s operating hours for {dayTypeName}. " +
                $"Available times: {string.Join(", ", businessSchedule.OrderBy(t => t))}");
        }
    }

    private TrainerProfileDto MapToDto(TrainerProfile trainerProfile)
    {
        return new TrainerProfileDto
        {
            Id = trainerProfile.Id,
            UserId = trainerProfile.UserId,
            TrainerType = trainerProfile.TrainerType,
            Nip = trainerProfile.Nip,
            CompanyName = trainerProfile.CompanyName,
            DisplayName = trainerProfile.DisplayName,
            Address = trainerProfile.Address,
            City = trainerProfile.City,
            PostalCode = trainerProfile.PostalCode,
            AvatarUrl = trainerProfile.AvatarUrl,
            Specializations = trainerProfile.Specializations,
            HourlyRate = trainerProfile.HourlyRate,
            VatRate = trainerProfile.VatRate,
            GrossHourlyRate = trainerProfile.GrossHourlyRate,
            Description = trainerProfile.Description,
            Certifications = trainerProfile.Certifications,
            Languages = trainerProfile.Languages,
            ExperienceYears = trainerProfile.ExperienceYears,
            Rating = trainerProfile.Rating,
            TotalSessions = trainerProfile.TotalSessions,
            AssociatedBusinessIds = trainerProfile.AssociatedBusinessIds,
            CreatedAt = trainerProfile.CreatedAt,
            UpdatedAt = trainerProfile.UpdatedAt,
            Availability = MapScheduleTemplatesToAvailability(trainerProfile.ScheduleTemplates, trainerProfile.Id),
            
            // TPay registration fields
            Email = trainerProfile.Email,
            PhoneNumber = trainerProfile.PhoneNumber,
            PhoneCountry = trainerProfile.PhoneCountry,
            Regon = trainerProfile.Regon,
            Krs = trainerProfile.Krs,
            LegalForm = trainerProfile.LegalForm,
            CategoryId = trainerProfile.CategoryId,
            Mcc = trainerProfile.Mcc,
            Website = trainerProfile.Website,
            WebsiteDescription = trainerProfile.WebsiteDescription,
            ContactPersonName = trainerProfile.ContactPersonName,
            ContactPersonSurname = trainerProfile.ContactPersonSurname,
            
            // TPay merchant data
            TPayMerchantId = trainerProfile.TPayMerchantId,
            TPayAccountId = trainerProfile.TPayAccountId,
            TPayPosId = trainerProfile.TPayPosId,
            TPayActivationLink = trainerProfile.TPayActivationLink,
            TPayVerificationStatus = trainerProfile.TPayVerificationStatus,
            TPayRegisteredAt = trainerProfile.TPayRegisteredAt
        };
    }

    private TrainerAvailabilityDto MapScheduleTemplatesToAvailability(List<TrainerScheduleTemplate> scheduleTemplates, Guid trainerProfileId)
    {
        var availability = new TrainerAvailabilityDto();

        // Get confirmed associations with their colors and business names
        var associations = _associationRepository.GetByTrainerProfileIdAsync(trainerProfileId, AssociationStatus.Confirmed).Result
            .ToDictionary(
                tba => tba.BusinessProfileId,
                tba => new { tba.Color, BusinessName = tba.BusinessProfile?.DisplayName ?? tba.BusinessProfile?.CompanyName }
            );

        // Group templates by schedule type
        var groupedTemplates = scheduleTemplates.GroupBy(st => st.ScheduleType);

        foreach (var group in groupedTemplates)
        {
            var timeSlots = group.Select(template =>
            {
                var businessInfo = template.AssociatedBusinessId.HasValue && associations.ContainsKey(template.AssociatedBusinessId.Value)
                    ? associations[template.AssociatedBusinessId.Value]
                    : null;

                return new TimeSlotItemDto
                {
                    Id = template.Time,
                    Time = template.Time,
                    IsAvailable = template.IsAvailable,
                    IsBooked = false,
                    BookedBy = null,
                    AssociatedBusinessId = template.AssociatedBusinessId,
                    AssociatedBusinessName = businessInfo?.BusinessName,
                    Color = businessInfo?.Color
                };
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

        return availability;
    }

    private BusinessProfileDto MapBusinessProfileToDto(BusinessProfile businessProfile)
    {
        return new BusinessProfileDto
        {
            Id = businessProfile.Id,
            UserId = businessProfile.UserId,
            Nip = businessProfile.Nip,
            CompanyName = businessProfile.CompanyName,
            DisplayName = businessProfile.DisplayName,
            Address = businessProfile.Address,
            City = businessProfile.City,
            PostalCode = businessProfile.PostalCode,
            AvatarUrl = businessProfile.AvatarUrl,
            CreatedAt = businessProfile.CreatedAt,
            UpdatedAt = businessProfile.UpdatedAt
        };
    }

    public async Task<TrainerProfileDto> RegisterWithTPayAsync(Guid userId, TPayBusinessRegistrationRequest request)
    {
        _logger.LogInformation("Manual TPay registration requested for trainer {UserId}", userId);
        
        var profile = _trainerProfileRepository.GetTrainerProfile(userId);
        if (profile == null)
        {
            throw new NotFoundException("TrainerProfile", userId.ToString());
        }

        try
        {
            _logger.LogInformation("Attempting TPay registration for trainer profile {TrainerProfileId} for user {UserId}", profile.Id, userId);
            
            var response = await _tpayService.RegisterBusinessAsync(request);
            
            _logger.LogInformation("TPay registration successful for trainer {UserId} with merchant ID {MerchantId}", userId, response.id);
            
            // Update profile with TPay merchant data
            profile.TPayMerchantId = response.id;
            profile.TPayAccountId = response.website?.FirstOrDefault()?.accountId;
            profile.TPayPosId = response.website?.FirstOrDefault()?.posId;
            profile.TPayActivationLink = response.activationLink;
            profile.TPayVerificationStatus = response.verificationStatus;
            profile.TPayRegisteredAt = DateTime.UtcNow;
            
            _trainerProfileRepository.UpdateTrainerProfile(profile);
            
            return MapToDto(profile);
        }
        catch (TPayException ex)
        {
            _logger.LogError(ex, "TPay registration failed for trainer profile {TrainerProfileId} with error: {Error}", 
                profile.Id, ex.Message);
            throw;
        }
    }

    public async Task<TrainerProfileDto> UpdateTPayMerchantDataAsync(Guid userId, TPayBusinessRegistrationResponse response)
    {
        _logger.LogInformation("Updating TPay merchant data for trainer {UserId} with merchant ID {MerchantId}", userId, response.id);
        
        var profile = _trainerProfileRepository.GetTrainerProfile(userId);
        if (profile == null)
        {
            throw new NotFoundException("TrainerProfile", userId.ToString());
        }

        try
        {
            // Update profile with TPay merchant data
            profile.TPayMerchantId = response.id;
            profile.TPayAccountId = response.website?.FirstOrDefault()?.accountId;
            profile.TPayPosId = response.website?.FirstOrDefault()?.posId;
            profile.TPayActivationLink = response.activationLink;
            profile.TPayVerificationStatus = response.verificationStatus;
            profile.TPayRegisteredAt = DateTime.UtcNow;
            
            _trainerProfileRepository.UpdateTrainerProfile(profile);
            
            _logger.LogInformation("TPay merchant data updated successfully for trainer {UserId}", userId);
            
            return MapToDto(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update TPay merchant data for trainer {UserId}", userId);
            throw new BusinessRuleException("Failed to update TPay merchant data.", ex);
        }
    }

    private TPayBusinessRegistrationRequest BuildTPayRegistrationRequest(CreateTrainerProfileDto profileDto)
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
            legalForm = profileDto.LegalForm ?? 1, // Default to "Osoba fizyczna prowadząca działalność gospodarczą"
            categoryId = profileDto.CategoryId ?? 74, // Default to "Kursy i szkolenia"
            mcc = profileDto.Mcc ?? "8999", // Default MCC for other professional services
            merchantApiConsent = false,
            website = new List<BusinessWebsite>(),
            address = new List<BusinessAddress>
            {
                new BusinessAddress
                {
                    friendlyName = "Main Address",
                    name = profileDto.CompanyName,
                    street = profileDto.Address,
                    houseNumber = "1", // Default value
                    roomNumber = "",
                    postalCode = profileDto.PostalCode,
                    city = profileDto.City,
                    country = "PL",
                    phone = FormatPhoneNumber(profileDto.PhoneNumber ?? ""),
                    isMain = true,
                    isCorrespondence = true,
                    isInvoice = true
                }
            },
            person = new List<BusinessPerson>
            {
                new BusinessPerson
                {
                    name = profileDto.ContactPersonName ?? profileDto.DisplayName.Split(' ').FirstOrDefault() ?? profileDto.DisplayName,
                    surname = profileDto.ContactPersonSurname ?? (profileDto.DisplayName.Split(' ').Length > 1 ? string.Join(" ", profileDto.DisplayName.Split(' ').Skip(1)) : ""),
                    isRepresentative = true,
                    isContactPerson = true,
                    contact = new List<PersonContact>
                    {
                        new PersonContact
                        {
                            type = 1, // Email
                            contact = profileDto.Email!
                        }
                    }
                }
            }
        };

        // Add website if provided
        if (!string.IsNullOrEmpty(profileDto.Website))
        {
            request.website.Add(new BusinessWebsite
            {
                name = profileDto.DisplayName,
                friendlyName = profileDto.DisplayName,
                description = profileDto.WebsiteDescription ?? $"Trainer services by {profileDto.DisplayName}",
                url = profileDto.Website
            });
        }

        return request;
    }

    private TPayBusinessRegistrationRequest BuildTPayRegistrationRequest(TrainerProfile profile)
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
            categoryId = profile.CategoryId ?? 74,
            mcc = profile.Mcc ?? "8999",
            merchantApiConsent = false,
            website = new List<BusinessWebsite>(),
            address = new List<BusinessAddress>
            {
                new BusinessAddress
                {
                    friendlyName = "Main Address",
                    name = profile.CompanyName,
                    street = profile.Address,
                    houseNumber = "1",
                    roomNumber = "",
                    postalCode = profile.PostalCode,
                    city = profile.City,
                    country = "PL",
                    phone = FormatPhoneNumber(profile.PhoneNumber ?? ""),
                    isMain = true,
                    isCorrespondence = true,
                    isInvoice = true
                }
            },
            person = new List<BusinessPerson>
            {
                new BusinessPerson
                {
                    name = profile.ContactPersonName ?? profile.DisplayName.Split(' ').FirstOrDefault() ?? profile.DisplayName,
                    surname = profile.ContactPersonSurname ?? (profile.DisplayName.Split(' ').Length > 1 ? string.Join(" ", profile.DisplayName.Split(' ').Skip(1)) : ""),
                    isRepresentative = true,
                    isContactPerson = true,
                    contact = new List<PersonContact>
                    {
                        new PersonContact
                        {
                            type = 1, // Email
                            contact = profile.Email!
                        }
                    }
                }
            }
        };

        // Add website if provided
        if (!string.IsNullOrEmpty(profile.Website))
        {
            request.website.Add(new BusinessWebsite
            {
                name = profile.DisplayName,
                friendlyName = profile.DisplayName,
                description = profile.WebsiteDescription ?? $"Trainer services by {profile.DisplayName}",
                url = profile.Website
            });
        }

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
}