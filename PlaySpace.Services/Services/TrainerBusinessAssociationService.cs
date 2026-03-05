using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class TrainerBusinessAssociationService : ITrainerBusinessAssociationService
{
    private readonly ITrainerBusinessAssociationRepository _associationRepository;
    private readonly ITrainerProfileRepository _trainerProfileRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TrainerBusinessAssociationService> _logger;

    // Distinct color palette - easy to differentiate, visually appealing
    private static readonly string[] ColorPalette = new[]
    {
        "#FF6B6B", // Red
        "#4ECDC4", // Teal
        "#45B7D1", // Sky Blue
        "#96CEB4", // Sage Green
        "#FFEAA7", // Yellow
        "#DDA0DD", // Plum
        "#98D8C8", // Mint
        "#F7DC6F", // Gold
        "#BB8FCE", // Purple
        "#85C1E9", // Light Blue
        "#F8B500", // Orange
        "#58D68D", // Green
        "#EC7063", // Coral
        "#5DADE2", // Blue
        "#F1948A", // Pink
        "#7DCEA0", // Sea Green
        "#D7BDE2", // Lavender
        "#F9E79F", // Light Yellow
        "#A3E4D7", // Aqua
        "#FAD7A0", // Peach
    };

    public TrainerBusinessAssociationService(
        ITrainerBusinessAssociationRepository associationRepository,
        ITrainerProfileRepository trainerProfileRepository,
        IBusinessProfileRepository businessProfileRepository,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<TrainerBusinessAssociationService> logger)
    {
        _associationRepository = associationRepository;
        _trainerProfileRepository = trainerProfileRepository;
        _businessProfileRepository = businessProfileRepository;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TrainerBusinessAssociationResponseDto> RequestAssociationAsync(Guid trainerProfileId, Guid businessProfileId)
    {
        _logger.LogInformation("Trainer {TrainerProfileId} requesting association with business {BusinessProfileId}",
            trainerProfileId, businessProfileId);

        // Verify trainer exists
        var trainerProfile = _trainerProfileRepository.GetTrainerProfileById(trainerProfileId);
        if (trainerProfile == null)
            throw new NotFoundException("TrainerProfile", trainerProfileId.ToString());

        // Verify business exists
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (businessProfile == null)
            throw new NotFoundException("BusinessProfile", businessProfileId.ToString());

        // Check if association already exists
        var existingAssociation = await _associationRepository.GetByTrainerAndBusinessAsync(trainerProfileId, businessProfileId);
        if (existingAssociation != null)
        {
            if (existingAssociation.Status == AssociationStatus.Pending)
                throw new BusinessRuleException("Association request already pending for this business");

            if (existingAssociation.Status == AssociationStatus.Confirmed)
                throw new BusinessRuleException("Association with this business already exists");

            if (existingAssociation.Status == AssociationStatus.Rejected)
            {
                // Allow re-requesting after rejection (update existing)
                _logger.LogInformation("Re-requesting association after previous rejection");
            }
        }

        // Generate confirmation token
        var confirmationToken = GenerateConfirmationToken();

        // Create or update association
        TrainerBusinessAssociation association;
        if (existingAssociation != null && existingAssociation.Status == AssociationStatus.Rejected)
        {
            // Re-use existing record for re-request
            existingAssociation.Status = AssociationStatus.Pending;
            existingAssociation.ConfirmationToken = confirmationToken;
            existingAssociation.ConfirmationTokenExpiresAt = DateTime.UtcNow.AddDays(7);
            existingAssociation.RequestedAt = DateTime.UtcNow;
            existingAssociation.RejectedAt = null;
            existingAssociation.RejectionReason = null;
            // Note: This would need a repository update method
            association = existingAssociation;
        }
        else
        {
            association = await _associationRepository.CreateAssociationRequestAsync(
                trainerProfileId, businessProfileId, confirmationToken);
        }

        // Build confirmation URL - leads to frontend page where business can choose permissions
        var baseUrl = (_configuration["FrontendConfiguration:WebAppUrl"] ?? "https://spotto.pl").TrimEnd('/');
        var confirmationPageUrl = $"{baseUrl}/trainer-association/{confirmationToken}";

        // Get business email
        var businessEmail = businessProfile.Email;
        if (string.IsNullOrEmpty(businessEmail))
        {
            _logger.LogWarning("Business profile {BusinessProfileId} has no email, cannot send confirmation request",
                businessProfileId);
            throw new BusinessRuleException("Business profile has no email configured. Cannot send confirmation request.");
        }

        // Send confirmation email to business
        try
        {
            await _emailService.SendTrainerAssociationRequestEmailAsync(
                businessEmail,
                businessProfile.DisplayName ?? businessProfile.CompanyName ?? "Business",
                trainerProfile.DisplayName,
                trainerProfile.Email ?? "No email",
                confirmationPageUrl);

            _logger.LogInformation("Association confirmation email sent to {BusinessEmail}", businessEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send association confirmation email to {BusinessEmail}", businessEmail);
            // Don't fail the request - association is created, email can be resent
        }

        return MapToResponseDto(association, trainerProfile, businessProfile);
    }

    public async Task<TrainerBusinessAssociationResponseDto> ProcessConfirmationAsync(
        string token,
        bool confirm,
        string? rejectionReason = null,
        bool canRunOwnTrainings = false,
        bool isEmployee = false)
    {
        _logger.LogInformation("Processing association confirmation. Token: {Token}, Confirm: {Confirm}", token, confirm);

        var association = await _associationRepository.GetByTokenAsync(token);
        if (association == null)
            throw new NotFoundException("Association", "token");

        if (association.Status != AssociationStatus.Pending)
            throw new BusinessRuleException($"Association is not pending. Current status: {association.Status}");

        if (association.ConfirmationTokenExpiresAt.HasValue && association.ConfirmationTokenExpiresAt < DateTime.UtcNow)
            throw new BusinessRuleException("Confirmation link has expired. Please ask the trainer to send a new request.");

        var trainerProfile = association.TrainerProfile ?? _trainerProfileRepository.GetTrainerProfileById(association.TrainerProfileId);
        var businessProfile = association.BusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.BusinessProfileId);

        if (confirm)
        {
            // At least one permission must be granted
            if (!canRunOwnTrainings && !isEmployee)
            {
                throw new BusinessRuleException("At least one permission must be granted (canRunOwnTrainings or isEmployee)");
            }

            // Generate unique color for this association
            var color = await GenerateUniqueColorForTrainerAsync(association.TrainerProfileId);

            association = await _associationRepository.ConfirmAssociationAsync(association.Id, canRunOwnTrainings, isEmployee, color);
            _logger.LogInformation("Association {AssociationId} confirmed with permissions: CanRunOwnTrainings={CanRunOwn}, IsEmployee={IsEmployee}, Color={Color}",
                association.Id, canRunOwnTrainings, isEmployee, color);

            // Send confirmation email to trainer
            if (!string.IsNullOrEmpty(trainerProfile?.Email))
            {
                try
                {
                    await _emailService.SendTrainerAssociationConfirmedEmailAsync(
                        trainerProfile.Email,
                        trainerProfile.DisplayName,
                        businessProfile?.DisplayName ?? businessProfile?.CompanyName ?? "Business");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send association confirmed email to trainer {TrainerEmail}", trainerProfile.Email);
                }
            }
        }
        else
        {
            association = await _associationRepository.RejectAssociationAsync(association.Id, rejectionReason);
            _logger.LogInformation("Association {AssociationId} rejected. Reason: {Reason}", association.Id, rejectionReason);

            // Send rejection email to trainer
            if (!string.IsNullOrEmpty(trainerProfile?.Email))
            {
                try
                {
                    await _emailService.SendTrainerAssociationRejectedEmailAsync(
                        trainerProfile.Email,
                        trainerProfile.DisplayName,
                        businessProfile?.DisplayName ?? businessProfile?.CompanyName ?? "Business",
                        rejectionReason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send association rejected email to trainer {TrainerEmail}", trainerProfile.Email);
                }
            }
        }

        return MapToResponseDto(association, trainerProfile, businessProfile);
    }

    public async Task<List<TrainerBusinessAssociationResponseDto>> GetTrainerAssociationsAsync(Guid trainerProfileId, string? status = null)
    {
        AssociationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AssociationStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var associations = await _associationRepository.GetByTrainerProfileIdAsync(trainerProfileId, statusFilter);
        return associations.Select(a => MapToResponseDto(a, a.TrainerProfile, a.BusinessProfile)).ToList();
    }

    public async Task<List<TrainerBusinessAssociationResponseDto>> GetConfirmedAssociationsForTrainerAsync(Guid trainerProfileId)
    {
        var associations = await _associationRepository.GetConfirmedAssociationsForTrainerAsync(trainerProfileId);
        return associations.Select(a => MapToResponseDto(a, a.TrainerProfile, a.BusinessProfile)).ToList();
    }

    public async Task<List<PendingAssociationRequestDto>> GetPendingRequestsForBusinessAsync(Guid businessProfileId)
    {
        var associations = await _associationRepository.GetPendingRequestsForBusinessAsync(businessProfileId);
        return associations.Select(a => new PendingAssociationRequestDto
        {
            Id = a.Id,
            TrainerProfileId = a.TrainerProfileId,
            TrainerDisplayName = a.TrainerProfile?.DisplayName ?? "Unknown",
            TrainerAvatarUrl = a.TrainerProfile?.AvatarUrl,
            TrainerEmail = a.TrainerProfile?.Email,
            TrainerSpecializations = a.TrainerProfile?.Specializations ?? new List<string>(),
            TrainerHourlyRate = a.TrainerProfile?.HourlyRate ?? 0,
            RequestedAt = a.RequestedAt
        }).ToList();
    }

    public async Task<List<TrainerBusinessAssociationResponseDto>> GetBusinessAssociationsAsync(Guid businessProfileId, string? status = null)
    {
        AssociationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AssociationStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var associations = await _associationRepository.GetByBusinessProfileIdAsync(businessProfileId, statusFilter);
        return associations.Select(a => MapToResponseDto(a, a.TrainerProfile, a.BusinessProfile)).ToList();
    }

    public async Task<bool> CancelAssociationRequestAsync(Guid trainerProfileId, Guid businessProfileId)
    {
        var association = await _associationRepository.GetByTrainerAndBusinessAsync(trainerProfileId, businessProfileId);
        if (association == null)
            return false;

        if (association.Status != AssociationStatus.Pending)
            throw new BusinessRuleException("Can only cancel pending association requests");

        return await _associationRepository.DeleteAssociationAsync(association.Id);
    }

    public async Task<bool> RemoveAssociationAsync(Guid associationId, Guid requestingUserId)
    {
        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            return false;

        // Verify requesting user is either the trainer or business owner
        var trainerProfile = association.TrainerProfile ?? _trainerProfileRepository.GetTrainerProfileById(association.TrainerProfileId);
        var businessProfile = association.BusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.BusinessProfileId);

        var isTrainerOwner = trainerProfile?.UserId == requestingUserId;
        var isBusinessOwner = businessProfile?.UserId == requestingUserId;

        if (!isTrainerOwner && !isBusinessOwner)
            throw new ForbiddenException("You do not have permission to remove this association");

        return await _associationRepository.DeleteAssociationAsync(associationId);
    }

    public async Task<bool> IsAssociationConfirmedAsync(Guid trainerProfileId, Guid businessProfileId)
    {
        return await _associationRepository.IsAssociationConfirmedAsync(trainerProfileId, businessProfileId);
    }

    public async Task<bool> ResendConfirmationEmailAsync(Guid associationId, Guid trainerProfileId)
    {
        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            throw new NotFoundException("Association", associationId.ToString());

        if (association.TrainerProfileId != trainerProfileId)
            throw new ForbiddenException("You can only resend confirmation for your own association requests");

        if (association.Status != AssociationStatus.Pending)
            throw new BusinessRuleException("Can only resend confirmation for pending requests");

        var trainerProfile = association.TrainerProfile ?? _trainerProfileRepository.GetTrainerProfileById(association.TrainerProfileId);
        var businessProfile = association.BusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.BusinessProfileId);

        if (businessProfile == null || string.IsNullOrEmpty(businessProfile.Email))
            throw new BusinessRuleException("Business profile has no email configured");

        // Generate new token and extend expiry
        var newToken = GenerateConfirmationToken();
        var newExpiry = DateTime.UtcNow.AddDays(7);
        await _associationRepository.UpdateConfirmationTokenAsync(association.Id, newToken, newExpiry);

        var baseUrl = (_configuration["FrontendConfiguration:WebAppUrl"] ?? "https://spotto.pl").TrimEnd('/');
        var confirmationPageUrl = $"{baseUrl}/trainer-association/{newToken}";

        await _emailService.SendTrainerAssociationRequestEmailAsync(
            businessProfile.Email,
            businessProfile.DisplayName ?? businessProfile.CompanyName ?? "Business",
            trainerProfile?.DisplayName ?? "Trainer",
            trainerProfile?.Email ?? "No email",
            confirmationPageUrl);

        return true;
    }

    public async Task<TrainerBusinessAssociationResponseDto?> GetByTokenAsync(string token)
    {
        var association = await _associationRepository.GetByTokenAsync(token);
        if (association == null)
            return null;

        var trainerProfile = association.TrainerProfile ?? _trainerProfileRepository.GetTrainerProfileById(association.TrainerProfileId);
        var businessProfile = association.BusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.BusinessProfileId);

        return MapToResponseDto(association, trainerProfile, businessProfile);
    }

    // Business-side actions

    public async Task<TrainerBusinessAssociationResponseDto> UpdateTrainerPricingAsync(
        Guid associationId,
        Guid businessProfileId,
        UpdateTrainerPricingDto dto)
    {
        _logger.LogInformation("Updating pricing for association {AssociationId} by business {BusinessProfileId}",
            associationId, businessProfileId);

        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            throw new NotFoundException("Association", associationId.ToString());

        // Verify the association belongs to this business profile
        if (association.BusinessProfileId != businessProfileId)
            throw new ForbiddenException("This association does not belong to your business profile");

        if (association.Status != AssociationStatus.Confirmed)
            throw new BusinessRuleException("Can only update pricing for confirmed associations");

        if (dto.HourlyRate < 0)
            throw new BusinessRuleException("Hourly rate cannot be negative");

        if (dto.VatRate < 0 || dto.VatRate > 100)
            throw new BusinessRuleException("VAT rate must be between 0 and 100");

        association = await _associationRepository.UpdatePricingAsync(associationId, dto.HourlyRate, dto.VatRate);

        var trainerProfile = association.TrainerProfile ?? _trainerProfileRepository.GetTrainerProfileById(association.TrainerProfileId);
        var businessProfile = association.BusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.BusinessProfileId);

        _logger.LogInformation("Pricing updated for association {AssociationId}: HourlyRate={HourlyRate}, VatRate={VatRate}, GrossRate={GrossRate}",
            associationId, dto.HourlyRate, dto.VatRate, association.GrossHourlyRate);

        return MapToResponseDto(association, trainerProfile, businessProfile);
    }

    public async Task<TrainerBusinessAssociationResponseDto> UpdateAssociationPermissionsAsync(
        Guid associationId,
        Guid businessProfileId,
        UpdateAssociationPermissionsDto dto)
    {
        _logger.LogInformation("Updating permissions for association {AssociationId} by business {BusinessProfileId}",
            associationId, businessProfileId);

        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            throw new NotFoundException("Association", associationId.ToString());

        // Verify the association belongs to this business profile
        if (association.BusinessProfileId != businessProfileId)
            throw new ForbiddenException("This association does not belong to your business profile");

        if (association.Status != AssociationStatus.Confirmed)
            throw new BusinessRuleException("Can only update permissions for confirmed associations");

        // At least one permission must be granted
        if (!dto.CanRunOwnTrainings && !dto.IsEmployee)
            throw new BusinessRuleException("At least one permission must be granted (CanRunOwnTrainings or IsEmployee)");

        // Validate maxNumberOfUsers if provided
        if (dto.MaxNumberOfUsers.HasValue && dto.MaxNumberOfUsers.Value < 1)
            throw new BusinessRuleException("MaxNumberOfUsers must be at least 1");

        association = await _associationRepository.UpdatePermissionsAsync(associationId, dto.CanRunOwnTrainings, dto.IsEmployee, dto.MaxNumberOfUsers);

        var trainerProfile = association.TrainerProfile ?? _trainerProfileRepository.GetTrainerProfileById(association.TrainerProfileId);
        var businessProfile = association.BusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.BusinessProfileId);

        _logger.LogInformation("Permissions updated for association {AssociationId}: CanRunOwnTrainings={CanRunOwn}, IsEmployee={IsEmployee}, MaxNumberOfUsers={MaxUsers}",
            associationId, dto.CanRunOwnTrainings, dto.IsEmployee, dto.MaxNumberOfUsers);

        return MapToResponseDto(association, trainerProfile, businessProfile);
    }

    public async Task<bool> BusinessRemoveAssociationAsync(Guid associationId, Guid businessProfileId)
    {
        _logger.LogInformation("Business {BusinessProfileId} removing association {AssociationId}",
            businessProfileId, associationId);

        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            return false;

        // Verify the association belongs to this business profile
        if (association.BusinessProfileId != businessProfileId)
            throw new ForbiddenException("This association does not belong to your business profile");

        var businessProfile = association.BusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.BusinessProfileId);
        var result = await _associationRepository.DeleteAssociationAsync(associationId);

        if (result)
        {
            _logger.LogInformation("Association {AssociationId} removed by business {BusinessProfileId}",
                associationId, businessProfileId);

            // Notify the trainer
            var trainerProfile = association.TrainerProfile ?? _trainerProfileRepository.GetTrainerProfileById(association.TrainerProfileId);
            if (trainerProfile != null && !string.IsNullOrEmpty(trainerProfile.Email))
            {
                try
                {
                    await _emailService.SendAssociationRemovedEmailAsync(
                        trainerProfile.Email,
                        trainerProfile.DisplayName,
                        businessProfile?.DisplayName ?? businessProfile?.CompanyName ?? "Business");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send association removed email to trainer {TrainerEmail}", trainerProfile.Email);
                }
            }
        }

        return result;
    }

    public async Task<List<TrainerBusinessAssociationResponseDto>> GetAssociationsForBusinessAsync(Guid businessProfileId, string? status = null)
    {
        AssociationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AssociationStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var associations = await _associationRepository.GetByBusinessProfileIdAsync(businessProfileId, statusFilter);
        return associations.Select(a => MapToResponseDto(a, a.TrainerProfile, a.BusinessProfile)).ToList();
    }

    public async Task<List<BusinessAvailableTrainerDto>> GetAvailableTrainersForBusinessAsync(Guid businessProfileId, DateTime date, List<string> timeSlots)
    {
        _logger.LogInformation("Getting available trainers for business {BusinessProfileId} on {Date} for slots {TimeSlots}",
            businessProfileId, date, string.Join(", ", timeSlots));

        // Get trainers with their available slots from repository
        var availableTrainers = _trainerProfileRepository.FindAvailableTrainersForBusiness(businessProfileId, date, timeSlots);

        if (!availableTrainers.Any())
        {
            return new List<BusinessAvailableTrainerDto>();
        }

        // Get associations to enrich with color and pricing info
        var associations = await _associationRepository.GetByBusinessProfileIdAsync(businessProfileId, AssociationStatus.Confirmed);
        var associationsByTrainer = associations.ToDictionary(a => a.TrainerProfileId);

        var result = new List<BusinessAvailableTrainerDto>();

        foreach (var (trainer, availableSlots) in availableTrainers)
        {
            if (!availableSlots.Any())
                continue;

            associationsByTrainer.TryGetValue(trainer.Id, out var association);

            result.Add(new BusinessAvailableTrainerDto
            {
                TrainerProfileId = trainer.Id,
                DisplayName = trainer.DisplayName,
                AvatarUrl = trainer.AvatarUrl,
                Email = trainer.Email,
                Color = association?.Color,
                HourlyRate = association?.HourlyRate,
                VatRate = association?.VatRate,
                GrossHourlyRate = association?.GrossHourlyRate,
                CanRunOwnTrainings = association?.CanRunOwnTrainings ?? false,
                IsEmployee = association?.IsEmployee ?? false,
                MaxNumberOfUsers = association?.MaxNumberOfUsers,
                AvailableTimeSlots = availableSlots
            });
        }

        _logger.LogInformation("Found {Count} available trainers for business {BusinessProfileId}",
            result.Count, businessProfileId);

        return result;
    }

    private string GenerateConfirmationToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .TrimEnd('=');
    }

    private async Task<string> GenerateUniqueColorForTrainerAsync(Guid trainerProfileId)
    {
        var usedColors = await _associationRepository.GetUsedColorsForTrainerAsync(trainerProfileId);
        var usedColorsSet = new HashSet<string>(usedColors, StringComparer.OrdinalIgnoreCase);

        // Find first available color from palette
        foreach (var color in ColorPalette)
        {
            if (!usedColorsSet.Contains(color))
            {
                return color;
            }
        }

        // If all palette colors are used, generate a random one
        // Use HSL to ensure good saturation and lightness
        var random = new Random();
        var hue = random.Next(0, 360);
        var saturation = random.Next(60, 80); // 60-80% saturation
        var lightness = random.Next(55, 70);  // 55-70% lightness

        return HslToHex(hue, saturation, lightness);
    }

    private static string HslToHex(int h, int s, int l)
    {
        double hue = h / 360.0;
        double saturation = s / 100.0;
        double lightness = l / 100.0;

        double r, g, b;

        if (saturation == 0)
        {
            r = g = b = lightness;
        }
        else
        {
            var q = lightness < 0.5
                ? lightness * (1 + saturation)
                : lightness + saturation - lightness * saturation;
            var p = 2 * lightness - q;
            r = HueToRgb(p, q, hue + 1.0 / 3);
            g = HueToRgb(p, q, hue);
            b = HueToRgb(p, q, hue - 1.0 / 3);
        }

        return $"#{(int)(r * 255):X2}{(int)(g * 255):X2}{(int)(b * 255):X2}";
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }

    private TrainerBusinessAssociationResponseDto MapToResponseDto(
        TrainerBusinessAssociation association,
        TrainerProfile? trainerProfile,
        BusinessProfile? businessProfile)
    {
        return new TrainerBusinessAssociationResponseDto
        {
            Id = association.Id,
            TrainerProfileId = association.TrainerProfileId,
            BusinessProfileId = association.BusinessProfileId,
            Status = association.Status.ToString(),
            CanRunOwnTrainings = association.CanRunOwnTrainings,
            IsEmployee = association.IsEmployee,
            Color = association.Color,
            HourlyRate = association.HourlyRate,
            VatRate = association.VatRate,
            GrossHourlyRate = association.GrossHourlyRate,
            MaxNumberOfUsers = association.MaxNumberOfUsers,
            TrainerDisplayName = trainerProfile?.DisplayName,
            TrainerAvatarUrl = trainerProfile?.AvatarUrl,
            TrainerEmail = trainerProfile?.Email,
            BusinessName = businessProfile?.DisplayName ?? businessProfile?.CompanyName,
            BusinessCity = businessProfile?.City,
            BusinessAvatarUrl = businessProfile?.AvatarUrl,
            RequestedAt = association.RequestedAt,
            ConfirmedAt = association.ConfirmedAt,
            RejectedAt = association.RejectedAt,
            RejectionReason = association.RejectionReason
        };
    }
}
