using PlaySpace.Services.Interfaces;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.Configuration;
using PlaySpace.Domain.DTOs;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace PlaySpace.Services.Services;

/// <summary>
/// KSeF 2.0 API Service - Complete implementation with RSA-OAEP encryption
///
/// Official Documentation:
/// - KSeF API v2: https://ksef-test.mf.gov.pl/docs/v2/index.html
/// - GitHub Docs: https://github.com/CIRFMF/ksef-docs/blob/main/uwierzytelnianie.md
///
/// Authentication Flow (KSeF 2.0):
/// 1. POST /auth/challenge → get challenge + timestamp
/// 2. Download RSA public key (cached)
/// 3. Encrypt token with RSA-OAEP: "{token}|{timestampMs}"
/// 4. POST /auth/ksef-token → get authenticationToken
/// 5. POST /auth/token/redeem → get accessToken + refreshToken
/// 6. Use accessToken in Authorization: Bearer header
/// </summary>
public class KSeFApiService_V2 : IKSeFApiService
{
    private readonly ILogger<KSeFApiService_V2> _logger;
    private readonly KSeFOptions _ksefOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private static RSA? _cachedPublicKey;
    private static DateTime _publicKeyExpiry = DateTime.MinValue;

    public KSeFApiService_V2(
        ILogger<KSeFApiService_V2> logger,
        IOptions<KSeFOptions> ksefOptions,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _ksefOptions = ksefOptions.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<KSeFSessionResult> InitializeSessionAsync(string nip, string token, string environment)
    {
        try
        {
            var apiUrl = environment == "Production"
                ? _ksefOptions.ProductionApiUrl
                : _ksefOptions.TestApiUrl;

            _logger.LogInformation("Initializing KSeF 2.0 session for NIP: {NIP}, Environment: {Environment}, URL: {ApiUrl}",
                nip, environment, apiUrl);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(apiUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Step 1: Get authorization challenge
            _logger.LogDebug("Step 1: Requesting authorization challenge");
            var challengeResponse = await httpClient.PostAsync(
                "/auth/challenge",
                new StringContent(JsonSerializer.Serialize(new
                {
                    contextIdentifier = new
                    {
                        type = "onip",
                        identifier = nip
                    }
                }), Encoding.UTF8, "application/json"));

            if (!challengeResponse.IsSuccessStatusCode)
            {
                var errorContent = await challengeResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to generate authorization challenge. Status: {Status}, Error: {Error}",
                    challengeResponse.StatusCode, errorContent);

                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to generate authorization challenge: {challengeResponse.StatusCode} - {errorContent}"
                };
            }

            var challengeJson = await challengeResponse.Content.ReadAsStringAsync();
            var challengeData = JsonSerializer.Deserialize<JsonElement>(challengeJson);

            var challenge = challengeData.GetProperty("challenge").GetString();
            var timestampStr = challengeData.GetProperty("timestamp").GetString();

            if (string.IsNullOrEmpty(challenge) || string.IsNullOrEmpty(timestampStr))
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "Invalid challenge response from KSeF - missing challenge or timestamp"
                };
            }

            _logger.LogDebug("Received challenge: {Challenge}, timestamp: {Timestamp}", challenge, timestampStr);

