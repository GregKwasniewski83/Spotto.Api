namespace PlaySpace.Domain.DTOs;

/// <summary>
/// Result of KSeF session initialization
/// </summary>
public class KSeFSessionResult
{
    public bool Success { get; set; }
    public string? SessionToken { get; set; } // Access token (JWT) for API calls
    public string? SessionReferenceNumber { get; set; } // Session reference number for invoice endpoints
    public string? ErrorMessage { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Encryption data for KSeF 2.0 (needed for encrypting invoices)
    public byte[]? SymmetricKey { get; set; } // AES-256 key (32 bytes)
    public byte[]? InitializationVector { get; set; } // AES IV (16 bytes)
}

/// <summary>
/// Result of invoice submission to KSeF
/// </summary>
public class KSeFInvoiceSubmissionResult
{
    public bool Success { get; set; }
    public string? KSeFReferenceNumber { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime SubmittedAt { get; set; }
}

/// <summary>
/// Result of invoice status check
/// </summary>
public class KSeFInvoiceStatusResult
{
    public bool Success { get; set; }
    public string Status { get; set; } = string.Empty; // "Accepted", "Rejected", "Pending", etc.
    public string? ErrorMessage { get; set; }
    public string? UPO { get; set; } // Official receipt (Urzędowe Poświadczenie Odbioru)
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>
/// Result of FA XML validation
/// </summary>
public class FAXmlValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
