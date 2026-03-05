namespace PlaySpace.Domain.Models;

/// <summary>
/// Represents a parent-child relationship between two business profiles.
/// Child business can operate under parent's TPay and/or NIP for invoices.
/// </summary>
public class BusinessParentChildAssociation
{
    public Guid Id { get; set; }

    /// <summary>
    /// The child business profile requesting to operate under a parent.
    /// </summary>
    public Guid ChildBusinessProfileId { get; set; }
    public BusinessProfile? ChildBusinessProfile { get; set; }

    /// <summary>
    /// The parent business profile that will provide TPay/NIP services.
    /// </summary>
    public Guid ParentBusinessProfileId { get; set; }
    public BusinessProfile? ParentBusinessProfile { get; set; }

    /// <summary>
    /// Current status of the association request.
    /// </summary>
    public ParentChildAssociationStatus Status { get; set; } = ParentChildAssociationStatus.Pending;

    /// <summary>
    /// Token for email confirmation by parent business.
    /// </summary>
    public string? ConfirmationToken { get; set; }

    /// <summary>
    /// When the confirmation token expires.
    /// </summary>
    public DateTime? ConfirmationTokenExpiresAt { get; set; }

    // Permissions (set by parent business on confirmation)

    /// <summary>
    /// If true, child business uses parent's TPay integration for payments.
    /// </summary>
    public bool UseParentTPay { get; set; } = false;

    /// <summary>
    /// If true, child business uses parent's NIP and company details for KSeF invoices.
    /// </summary>
    public bool UseParentNipForInvoices { get; set; } = false;

    // Timestamps
    public DateTime RequestedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public enum ParentChildAssociationStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2
}
