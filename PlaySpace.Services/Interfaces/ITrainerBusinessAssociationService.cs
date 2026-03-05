using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface ITrainerBusinessAssociationService
{
    /// <summary>
    /// Trainer requests to associate with a business profile.
    /// Sends confirmation email to the business.
    /// </summary>
    Task<TrainerBusinessAssociationResponseDto> RequestAssociationAsync(Guid trainerProfileId, Guid businessProfileId);

    /// <summary>
    /// Business confirms or rejects the association request via token.
    /// When confirming, specify what the trainer is allowed to do.
    /// </summary>
    Task<TrainerBusinessAssociationResponseDto> ProcessConfirmationAsync(
        string token,
        bool confirm,
        string? rejectionReason = null,
        bool canRunOwnTrainings = false,
        bool isEmployee = false);

    /// <summary>
    /// Get all associations for a trainer (filtered by status).
    /// </summary>
    Task<List<TrainerBusinessAssociationResponseDto>> GetTrainerAssociationsAsync(Guid trainerProfileId, string? status = null);

    /// <summary>
    /// Get only confirmed associations for a trainer (for display under 'Powiązane firmy').
    /// </summary>
    Task<List<TrainerBusinessAssociationResponseDto>> GetConfirmedAssociationsForTrainerAsync(Guid trainerProfileId);

    /// <summary>
    /// Get pending association requests for a business.
    /// </summary>
    Task<List<PendingAssociationRequestDto>> GetPendingRequestsForBusinessAsync(Guid businessProfileId);

    /// <summary>
    /// Get all associations for a business.
    /// </summary>
    Task<List<TrainerBusinessAssociationResponseDto>> GetBusinessAssociationsAsync(Guid businessProfileId, string? status = null);

    /// <summary>
    /// Trainer cancels their pending association request.
    /// </summary>
    Task<bool> CancelAssociationRequestAsync(Guid trainerProfileId, Guid businessProfileId);

    /// <summary>
    /// Either party removes a confirmed association.
    /// </summary>
    Task<bool> RemoveAssociationAsync(Guid associationId, Guid requestingUserId);

    /// <summary>
    /// Check if a trainer has a confirmed association with a business.
    /// </summary>
    Task<bool> IsAssociationConfirmedAsync(Guid trainerProfileId, Guid businessProfileId);

    /// <summary>
    /// Resend confirmation email for a pending association.
    /// </summary>
    Task<bool> ResendConfirmationEmailAsync(Guid associationId, Guid trainerProfileId);

    /// <summary>
    /// Get association details by confirmation token (for frontend confirmation page).
    /// </summary>
    Task<TrainerBusinessAssociationResponseDto?> GetByTokenAsync(string token);

    // Business-side actions

    /// <summary>
    /// Business updates trainer's pricing for this association.
    /// </summary>
    Task<TrainerBusinessAssociationResponseDto> UpdateTrainerPricingAsync(Guid associationId, Guid businessProfileId, UpdateTrainerPricingDto dto);

    /// <summary>
    /// Business updates association permissions.
    /// </summary>
    Task<TrainerBusinessAssociationResponseDto> UpdateAssociationPermissionsAsync(Guid associationId, Guid businessProfileId, UpdateAssociationPermissionsDto dto);

    /// <summary>
    /// Business removes a trainer association.
    /// </summary>
    Task<bool> BusinessRemoveAssociationAsync(Guid associationId, Guid businessProfileId);

    /// <summary>
    /// Get all associations for a business (trainers associated with this business).
    /// </summary>
    Task<List<TrainerBusinessAssociationResponseDto>> GetAssociationsForBusinessAsync(Guid businessProfileId, string? status = null);

    /// <summary>
    /// Get available trainers for a business profile at a specific date and timeslots.
    /// Only returns trainers with slots assigned to this specific business.
    /// </summary>
    Task<List<BusinessAvailableTrainerDto>> GetAvailableTrainersForBusinessAsync(Guid businessProfileId, DateTime date, List<string> timeSlots);
}
