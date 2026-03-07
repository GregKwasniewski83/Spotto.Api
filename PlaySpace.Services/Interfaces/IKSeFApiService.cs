using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

/// <summary>
/// Service interface for KSeF (Polish National e-Invoice System) API integration
/// Wraps the KSEFAPI.KSEFAPIClient library for easier testing and abstraction
/// </summary>
public interface IKSeFApiService
{
    /// <summary>
    /// Initialize a session with KSeF API using business credentials
    /// </summary>
    /// <param name="nip">Business NIP (tax identification number)</param>
    /// <param name="token">KSeF API token for the business</param>
    /// <param name="environment">"Test" or "Production"</param>
    /// <returns>Session token for subsequent API calls</returns>
    Task<KSeFSessionResult> InitializeSessionAsync(string nip, string token, string environment);

    /// <summary>
    /// Send an invoice to KSeF (KSeF 2.0 with AES-256 encryption)
    /// </summary>
    /// <param name="sessionToken">Active session token (access token)</param>
    /// <param name="sessionReferenceNumber">Session reference number from initialization</param>
    /// <param name="invoiceXml">FA (Faktura) XML content</param>
    /// <param name="symmetricKey">AES-256 key (32 bytes) from session initialization</param>
    /// <param name="initializationVector">AES IV (16 bytes) from session initialization</param>
    /// <param name="environment">"Test" or "Production"</param>
    /// <returns>Result containing KSeF reference number and status</returns>
    Task<KSeFInvoiceSubmissionResult> SendInvoiceAsync(string sessionToken, string sessionReferenceNumber, string invoiceXml, byte[] symmetricKey, byte[] initializationVector, string environment);

    /// <summary>
    /// Check the status of a submitted invoice
    /// </summary>
    /// <param name="sessionToken">Active session token</param>
    /// <param name="ksefReferenceNumber">KSeF reference number from invoice submission</param>
    /// <param name="environment">"Test" or "Production"</param>
    /// <returns>Current status of the invoice in KSeF</returns>
    Task<KSeFInvoiceStatusResult> CheckInvoiceStatusAsync(string sessionToken, string ksefReferenceNumber, string environment);

    /// <summary>
    /// Close an active KSeF session
    /// </summary>
    /// <param name="sessionToken">Active session token to close</param>
    /// <param name="environment">"Test" or "Production"</param>
    Task CloseSessionAsync(string sessionToken, string environment);

    /// <summary>
    /// Refresh an access token using a refresh token
    /// </summary>
    /// <param name="refreshToken">The refresh token from session initialization</param>
    /// <param name="environment">"Test" or "Production"</param>
    /// <returns>New access token with extended validity</returns>
    Task<KSeFTokenRefreshResult> RefreshTokenAsync(string refreshToken, string environment);

    /// <summary>
    /// Test connection to KSeF API with business credentials
    /// </summary>
    /// <param name="nip">Business NIP</param>
    /// <param name="token">KSeF API token</param>
    /// <param name="environment">"Test" or "Production"</param>
    /// <returns>True if connection successful</returns>
    Task<bool> TestConnectionAsync(string nip, string token, string environment);
}
