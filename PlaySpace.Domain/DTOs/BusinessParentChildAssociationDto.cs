namespace PlaySpace.Domain.DTOs;

/// <summary>
/// DTO for child business to request association with a parent business.
/// </summary>
public class RequestParentAssociationDto
{
    /// <summary>
    /// The ID of the parent business profile to associate with.
    /// </summary>
    public required Guid ParentBusinessProfileId { get; set; }
}

/// <summary>
/// DTO for parent business to confirm or reject an association request.
/// </summary>
public class ConfirmParentChildAssociationDto
{
    /// <summary>
    /// The confirmation token received via email.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// True to confirm, false to reject.
    /// </summary>
    public bool Confirm { get; set; } = true;

    /// <summary>
    /// Reason for rejection (only used when Confirm = false).
    /// </summary>
    public string? RejectionReason { get; set; }

    // Permissions (only used when Confirm = true)

    /// <summary>
    /// Allow child business to use parent's TPay integration for payments.
    /// </summary>
    public bool UseParentTPay { get; set; } = false;

    /// <summary>
    /// Allow child business to use parent's NIP and company details for KSeF invoices.
    /// </summary>
    public bool UseParentNipForInvoices { get; set; } = false;
}

/// <summary>
/// Response DTO for parent-child association.
/// </summary>
public class BusinessParentChildAssociationResponseDto
{
    public Guid Id { get; set; }
    public Guid ChildBusinessProfileId { get; set; }
    public Guid ParentBusinessProfileId { get; set; }
    public string Status { get; set; } = string.Empty;

    // Permissions
    public bool UseParentTPay { get; set; }
    public bool UseParentNipForInvoices { get; set; }

    // Child business info
    public string? ChildBusinessName { get; set; }
    public string? ChildBusinessDisplayName { get; set; }
    public string? ChildBusinessCity { get; set; }
    public string? ChildBusinessAvatarUrl { get; set; }
    public string? ChildBusinessNip { get; set; }

    // Parent business info
    public string? ParentBusinessName { get; set; }
    public string? ParentBusinessDisplayName { get; set; }
    public string? ParentBusinessCity { get; set; }
    public string? ParentBusinessAvatarUrl { get; set; }
    public string? ParentBusinessNip { get; set; }
    public bool ParentHasTPay { get; set; }

    // Timestamps
    public DateTime RequestedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// DTO for pending association requests shown to parent business.
/// </summary>
public class PendingChildAssociationRequestDto
{
    public Guid Id { get; set; }
    public Guid ChildBusinessProfileId { get; set; }
    public string ChildBusinessName { get; set; } = string.Empty;
    public string? ChildBusinessDisplayName { get; set; }
    public string? ChildBusinessAvatarUrl { get; set; }
    public string? ChildBusinessCity { get; set; }
    public string? ChildBusinessNip { get; set; }
    public string? ChildBusinessEmail { get; set; }
    public DateTime RequestedAt { get; set; }
}

/// <summary>
/// DTO for parent business to update permissions for a confirmed association.
/// </summary>
public class UpdateChildAssociationPermissionsDto
{
    /// <summary>
    /// Allow child business to use parent's TPay integration for payments.
    /// </summary>
    public bool UseParentTPay { get; set; }

    /// <summary>
    /// Allow child business to use parent's NIP and company details for KSeF invoices.
    /// </summary>
    public bool UseParentNipForInvoices { get; set; }
}