            // Step 2: Download and cache public key
            _logger.LogDebug("Step 2: Getting KSeF public key");
            var publicKey = await GetKSeFPublicKeyAsync(apiUrl);
            if (publicKey == null)
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to retrieve KSeF public key"
                };
            }

            // Step 3: Encrypt token with RSA-OAEP
            _logger.LogDebug("Step 3: Encrypting token with RSA-OAEP");

            // Convert timestamp to milliseconds (Unix epoch)
            var timestampMs = DateTimeOffset.Parse(timestampStr).ToUnixTimeMilliseconds();
            var tokenString = $"{token}|{timestampMs}";

            var encryptedToken = EncryptTokenWithRSA(tokenString, publicKey);
            if (encryptedToken == null)
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to encrypt token"
                };
            }

            // Step 4: Submit encrypted token for authentication
            _logger.LogDebug("Step 4: Submitting encrypted token to /auth/ksef-token");
            var authRequest = new
            {
                challenge = challenge,
                contextIdentifier = new
                {
                    type = "onip",
                    identifier = nip
                },
                encryptedToken = encryptedToken
            };

            var authResponse = await httpClient.PostAsync(
                "/auth/ksef-token",
                new StringContent(JsonSerializer.Serialize(authRequest), Encoding.UTF8, "application/json"));

            if (!authResponse.IsSuccessStatusCode)
            {
                var errorContent = await authResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to authenticate with KSeF. Status: {Status}, Error: {Error}",
                    authResponse.StatusCode, errorContent);

                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to authenticate: {authResponse.StatusCode} - {errorContent}"
                };
            }

            var authJson = await authResponse.Content.ReadAsStringAsync();
            var authData = JsonSerializer.Deserialize<JsonElement>(authJson);

            var authenticationToken = authData.GetProperty("authenticationToken").GetString();
            var referenceNumber = authData.GetProperty("referenceNumber").GetString();

            if (string.IsNullOrEmpty(authenticationToken))
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "No authentication token received from KSeF"
                };
            }

            _logger.LogDebug("Received authenticationToken, reference: {Reference}", referenceNumber);

            // Step 5: Redeem authentication token for access token
            _logger.LogDebug("Step 5: Redeeming authentication token for access token");
            var redeemClient = _httpClientFactory.CreateClient();
            redeemClient.BaseAddress = new Uri(apiUrl);
            redeemClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            redeemClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authenticationToken);

            var redeemResponse = await redeemClient.PostAsync("/auth/token/redeem", null);

            if (!redeemResponse.IsSuccessStatusCode)
            {
                var errorContent = await redeemResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to redeem token. Status: {Status}, Error: {Error}",
                    redeemResponse.StatusCode, errorContent);

                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to redeem token: {redeemResponse.StatusCode} - {errorContent}"
                };
            }

            var redeemJson = await redeemResponse.Content.ReadAsStringAsync();
            var redeemData = JsonSerializer.Deserialize<JsonElement>(redeemJson);

            var accessToken = redeemData.GetProperty("accessToken").GetProperty("token").GetString();
            var validUntil = redeemData.GetProperty("accessToken").GetProperty("validUntil").GetString();

            if (string.IsNullOrEmpty(accessToken))
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "No access token received from KSeF"
                };
            }

            _logger.LogInformation("KSeF 2.0 session initialized successfully for NIP: {NIP}, valid until: {ValidUntil}",
                nip, validUntil);

            return new KSeFSessionResult
            {
                Success = true,
                SessionToken = accessToken, // Now it's the access token (JWT)
                ExpiresAt = DateTime.Parse(validUntil)
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error initializing KSeF session for NIP: {NIP}", nip);
            return new KSeFSessionResult
            {
                Success = false,
                ErrorMessage = $"Network error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing KSeF session for NIP: {NIP}", nip);
            return new KSeFSessionResult
            {
                Success = false,
                ErrorMessage = $"Session initialization error: {ex.Message}"
            };
        }
    }

    public async Task<KSeFInvoiceSubmissionResult> SendInvoiceAsync(string accessToken, string invoiceXml)
    {
        try
        {
            _logger.LogInformation("Sending invoice to KSeF 2.0. XML length: {Length}", invoiceXml.Length);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Encode invoice XML to Base64
            var base64Invoice = Convert.ToBase64String(Encoding.UTF8.GetBytes(invoiceXml));

            var submitRequest = new
            {
                invoiceHash = new
                {
                    hashSHA = new
                    {
                        algorithm = "SHA-256",
                        encoding = "Base64",
                        value = ComputeSHA256Hash(invoiceXml)
                    }
                },
                invoicePayload = new
                {
                    type = "plain",
                    invoiceBody = base64Invoice
                }
            };

            var submitResponse = await httpClient.PostAsync(
                "/invoices/send",
                new StringContent(JsonSerializer.Serialize(submitRequest), Encoding.UTF8, "application/json"));

            if (!submitResponse.IsSuccessStatusCode)
            {
                var errorContent = await submitResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send invoice to KSeF. Status: {Status}, Error: {Error}",
                    submitResponse.StatusCode, errorContent);

                return new KSeFInvoiceSubmissionResult
                {
                    Success = false,
                    ErrorMessage = errorContent,
                    SubmittedAt = DateTime.UtcNow
                };
            }

            var responseJson = await submitResponse.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var referenceNumber = responseData.GetProperty("elementReferenceNumber").GetString();

            _logger.LogInformation("Invoice sent to KSeF successfully. Reference: {Reference}", referenceNumber);

            return new KSeFInvoiceSubmissionResult
            {
                Success = true,
                KSeFReferenceNumber = referenceNumber,
                Status = "Sent",
                SubmittedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending invoice to KSeF");
            return new KSeFInvoiceSubmissionResult
            {
                Success = false,
                ErrorMessage = $"Invoice submission error: {ex.Message}",
                SubmittedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<KSeFInvoiceStatusResult> CheckInvoiceStatusAsync(string accessToken, string ksefReferenceNumber)
    {
        try
        {
            _logger.LogInformation("Checking invoice status for reference: {ReferenceNumber}", ksefReferenceNumber);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var statusResponse = await httpClient.GetAsync($"/invoices/{ksefReferenceNumber}/status");

            if (!statusResponse.IsSuccessStatusCode)
            {
                var errorContent = await statusResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to check invoice status. Status: {Status}, Error: {Error}",
                    statusResponse.StatusCode, errorContent);

                return new KSeFInvoiceStatusResult
                {
                    Success = false,
                    Status = "Unknown",
                    ErrorMessage = $"Failed to check status: {statusResponse.StatusCode}"
                };
            }

            var statusJson = await statusResponse.Content.ReadAsStringAsync();
            var statusData = JsonSerializer.Deserialize<JsonElement>(statusJson);

            var processingCode = statusData.GetProperty("processingCode").GetInt32();
            string status = processingCode switch
            {
                200 => "Accepted",
                400 => "Rejected",
                _ => "Pending"
            };

            _logger.LogInformation("Invoice status checked. Reference: {Reference}, Status: {Status}",
                ksefReferenceNumber, status);

            return new KSeFInvoiceStatusResult
            {
                Success = true,
                Status = status,
                ProcessedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking invoice status: {ReferenceNumber}", ksefReferenceNumber);
            return new KSeFInvoiceStatusResult
            {
                Success = false,
                Status = "Error",
                ErrorMessage = $"Status check error: {ex.Message}"
            };
        }
    }

    public async Task CloseSessionAsync(string accessToken)
    {
        try
        {
            _logger.LogInformation("Closing KSeF session");

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.DeleteAsync("/auth/sessions/current");

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("KSeF session closed successfully");
            }
            else
            {
                _logger.LogWarning("Failed to close KSeF session. Status: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing KSeF session");
        }
    }

    public async Task<bool> TestConnectionAsync(string nip, string token, string environment)
    {
        try
        {
            _logger.LogInformation("Testing KSeF connection for NIP: {NIP}, Environment: {Environment}", nip, environment);

            var sessionResult = await InitializeSessionAsync(nip, token, environment);
            if (sessionResult.Success && !string.IsNullOrEmpty(sessionResult.SessionToken))
            {
                await CloseSessionAsync(sessionResult.SessionToken);
                _logger.LogInformation("KSeF connection test successful for NIP: {NIP}", nip);
                return true;
            }

            _logger.LogWarning("KSeF connection test failed for NIP: {NIP}. Error: {Error}", nip, sessionResult.ErrorMessage);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing KSeF connection for NIP: {NIP}", nip);
            return false;
        }
    }

    // Helper Methods

    private async Task<RSA?> GetKSeFPublicKeyAsync(string apiUrl)
    {
        try
        {
            // Check if cached key is still valid (cache for 24 hours)
            if (_cachedPublicKey != null && DateTime.UtcNow < _publicKeyExpiry)
            {
                _logger.LogDebug("Using cached KSeF public key");
                return _cachedPublicKey;
            }

            _logger.LogDebug("Downloading KSeF public key from {Url}", _ksefOptions.PublicKeyUrl);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(_ksefOptions.PublicKeyUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download public key. Status: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // The response contains an array of certificates
            if (!data.TryGetProperty("certificates", out var certificates) || certificates.GetArrayLength() == 0)
            {
                _logger.LogError("No certificates found in public key response");
                return null;
            }

            // Get the first certificate's public key (PEM format)
            var pemKey = certificates[0].GetProperty("publicKey").GetString();

            if (string.IsNullOrEmpty(pemKey))
            {
                _logger.LogError("Empty public key in response");
                return null;
            }

            // Parse PEM format RSA public key
            var rsa = RSA.Create();
            rsa.ImportFromPem(pemKey);

            // Cache the key for 24 hours
            _cachedPublicKey = rsa;
            _publicKeyExpiry = DateTime.UtcNow.AddHours(24);

            _logger.LogInformation("KSeF public key downloaded and cached successfully");
            return rsa;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading or parsing KSeF public key");
            return null;
        }
    }

    private string? EncryptTokenWithRSA(string tokenString, RSA publicKey)
    {
        try
        {
            var tokenBytes = Encoding.UTF8.GetBytes(tokenString);

            // Encrypt using RSA-OAEP with SHA-256
            var encryptedBytes = publicKey.Encrypt(tokenBytes, RSAEncryptionPadding.OaepSHA256);

            // Encode to Base64
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting token with RSA");
            return null;
        }
    }

    private string ComputeSHA256Hash(string data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hashBytes);
    }
}
