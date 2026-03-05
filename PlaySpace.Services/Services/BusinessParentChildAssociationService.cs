using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class BusinessParentChildAssociationService : IBusinessParentChildAssociationService
{
    private readonly IBusinessParentChildAssociationRepository _associationRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BusinessParentChildAssociationService> _logger;

    public BusinessParentChildAssociationService(
        IBusinessParentChildAssociationRepository associationRepository,
        IBusinessProfileRepository businessProfileRepository,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<BusinessParentChildAssociationService> logger)
    {
        _associationRepository = associationRepository;
        _businessProfileRepository = businessProfileRepository;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BusinessParentChildAssociationResponseDto> RequestAssociationAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId)
    {
        _logger.LogInformation("Child business {ChildBusinessProfileId} requesting association with parent {ParentBusinessProfileId}",
            childBusinessProfileId, parentBusinessProfileId);

        // Verify child business exists
        var childProfile = _businessProfileRepository.GetBusinessProfileById(childBusinessProfileId);
        if (childProfile == null)
            throw new NotFoundException("BusinessProfile", childBusinessProfileId.ToString());

        // Verify parent business exists
        var parentProfile = _businessProfileRepository.GetBusinessProfileById(parentBusinessProfileId);
        if (parentProfile == null)
            throw new NotFoundException("BusinessProfile", parentBusinessProfileId.ToString());

        // Cannot associate with self
        if (childBusinessProfileId == parentBusinessProfileId)
            throw new BusinessRuleException("A business cannot associate with itself as parent");

        // Check if child already has a confirmed parent
        var existingConfirmedParent = await _associationRepository.GetConfirmedAssociationForChildAsync(childBusinessProfileId);
        if (existingConfirmedParent != null)
            throw new BusinessRuleException("This business already has a confirmed parent association. Remove the existing association first.");

        // Check if association already exists with this specific parent
        var existingAssociation = await _associationRepository.GetByChildAndParentAsync(childBusinessProfileId, parentBusinessProfileId);
        if (existingAssociation != null)
        {
            if (existingAssociation.Status == ParentChildAssociationStatus.Pending)
                throw new BusinessRuleException("Association request already pending for this parent business");

            if (existingAssociation.Status == ParentChildAssociationStatus.Confirmed)
                throw new BusinessRuleException("Association with this parent business already exists");

            // If rejected, we allow re-requesting (but for simplicity, delete and create new)
            if (existingAssociation.Status == ParentChildAssociationStatus.Rejected)
            {
                _logger.LogInformation("Re-requesting association after previous rejection");
                await _associationRepository.DeleteAssociationAsync(existingAssociation.Id);
            }
        }

        // Generate confirmation token
        var confirmationToken = GenerateConfirmationToken();

        // Create association
        var association = await _associationRepository.CreateAssociationRequestAsync(
            childBusinessProfileId, parentBusinessProfileId, confirmationToken);

        // Build confirmation URL - leads to frontend page where parent can choose permissions
        var baseUrl = (_configuration["FrontendConfiguration:WebAppUrl"] ?? "https://spotto.pl").TrimEnd('/');
        var confirmationPageUrl = $"{baseUrl}/business-association/{confirmationToken}";

        // Get parent email
        var parentEmail = parentProfile.Email;
        if (string.IsNullOrEmpty(parentEmail))
        {
            _logger.LogWarning("Parent business profile {ParentBusinessProfileId} has no email, cannot send confirmation request",
                parentBusinessProfileId);
            throw new BusinessRuleException("Parent business profile has no email configured. Cannot send confirmation request.");
        }

        // Send confirmation email to parent business
        try
        {
            await _emailService.SendChildBusinessAssociationRequestEmailAsync(
                parentEmail,
                parentProfile.DisplayName ?? parentProfile.CompanyName ?? "Business",
                childProfile.DisplayName ?? childProfile.CompanyName ?? "Child Business",
                childProfile.Email ?? "No email",
                childProfile.Nip,
                confirmationPageUrl);

            _logger.LogInformation("Association confirmation email sent to parent {ParentEmail}", parentEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send association confirmation email to {ParentEmail}", parentEmail);
            // Don't fail the request - association is created, email can be resent
        }

        return MapToResponseDto(association, childProfile, parentProfile);
    }

    public async Task<BusinessParentChildAssociationResponseDto> ProcessConfirmationAsync(
        string token,
        bool confirm,
        string? rejectionReason = null,
        bool useParentTPay = false,
        bool useParentNipForInvoices = false)
    {
        _logger.LogInformation("Processing association confirmation. Token: {Token}, Confirm: {Confirm}", token, confirm);

        var association = await _associationRepository.GetByTokenAsync(token);
        if (association == null)
            throw new NotFoundException("Association", "token");

        if (association.Status != ParentChildAssociationStatus.Pending)
            throw new BusinessRuleException($"Association is not pending. Current status: {association.Status}");

        if (association.ConfirmationTokenExpiresAt.HasValue && association.ConfirmationTokenExpiresAt < DateTime.UtcNow)
            throw new BusinessRuleException("Confirmation link has expired. Please ask the child business to send a new request.");

        var childProfile = association.ChildBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ChildBusinessProfileId);
        var parentProfile = association.ParentBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ParentBusinessProfileId);

        if (confirm)
        {
            // At least one permission should typically be granted, but we allow none
            // (business might just want association without sharing resources)

            // Validate that parent has TPay if granting TPay permission
            if (useParentTPay && string.IsNullOrEmpty(parentProfile?.TPayMerchantId))
            {
                throw new BusinessRuleException("Cannot grant TPay permission - parent business does not have TPay configured");
            }

            // Validate that parent has NIP if granting NIP permission
            if (useParentNipForInvoices && string.IsNullOrEmpty(parentProfile?.Nip))
            {
                throw new BusinessRuleException("Cannot grant NIP for invoices permission - parent business does not have NIP configured");
            }

            association = await _associationRepository.ConfirmAssociationAsync(association.Id, useParentTPay, useParentNipForInvoices);
            _logger.LogInformation("Association {AssociationId} confirmed with permissions: UseParentTPay={UseTPay}, UseParentNipForInvoices={UseNip}",
                association.Id, useParentTPay, useParentNipForInvoices);

            // Update child business profile with parent relationship
            await _businessProfileRepository.UpdateParentChildRelationshipAsync(
                association.ChildBusinessProfileId,
                association.ParentBusinessProfileId,
                useParentTPay,
                useParentNipForInvoices);
            _logger.LogInformation("Updated child profile {ChildProfileId} with parent relationship", association.ChildBusinessProfileId);

            // Send confirmation email to child business
            if (!string.IsNullOrEmpty(childProfile?.Email))
            {
                try
                {
                    await _emailService.SendChildBusinessAssociationConfirmedEmailAsync(
                        childProfile.Email,
                        childProfile.DisplayName ?? childProfile.CompanyName ?? "Business",
                        parentProfile?.DisplayName ?? parentProfile?.CompanyName ?? "Parent Business",
                        useParentTPay,
                        useParentNipForInvoices);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send association confirmed email to child business {ChildEmail}", childProfile.Email);
                }
            }
        }
        else
        {
            association = await _associationRepository.RejectAssociationAsync(association.Id, rejectionReason);
            _logger.LogInformation("Association {AssociationId} rejected. Reason: {Reason}", association.Id, rejectionReason);

            // Send rejection email to child business
            if (!string.IsNullOrEmpty(childProfile?.Email))
            {
                try
                {
                    await _emailService.SendChildBusinessAssociationRejectedEmailAsync(
                        childProfile.Email,
                        childProfile.DisplayName ?? childProfile.CompanyName ?? "Business",
                        parentProfile?.DisplayName ?? parentProfile?.CompanyName ?? "Parent Business",
                        rejectionReason);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send association rejected email to child business {ChildEmail}", childProfile.Email);
                }
            }
        }

        return MapToResponseDto(association, childProfile, parentProfile);
    }

    public async Task<List<BusinessParentChildAssociationResponseDto>> GetChildAssociationsAsync(Guid childBusinessProfileId, string? status = null)
    {
        ParentChildAssociationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ParentChildAssociationStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var associations = await _associationRepository.GetByChildBusinessProfileIdAsync(childBusinessProfileId, statusFilter);
        return associations.Select(a => MapToResponseDto(a, a.ChildBusinessProfile, a.ParentBusinessProfile)).ToList();
    }

    public async Task<BusinessParentChildAssociationResponseDto?> GetConfirmedParentForChildAsync(Guid childBusinessProfileId)
    {
        var association = await _associationRepository.GetConfirmedAssociationForChildAsync(childBusinessProfileId);
        if (association == null)
            return null;

        var childProfile = association.ChildBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ChildBusinessProfileId);
        var parentProfile = association.ParentBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ParentBusinessProfileId);

        return MapToResponseDto(association, childProfile, parentProfile);
    }

    public async Task<List<PendingChildAssociationRequestDto>> GetPendingRequestsForParentAsync(Guid parentBusinessProfileId)
    {
        var associations = await _associationRepository.GetPendingRequestsForParentAsync(parentBusinessProfileId);
        return associations.Select(a => new PendingChildAssociationRequestDto
        {
            Id = a.Id,
            ChildBusinessProfileId = a.ChildBusinessProfileId,
            ChildBusinessName = a.ChildBusinessProfile?.CompanyName ?? "Unknown",
            ChildBusinessDisplayName = a.ChildBusinessProfile?.DisplayName,
            ChildBusinessAvatarUrl = a.ChildBusinessProfile?.AvatarUrl,
            ChildBusinessCity = a.ChildBusinessProfile?.City,
            ChildBusinessNip = a.ChildBusinessProfile?.Nip,
            ChildBusinessEmail = a.ChildBusinessProfile?.Email,
            RequestedAt = a.RequestedAt
        }).ToList();
    }

    public async Task<List<BusinessParentChildAssociationResponseDto>> GetParentAssociationsAsync(Guid parentBusinessProfileId, string? status = null)
    {
        ParentChildAssociationStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ParentChildAssociationStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var associations = await _associationRepository.GetByParentBusinessProfileIdAsync(parentBusinessProfileId, statusFilter);
        return associations.Select(a => MapToResponseDto(a, a.ChildBusinessProfile, a.ParentBusinessProfile)).ToList();
    }

    public async Task<bool> CancelAssociationRequestAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId)
    {
        var association = await _associationRepository.GetByChildAndParentAsync(childBusinessProfileId, parentBusinessProfileId);
        if (association == null)
            return false;

        if (association.Status != ParentChildAssociationStatus.Pending)
            throw new BusinessRuleException("Can only cancel pending association requests");

        return await _associationRepository.DeleteAssociationAsync(association.Id);
    }

    public async Task<bool> RemoveAssociationAsync(Guid associationId, Guid requestingBusinessProfileId)
    {
        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            return false;

        // Verify requesting business is either the child or parent
        var isChild = association.ChildBusinessProfileId == requestingBusinessProfileId;
        var isParent = association.ParentBusinessProfileId == requestingBusinessProfileId;

        if (!isChild && !isParent)
            throw new ForbiddenException("You do not have permission to remove this association");

        return await _associationRepository.DeleteAssociationAsync(associationId);
    }

    public async Task<bool> IsAssociationConfirmedAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId)
    {
        return await _associationRepository.IsAssociationConfirmedAsync(childBusinessProfileId, parentBusinessProfileId);
    }

    public async Task<bool> ResendConfirmationEmailAsync(Guid associationId, Guid childBusinessProfileId)
    {
        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            throw new NotFoundException("Association", associationId.ToString());

        if (association.ChildBusinessProfileId != childBusinessProfileId)
            throw new ForbiddenException("You can only resend confirmation for your own association requests");

        if (association.Status != ParentChildAssociationStatus.Pending)
            throw new BusinessRuleException("Can only resend confirmation for pending requests");

        var childProfile = association.ChildBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ChildBusinessProfileId);
        var parentProfile = association.ParentBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ParentBusinessProfileId);

        if (parentProfile == null || string.IsNullOrEmpty(parentProfile.Email))
            throw new BusinessRuleException("Parent business profile has no email configured");

        // Generate new token (the repository would need an update method, for now we'll use the existing token)
        var baseUrl = (_configuration["FrontendConfiguration:WebAppUrl"] ?? "https://spotto.pl").TrimEnd('/');
        var confirmationPageUrl = $"{baseUrl}/business-association/{association.ConfirmationToken}";

        await _emailService.SendChildBusinessAssociationRequestEmailAsync(
            parentProfile.Email,
            parentProfile.DisplayName ?? parentProfile.CompanyName ?? "Business",
            childProfile?.DisplayName ?? childProfile?.CompanyName ?? "Child Business",
            childProfile?.Email ?? "No email",
            childProfile?.Nip,
            confirmationPageUrl);

        return true;
    }

    public async Task<BusinessParentChildAssociationResponseDto?> GetByTokenAsync(string token)
    {
        var association = await _associationRepository.GetByTokenAsync(token);
        if (association == null)
            return null;

        var childProfile = association.ChildBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ChildBusinessProfileId);
        var parentProfile = association.ParentBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ParentBusinessProfileId);

        return MapToResponseDto(association, childProfile, parentProfile);
    }

    public async Task<BusinessParentChildAssociationResponseDto> UpdateChildPermissionsAsync(
        Guid associationId,
        Guid parentBusinessProfileId,
        UpdateChildAssociationPermissionsDto dto)
    {
        _logger.LogInformation("Updating permissions for association {AssociationId} by parent {ParentBusinessProfileId}",
            associationId, parentBusinessProfileId);

        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            throw new NotFoundException("Association", associationId.ToString());

        // Verify the association belongs to this parent profile
        if (association.ParentBusinessProfileId != parentBusinessProfileId)
            throw new ForbiddenException("This association does not belong to your business profile");

        if (association.Status != ParentChildAssociationStatus.Confirmed)
            throw new BusinessRuleException("Can only update permissions for confirmed associations");

        var parentProfile = association.ParentBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ParentBusinessProfileId);

        // Validate that parent has TPay if granting TPay permission
        if (dto.UseParentTPay && string.IsNullOrEmpty(parentProfile?.TPayMerchantId))
        {
            throw new BusinessRuleException("Cannot grant TPay permission - you do not have TPay configured");
        }

        // Validate that parent has NIP if granting NIP permission
        if (dto.UseParentNipForInvoices && string.IsNullOrEmpty(parentProfile?.Nip))
        {
            throw new BusinessRuleException("Cannot grant NIP for invoices permission - you do not have NIP configured");
        }

        association = await _associationRepository.UpdatePermissionsAsync(associationId, dto.UseParentTPay, dto.UseParentNipForInvoices);

        var childProfile = association.ChildBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ChildBusinessProfileId);

        // Update child business profile with new permissions
        await _businessProfileRepository.UpdateParentChildRelationshipAsync(
            association.ChildBusinessProfileId,
            association.ParentBusinessProfileId,
            dto.UseParentTPay,
            dto.UseParentNipForInvoices);

        _logger.LogInformation("Permissions updated for association {AssociationId}: UseParentTPay={UseTPay}, UseParentNipForInvoices={UseNip}",
            associationId, dto.UseParentTPay, dto.UseParentNipForInvoices);

        // Notify child business of permission changes
        if (!string.IsNullOrEmpty(childProfile?.Email))
        {
            try
            {
                await _emailService.SendChildBusinessPermissionsUpdatedEmailAsync(
                    childProfile.Email,
                    childProfile.DisplayName ?? childProfile.CompanyName ?? "Business",
                    parentProfile?.DisplayName ?? parentProfile?.CompanyName ?? "Parent Business",
                    dto.UseParentTPay,
                    dto.UseParentNipForInvoices);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send permissions updated email to child business {ChildEmail}", childProfile.Email);
            }
        }

        return MapToResponseDto(association, childProfile, parentProfile);
    }

    public async Task<bool> ParentRemoveAssociationAsync(Guid associationId, Guid parentBusinessProfileId)
    {
        _logger.LogInformation("Parent {ParentBusinessProfileId} removing association {AssociationId}",
            parentBusinessProfileId, associationId);

        var association = await _associationRepository.GetByIdAsync(associationId);
        if (association == null)
            return false;

        // Verify the association belongs to this parent profile
        if (association.ParentBusinessProfileId != parentBusinessProfileId)
            throw new ForbiddenException("This association does not belong to your business profile");

        var parentProfile = association.ParentBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(association.ParentBusinessProfileId);
        var childProfileId = association.ChildBusinessProfileId;
        var result = await _associationRepository.DeleteAssociationAsync(associationId);

        if (result)
        {
            // Clear parent-child relationship on the child profile
            await _businessProfileRepository.ClearParentChildRelationshipAsync(childProfileId);

            _logger.LogInformation("Association {AssociationId} removed by parent {ParentBusinessProfileId}",
                associationId, parentBusinessProfileId);

            // Notify the child business
            var childProfile = association.ChildBusinessProfile ?? _businessProfileRepository.GetBusinessProfileById(childProfileId);
            if (childProfile != null && !string.IsNullOrEmpty(childProfile.Email))
            {
                try
                {
                    await _emailService.SendChildBusinessAssociationRemovedEmailAsync(
                        childProfile.Email,
                        childProfile.DisplayName ?? childProfile.CompanyName ?? "Business",
                        parentProfile?.DisplayName ?? parentProfile?.CompanyName ?? "Parent Business");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send association removed email to child business {ChildEmail}", childProfile.Email);
                }
            }
        }

        return result;
    }

    // Helper methods for payment and invoice processing

    public async Task<string?> GetEffectiveTPayMerchantIdAsync(Guid businessProfileId)
    {
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (businessProfile == null)
            return null;

        // Check if this business has a confirmed parent association with TPay permission
        var parentAssociation = await _associationRepository.GetConfirmedAssociationForChildAsync(businessProfileId);
        if (parentAssociation != null && parentAssociation.UseParentTPay)
        {
            var parentProfile = parentAssociation.ParentBusinessProfile ??
                _businessProfileRepository.GetBusinessProfileById(parentAssociation.ParentBusinessProfileId);
            return parentProfile?.TPayMerchantId;
        }

        return businessProfile.TPayMerchantId;
    }

    public async Task<(string? Nip, string? CompanyName, string? Address, string? City, string? PostalCode)?> GetEffectiveSellerInfoAsync(Guid businessProfileId)
    {
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (businessProfile == null)
            return null;

        // Check if this business has a confirmed parent association with NIP permission
        var parentAssociation = await _associationRepository.GetConfirmedAssociationForChildAsync(businessProfileId);
        if (parentAssociation != null && parentAssociation.UseParentNipForInvoices)
        {
            var parentProfile = parentAssociation.ParentBusinessProfile ??
                _businessProfileRepository.GetBusinessProfileById(parentAssociation.ParentBusinessProfileId);
            if (parentProfile != null)
            {
                return (parentProfile.Nip, parentProfile.CompanyName, parentProfile.Address, parentProfile.City, parentProfile.PostalCode);
            }
        }

        return (businessProfile.Nip, businessProfile.CompanyName, businessProfile.Address, businessProfile.City, businessProfile.PostalCode);
    }

    public async Task<bool> ShouldUseParentTPayAsync(Guid businessProfileId)
    {
        var parentAssociation = await _associationRepository.GetConfirmedAssociationForChildAsync(businessProfileId);
        return parentAssociation != null && parentAssociation.UseParentTPay;
    }

    public async Task<bool> ShouldUseParentNipForInvoicesAsync(Guid businessProfileId)
    {
        var parentAssociation = await _associationRepository.GetConfirmedAssociationForChildAsync(businessProfileId);
        return parentAssociation != null && parentAssociation.UseParentNipForInvoices;
    }

    private string GenerateConfirmationToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .TrimEnd('=');
    }

    private BusinessParentChildAssociationResponseDto MapToResponseDto(
        BusinessParentChildAssociation association,
        BusinessProfile? childProfile,
        BusinessProfile? parentProfile)
    {
        return new BusinessParentChildAssociationResponseDto
        {
            Id = association.Id,
            ChildBusinessProfileId = association.ChildBusinessProfileId,
            ParentBusinessProfileId = association.ParentBusinessProfileId,
            Status = association.Status.ToString(),
            UseParentTPay = association.UseParentTPay,
            UseParentNipForInvoices = association.UseParentNipForInvoices,
            ChildBusinessName = childProfile?.CompanyName,
            ChildBusinessDisplayName = childProfile?.DisplayName,
            ChildBusinessCity = childProfile?.City,
            ChildBusinessAvatarUrl = childProfile?.AvatarUrl,
            ChildBusinessNip = childProfile?.Nip,
            ParentBusinessName = parentProfile?.CompanyName,
            ParentBusinessDisplayName = parentProfile?.DisplayName,
            ParentBusinessCity = parentProfile?.City,
            ParentBusinessAvatarUrl = parentProfile?.AvatarUrl,
            ParentBusinessNip = parentProfile?.Nip,
            ParentHasTPay = !string.IsNullOrEmpty(parentProfile?.TPayMerchantId),
            RequestedAt = association.RequestedAt,
            ConfirmedAt = association.ConfirmedAt,
            RejectedAt = association.RejectedAt,
            RejectionReason = association.RejectionReason
        };
    }
}
