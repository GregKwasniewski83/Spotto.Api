using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IBusinessParentChildAssociationService
{
    /// <summary>
    /// Child business requests to associate with a parent business profile.
    /// Sends confirmation email to the parent business.
    /// </summary>
    Task<BusinessParentChildAssociationResponseDto> RequestAssociationAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId);

    /// <summary>
    /// Parent business confirms or rejects the association request via token.
    /// When confirming, specify what permissions to grant (TPay, NIP for invoices).
    /// </summary>
    Task<BusinessParentChildAssociationResponseDto> ProcessConfirmationAsync(
        string token,
        bool confirm,
        string? rejectionReason = null,
        bool useParentTPay = false,
        bool useParentNipForInvoices = false);

    /// <summary>
    /// Get all associations for a child business (filtered by status).
    /// </summary>
    Task<List<BusinessParentChildAssociationResponseDto>> GetChildAssociationsAsync(Guid childBusinessProfileId, string? status = null);

    /// <summary>
    /// Get the confirmed parent association for a child business (if any).
    /// A child can only have one confirmed parent at a time.
    /// </summary>
    Task<BusinessParentChildAssociationResponseDto?> GetConfirmedParentForChildAsync(Guid childBusinessProfileId);

    /// <summary>
    /// Get pending association requests for a parent business.
    /// </summary>
    Task<List<PendingChildAssociationRequestDto>> GetPendingRequestsForParentAsync(Guid parentBusinessProfileId);

    /// <summary>
    /// Get all associations for a parent business (child businesses associated with this parent).
    /// </summary>
    Task<List<BusinessParentChildAssociationResponseDto>> GetParentAssociationsAsync(Guid parentBusinessProfileId, string? status = null);

    /// <summary>
    /// Child business cancels their pending association request.
    /// </summary>
    Task<bool> CancelAssociationRequestAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId);

    /// <summary>
    /// Either party removes a confirmed association.
    /// </summary>
    Task<bool> RemoveAssociationAsync(Guid associationId, Guid requestingBusinessProfileId);

    /// <summary>
    /// Check if a child business has a confirmed association with a parent.
    /// </summary>
    Task<bool> IsAssociationConfirmedAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId);

    /// <summary>
    /// Resend confirmation email for a pending association.
    /// </summary>
    Task<bool> ResendConfirmationEmailAsync(Guid associationId, Guid childBusinessProfileId);

    /// <summary>
    /// Get association details by confirmation token (for frontend confirmation page).
    /// </summary>
    Task<BusinessParentChildAssociationResponseDto?> GetByTokenAsync(string token);

    // Parent-side actions

    /// <summary>
    /// Parent business updates permissions for a confirmed association.
    /// </summary>
    Task<BusinessParentChildAssociationResponseDto> UpdateChildPermissionsAsync(
        Guid associationId,
        Guid parentBusinessProfileId,
        UpdateChildAssociationPermissionsDto dto);

    /// <summary>
    /// Parent business removes a child association.
    /// </summary>
    Task<bool> ParentRemoveAssociationAsync(Guid associationId, Guid parentBusinessProfileId);

    // Helper methods for payment and invoice processing

    /// <summary>
    /// Gets the effective TPay merchant ID for a business profile.
    /// If child uses parent's TPay, returns parent's merchant ID.
    /// </summary>
    Task<string?> GetEffectiveTPayMerchantIdAsync(Guid businessProfileId);

    /// <summary>
    /// Gets the effective seller info (NIP, company details) for invoices.
    /// If child uses parent's NIP for invoices, returns parent's details.
    /// </summary>
    Task<(string? Nip, string? CompanyName, string? Address, string? City, string? PostalCode)?> GetEffectiveSellerInfoAsync(Guid businessProfileId);

    /// <summary>
    /// Checks if a business profile should use parent's TPay for payments.
    /// </summary>
    Task<bool> ShouldUseParentTPayAsync(Guid businessProfileId);

    /// <summary>
    /// Checks if a business profile should use parent's NIP for invoices.
    /// </summary>
    Task<bool> ShouldUseParentNipForInvoicesAsync(Guid businessProfileId);
}
