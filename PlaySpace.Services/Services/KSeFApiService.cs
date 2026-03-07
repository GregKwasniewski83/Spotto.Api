using PlaySpace.Services.Interfaces;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.Configuration;
using PlaySpace.Domain.DTOs;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

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
public class KSeFApiService : IKSeFApiService
{
    private readonly ILogger<KSeFApiService> _logger;
    private readonly KSeFOptions _ksefOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private static RSA? _cachedPublicKey; // For KsefTokenEncryption
    private static DateTime _publicKeyExpiry = DateTime.MinValue;
    private static RSA? _cachedEncryptionPublicKey; // For SymmetricKeyEncryption
    private static DateTime _encryptionPublicKeyExpiry = DateTime.MinValue;

    public KSeFApiService(
        ILogger<KSeFApiService> logger,
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

            // Step 1: Get authorization challenge (KSeF 2.0 - no request body required)
            _logger.LogDebug("Step 1: Requesting authorization challenge for NIP: {NIP}", nip);
            _logger.LogDebug("Challenge URL: {BaseAddress}{Path}", httpClient.BaseAddress, "auth/challenge");

            // KSeF 2.0: Challenge endpoint requires no body - just POST to get challenge + timestamp
            var challengeResponse = await httpClient.PostAsync(
                "auth/challenge",
                new StringContent("", Encoding.UTF8, "application/json"));

            _logger.LogDebug("Challenge response status: {Status}, Request URI: {RequestUri}",
                challengeResponse.StatusCode, challengeResponse.RequestMessage?.RequestUri);

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

            // KSeF 2.0 returns timestampMs directly (milliseconds since Unix epoch)
            long timestampMs;
            if (challengeData.TryGetProperty("timestampMs", out var timestampMsElement))
            {
                timestampMs = timestampMsElement.GetInt64();
            }
            else
            {
                // Fallback: parse timestamp string if timestampMs not available
                var timestampStr = challengeData.GetProperty("timestamp").GetString();
                if (string.IsNullOrEmpty(timestampStr))
                {
                    return new KSeFSessionResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid challenge response from KSeF - missing timestamp"
                    };
                }
                timestampMs = DateTimeOffset.Parse(timestampStr).ToUnixTimeMilliseconds();
            }

            if (string.IsNullOrEmpty(challenge))
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "Invalid challenge response from KSeF - missing challenge"
                };
            }

            _logger.LogDebug("Received challenge: {Challenge}, timestampMs: {TimestampMs}", challenge, timestampMs);

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
                    type = "Nip",
                    value = nip
                },
                encryptedToken = encryptedToken
            };

            var authResponse = await httpClient.PostAsync(
                "auth/ksef-token",
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
            _logger.LogInformation("[KSeF] Step 4 response (auth/ksef-token): {Response}", authJson);

            var authData = JsonSerializer.Deserialize<JsonElement>(authJson);

            // authenticationToken is an object with token and validUntil properties
            var authenticationToken = authData.GetProperty("authenticationToken").GetProperty("token").GetString();
            var referenceNumber = authData.TryGetProperty("referenceNumber", out var refNumElement)
                ? refNumElement.GetString()
                : null;

            if (string.IsNullOrEmpty(authenticationToken))
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "No authentication token received from KSeF"
                };
            }

            _logger.LogInformation("[KSeF] Received authenticationToken (length={Length}), referenceNumber: {Reference}",
                authenticationToken?.Length ?? 0, referenceNumber ?? "NULL");

            // Step 5: Poll for authentication status (KSeF 2.0 requires waiting for status 200)
            if (!string.IsNullOrEmpty(referenceNumber))
            {
                _logger.LogInformation("[KSeF] Step 5: Polling authentication status for reference: {Reference}", referenceNumber);

                var statusClient = _httpClientFactory.CreateClient();
                statusClient.BaseAddress = new Uri(apiUrl);
                statusClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                statusClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authenticationToken);

                const int maxAttempts = 30;  // 30 attempts
                const int delayMs = 2000;    // 2s between polls (total 60s timeout)
                int lastStatusCode = 0;
                string lastStatusDescription = "";
                int lastHttpStatusCode = 0;
                string lastHttpResponse = "";

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        var statusResponse = await statusClient.GetAsync($"auth/{referenceNumber}");
                        var statusJson = await statusResponse.Content.ReadAsStringAsync();
                        lastHttpStatusCode = (int)statusResponse.StatusCode;
                        lastHttpResponse = statusJson;

                        _logger.LogInformation("[KSeF] Auth status poll attempt {Attempt}/{MaxAttempts}, HTTP {StatusCode}: {Response}",
                            attempt, maxAttempts, lastHttpStatusCode, statusJson);

                        if (statusResponse.IsSuccessStatusCode)
                        {
                            var statusData = JsonSerializer.Deserialize<JsonElement>(statusJson);

                            // KSeF 2.0 response structure: { "status": { "code": 100, "description": "..." } }
                            if (statusData.TryGetProperty("status", out var statusElement))
                            {
                                if (statusElement.TryGetProperty("code", out var codeElement))
                                {
                                    lastStatusCode = codeElement.GetInt32();
                                }

                                if (statusElement.TryGetProperty("description", out var descElement))
                                {
                                    lastStatusDescription = descElement.GetString() ?? "";
                                }

                                _logger.LogInformation("[KSeF] Authentication status: {StatusCode} - {Description}",
                                    lastStatusCode, lastStatusDescription);

                                if (lastStatusCode == 200)
                                {
                                    _logger.LogInformation("[KSeF] Authentication completed successfully (status 200)");
                                    break;
                                }
                                else if (lastStatusCode >= 300)
                                {
                                    // Get additional details if available
                                    var details = "";
                                    if (statusElement.TryGetProperty("details", out var detailsElement) &&
                                        detailsElement.ValueKind == JsonValueKind.Array)
                                    {
                                        var detailsList = new List<string>();
                                        foreach (var item in detailsElement.EnumerateArray())
                                        {
                                            detailsList.Add(item.GetString() ?? "");
                                        }
                                        details = string.Join("; ", detailsList);
                                    }

                                    return new KSeFSessionResult
                                    {
                                        Success = false,
                                        ErrorMessage = $"KSeF authentication failed with status {lastStatusCode}: {lastStatusDescription}. {details}"
                                    };
                                }
                                // Status 100 = pending, continue polling
                            }
                            else
                            {
                                _logger.LogWarning("[KSeF] Response missing 'status' property: {Response}", statusJson);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("[KSeF] Auth status poll failed with HTTP {StatusCode}: {Response}",
                                lastHttpStatusCode, statusJson);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[KSeF] Auth status poll attempt {Attempt} failed with exception: {Message}", attempt, ex.Message);
                    }

                    if (attempt == maxAttempts)
                    {
                        return new KSeFSessionResult
                        {
                            Success = false,
                            ErrorMessage = $"KSeF authentication timeout after {maxAttempts * delayMs / 1000}s - last auth status: {lastStatusCode} ({lastStatusDescription}), last HTTP: {lastHttpStatusCode}"
                        };
                    }

                    await Task.Delay(delayMs);
                }
            }

            // Step 6: Redeem authentication token for access token
            _logger.LogDebug("Step 6: Redeeming authentication token for access token");
            var redeemClient = _httpClientFactory.CreateClient();
            redeemClient.BaseAddress = new Uri(apiUrl);
            redeemClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            redeemClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authenticationToken);

            var redeemResponse = await redeemClient.PostAsync("auth/token/redeem", null);

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
            _logger.LogDebug("Redeem token response: {Response}", redeemJson);

            var redeemData = JsonSerializer.Deserialize<JsonElement>(redeemJson);

            // Check if accessToken is a string or object
            string? accessToken = null;
            string? validUntil = null;
            string? refreshToken = null;
            string? refreshTokenValidUntil = null;

            if (redeemData.TryGetProperty("accessToken", out var accessTokenElement))
            {
                if (accessTokenElement.ValueKind == JsonValueKind.String)
                {
                    // accessToken is a simple string
                    accessToken = accessTokenElement.GetString();
                }
                else if (accessTokenElement.ValueKind == JsonValueKind.Object)
                {
                    // accessToken is an object with token and validUntil properties
                    accessToken = accessTokenElement.GetProperty("token").GetString();
                    if (accessTokenElement.TryGetProperty("validUntil", out var validUntilElement))
                        validUntil = validUntilElement.GetString();
                }
            }

            // Extract refresh token (KSeF 2.0 - valid up to 7 days)
            if (redeemData.TryGetProperty("refreshToken", out var refreshTokenElement))
            {
                if (refreshTokenElement.ValueKind == JsonValueKind.String)
                {
                    refreshToken = refreshTokenElement.GetString();
                }
                else if (refreshTokenElement.ValueKind == JsonValueKind.Object)
                {
                    refreshToken = refreshTokenElement.GetProperty("token").GetString();
                    if (refreshTokenElement.TryGetProperty("validUntil", out var refreshValidUntilElement))
                        refreshTokenValidUntil = refreshValidUntilElement.GetString();
                }
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "No access token received from KSeF"
                };
            }

            _logger.LogInformation("KSeF 2.0 authentication successful for NIP: {NIP}, valid until: {ValidUntil}, refresh token: {HasRefresh}",
                nip, validUntil ?? "unknown", !string.IsNullOrEmpty(refreshToken) ? "YES" : "NO");

            // Step 7: Open online session with encryption (KSeF 2.0 requirement)
            _logger.LogDebug("Step 7: Opening online session with encryption");

            // Generate AES-256 key (32 bytes) and IV (16 bytes)
            var symmetricKey = new byte[32];
            var initializationVector = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(symmetricKey);
                rng.GetBytes(initializationVector);
            }

            _logger.LogDebug("Generated AES-256 key and IV for session encryption");

            // Get the encryption public key (for symmetric key encryption)
            var encryptionPublicKey = await GetKSeFEncryptionPublicKeyAsync(apiUrl);
            if (encryptionPublicKey == null)
            {
                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = "Failed to get KSeF encryption public key"
                };
            }

            // Encrypt the symmetric key with RSA-OAEP
            var encryptedSymmetricKey = encryptionPublicKey.Encrypt(symmetricKey, RSAEncryptionPadding.OaepSHA256);
            var encryptedSymmetricKeyBase64 = Convert.ToBase64String(encryptedSymmetricKey);
            var initializationVectorBase64 = Convert.ToBase64String(initializationVector);

            _logger.LogDebug("Encrypted symmetric key with RSA-OAEP");

            // Open online session
            var sessionClient = _httpClientFactory.CreateClient();
            sessionClient.BaseAddress = new Uri(apiUrl);
            sessionClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            sessionClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Determine schema version based on current date
            // FA(2) until January 31, 2026, FA(3) from February 1, 2026
            var useFA3 = DateTime.UtcNow >= new DateTime(2026, 2, 1);
            var systemCode = useFA3 ? "FA (3)" : "FA (2)";
            var formVariant = useFA3 ? "1" : "2";

            _logger.LogInformation("Opening session with schema: {SystemCode}, variant: {Variant}", systemCode, formVariant);

            var openSessionRequest = new
            {
                formCode = new
                {
                    systemCode = systemCode,
                    schemaVersion = "1-0E",
                    value = "FA"
                },
                encryption = new
                {
                    encryptedSymmetricKey = encryptedSymmetricKeyBase64,
                    initializationVector = initializationVectorBase64
                }
            };

            var openSessionJson = JsonSerializer.Serialize(openSessionRequest);
            _logger.LogDebug("Opening online session with request: {Request}",
                openSessionJson.Length > 200 ? openSessionJson.Substring(0, 200) + "..." : openSessionJson);

            var openSessionResponse = await sessionClient.PostAsync(
                "sessions/online",
                new StringContent(openSessionJson, Encoding.UTF8, "application/json"));

            if (!openSessionResponse.IsSuccessStatusCode)
            {
                var errorContent = await openSessionResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to open online session. Status: {Status}, Error: {Error}",
                    openSessionResponse.StatusCode, errorContent);

                return new KSeFSessionResult
                {
                    Success = false,
                    ErrorMessage = $"Failed to open online session: {openSessionResponse.StatusCode} - {errorContent}"
                };
            }

            var openSessionResponseJson = await openSessionResponse.Content.ReadAsStringAsync();
            _logger.LogDebug("Open session response: {Response}", openSessionResponseJson);

            var sessionData = JsonSerializer.Deserialize<JsonElement>(openSessionResponseJson);
            var sessionReferenceNumber = sessionData.GetProperty("referenceNumber").GetString();
            var sessionValidUntil = sessionData.TryGetProperty("validUntil", out var sessionValidUntilElement)
                ? sessionValidUntilElement.GetString()
                : validUntil;

            _logger.LogInformation("KSeF 2.0 online session opened successfully. SessionRef: {SessionRef}, ValidUntil: {ValidUntil}",
                sessionReferenceNumber, sessionValidUntil ?? "unknown");

            return new KSeFSessionResult
            {
                Success = true,
                SessionToken = accessToken, // Access token (JWT) for API authorization
                RefreshToken = refreshToken, // Refresh token for extending session (valid up to 7 days)
                SessionReferenceNumber = sessionReferenceNumber, // Session reference number for invoice endpoints
                SymmetricKey = symmetricKey, // Store for invoice encryption
                InitializationVector = initializationVector, // Store for invoice encryption
                ExpiresAt = string.IsNullOrEmpty(sessionValidUntil)
                    ? DateTime.UtcNow.AddMinutes(_ksefOptions.SessionExpirationMinutes)
                    : DateTime.Parse(sessionValidUntil),
                RefreshTokenExpiresAt = string.IsNullOrEmpty(refreshTokenValidUntil)
                    ? DateTime.UtcNow.AddDays(7) // Default 7 days for refresh token
                    : DateTime.Parse(refreshTokenValidUntil)
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
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error initializing KSeF session for NIP: {NIP}. Error at: {Path}", nip, ex.Path);
            return new KSeFSessionResult
            {
                Success = false,
                ErrorMessage = $"Session initialization error (JSON parsing at {ex.Path}): {ex.Message}"
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

    public async Task<KSeFInvoiceSubmissionResult> SendInvoiceAsync(
        string accessToken,
        string sessionReferenceNumber,
        string invoiceXml,
        byte[] symmetricKey,
        byte[] initializationVector,
        string environment)
    {
        try
        {
            var apiUrl = environment == "Production"
                ? _ksefOptions.ProductionApiUrl
                : _ksefOptions.TestApiUrl;

            _logger.LogInformation("[KSeF] Sending encrypted invoice to KSeF 2.0. SessionRef: {SessionRef}, XML length: {Length}, Environment: {Environment}",
                sessionReferenceNumber, invoiceXml.Length, environment);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(apiUrl);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Original invoice data
            var invoiceBytes = Encoding.UTF8.GetBytes(invoiceXml);
            var invoiceHash = ComputeSHA256HashBytes(invoiceBytes);
            var invoiceHashBase64 = Convert.ToBase64String(invoiceHash);
            var invoiceSize = invoiceBytes.Length;

            _logger.LogDebug("[KSeF] Original invoice: size={Size}, hash={Hash}", invoiceSize, invoiceHashBase64);

            // Encrypt invoice with AES-256-CBC
            byte[] encryptedInvoice;
            using (var aes = Aes.Create())
            {
                aes.Key = symmetricKey;
                aes.IV = initializationVector;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var encryptor = aes.CreateEncryptor();
                encryptedInvoice = encryptor.TransformFinalBlock(invoiceBytes, 0, invoiceBytes.Length);
            }

            var encryptedInvoiceHash = ComputeSHA256HashBytes(encryptedInvoice);
            var encryptedInvoiceHashBase64 = Convert.ToBase64String(encryptedInvoiceHash);
            var encryptedInvoiceBase64 = Convert.ToBase64String(encryptedInvoice);
            var encryptedInvoiceSize = encryptedInvoice.Length;

            _logger.LogDebug("[KSeF] Encrypted invoice: size={Size}, hash={Hash}", encryptedInvoiceSize, encryptedInvoiceHashBase64);

            // KSeF 2.0 request format with encrypted invoice
            var submitRequest = new
            {
                invoiceHash = invoiceHashBase64,
                invoiceSize = invoiceSize,
                encryptedInvoiceHash = encryptedInvoiceHashBase64,
                encryptedInvoiceSize = encryptedInvoiceSize,
                encryptedInvoiceContent = encryptedInvoiceBase64,
                offlineMode = false
            };

            var requestJson = JsonSerializer.Serialize(submitRequest);
            _logger.LogDebug("[KSeF] Invoice submit URL: sessions/online/{SessionRef}/invoices", sessionReferenceNumber);
            _logger.LogDebug("[KSeF] Invoice submit request (keys only): invoiceHash={InvoiceHash}, invoiceSize={InvoiceSize}, encryptedSize={EncryptedSize}",
                invoiceHashBase64, invoiceSize, encryptedInvoiceSize);

            var submitResponse = await httpClient.PostAsync(
                $"sessions/online/{sessionReferenceNumber}/invoices",
                new StringContent(requestJson, Encoding.UTF8, "application/json"));

            var responseContent = await submitResponse.Content.ReadAsStringAsync();

            if (!submitResponse.IsSuccessStatusCode)
            {
                _logger.LogError("[KSeF] Failed to send invoice. Status: {Status}, Error: {Error}",
                    submitResponse.StatusCode, responseContent);

                return new KSeFInvoiceSubmissionResult
                {
                    Success = false,
                    ErrorMessage = responseContent,
                    SubmittedAt = DateTime.UtcNow
                };
            }

            _logger.LogInformation("[KSeF] Invoice submission response: {Response}", responseContent);

            var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Try to get the reference number - KSeF response format may vary
            string? referenceNumber = null;
            if (responseData.TryGetProperty("elementReferenceNumber", out var refElement))
            {
                referenceNumber = refElement.GetString();
            }
            else if (responseData.TryGetProperty("referenceNumber", out var altRefElement))
            {
                referenceNumber = altRefElement.GetString();
            }
            else
            {
                // Log all available properties to debug
                _logger.LogWarning("[KSeF] Response doesn't contain expected reference number fields. Available properties: {Props}",
                    string.Join(", ", responseData.EnumerateObject().Select(p => p.Name)));
            }

            _logger.LogInformation("[KSeF] Invoice sent successfully. KSeF Reference: {Reference}", referenceNumber ?? "UNKNOWN");

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
            _logger.LogError(ex, "[KSeF] Error sending invoice");
            return new KSeFInvoiceSubmissionResult
            {
                Success = false,
                ErrorMessage = $"Invoice submission error: {ex.Message}",
                SubmittedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<KSeFInvoiceStatusResult> CheckInvoiceStatusAsync(string accessToken, string ksefReferenceNumber, string environment)
    {
        try
        {
            var apiUrl = environment == "Production"
                ? _ksefOptions.ProductionApiUrl
                : _ksefOptions.TestApiUrl;

            _logger.LogInformation("Checking invoice status for reference: {ReferenceNumber}, Environment: {Environment}", ksefReferenceNumber, environment);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(apiUrl);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var statusResponse = await httpClient.GetAsync($"context/invoices/{ksefReferenceNumber}/status");

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

    public async Task CloseSessionAsync(string accessToken, string environment)
    {
        try
        {
            var apiUrl = environment == "Production"
                ? _ksefOptions.ProductionApiUrl
                : _ksefOptions.TestApiUrl;

            _logger.LogInformation("Closing KSeF session, Environment: {Environment}", environment);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(apiUrl);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.DeleteAsync("context/sessions/current");

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

    public async Task<KSeFTokenRefreshResult> RefreshTokenAsync(string refreshToken, string environment)
    {
        try
        {
            var apiUrl = environment == "Production"
                ? _ksefOptions.ProductionApiUrl
                : _ksefOptions.TestApiUrl;

            _logger.LogInformation("Refreshing KSeF access token, Environment: {Environment}", environment);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(apiUrl);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);

            var response = await httpClient.PostAsync("auth/token/refresh", null);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to refresh KSeF token. Status: {Status}, Error: {Error}",
                    response.StatusCode, responseJson);
                return new KSeFTokenRefreshResult
                {
                    Success = false,
                    ErrorMessage = $"Token refresh failed: {response.StatusCode} - {responseJson}"
                };
            }

            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            string? accessToken = null;
            string? validUntil = null;

            if (responseData.TryGetProperty("accessToken", out var accessTokenElement))
            {
                if (accessTokenElement.ValueKind == JsonValueKind.String)
                {
                    accessToken = accessTokenElement.GetString();
                }
                else if (accessTokenElement.ValueKind == JsonValueKind.Object)
                {
                    accessToken = accessTokenElement.GetProperty("token").GetString();
                    if (accessTokenElement.TryGetProperty("validUntil", out var validUntilElement))
                        validUntil = validUntilElement.GetString();
                }
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                return new KSeFTokenRefreshResult
                {
                    Success = false,
                    ErrorMessage = "No access token in refresh response"
                };
            }

            _logger.LogInformation("KSeF token refreshed successfully, valid until: {ValidUntil}", validUntil ?? "unknown");

            return new KSeFTokenRefreshResult
            {
                Success = true,
                AccessToken = accessToken,
                ExpiresAt = string.IsNullOrEmpty(validUntil)
                    ? DateTime.UtcNow.AddMinutes(_ksefOptions.SessionExpirationMinutes)
                    : DateTime.Parse(validUntil)
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error refreshing KSeF token");
            return new KSeFTokenRefreshResult
            {
                Success = false,
                ErrorMessage = $"Network error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing KSeF token");
            return new KSeFTokenRefreshResult
            {
                Success = false,
                ErrorMessage = $"Token refresh error: {ex.Message}"
            };
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
                await CloseSessionAsync(sessionResult.SessionToken, environment);
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
            _logger.LogDebug("Public key response: {Response}", json.Substring(0, Math.Min(200, json.Length)));

            var certificates = JsonSerializer.Deserialize<JsonElement[]>(json);

            if (certificates == null || certificates.Length == 0)
            {
                _logger.LogError("No certificates found in public key response");
                return null;
            }

            // Find the certificate with "KsefTokenEncryption" usage
            JsonElement? tokenEncryptionCert = null;
            foreach (var cert in certificates)
            {
                if (cert.TryGetProperty("usage", out var usage))
                {
                    foreach (var usageItem in usage.EnumerateArray())
                    {
                        if (usageItem.GetString() == "KsefTokenEncryption")
                        {
                            tokenEncryptionCert = cert;
                            break;
                        }
                    }
                }
                if (tokenEncryptionCert.HasValue) break;
            }

            if (!tokenEncryptionCert.HasValue)
            {
                _logger.LogError("No certificate with KsefTokenEncryption usage found");
                return null;
            }

            // Get the certificate (base64 encoded X.509)
            var certBase64 = tokenEncryptionCert.Value.GetProperty("certificate").GetString();

            if (string.IsNullOrEmpty(certBase64))
            {
                _logger.LogError("Empty certificate in response");
                return null;
            }

            // Parse X.509 certificate and extract public key
            var certBytes = Convert.FromBase64String(certBase64);
            using var x509Cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certBytes);
            var rsa = x509Cert.GetRSAPublicKey();

            if (rsa == null)
            {
                _logger.LogError("Failed to extract RSA public key from certificate");
                return null;
            }

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

    private async Task<RSA?> GetKSeFEncryptionPublicKeyAsync(string apiUrl)
    {
        try
        {
            // Check if cached key is still valid (cache for 24 hours)
            if (_cachedEncryptionPublicKey != null && DateTime.UtcNow < _encryptionPublicKeyExpiry)
            {
                _logger.LogDebug("Using cached KSeF encryption public key");
                return _cachedEncryptionPublicKey;
            }

            _logger.LogDebug("Downloading KSeF encryption public key from {Url}", _ksefOptions.PublicKeyUrl);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(_ksefOptions.PublicKeyUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download encryption public key. Status: {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Encryption public key response length: {Length}", json.Length);

            var certificates = JsonSerializer.Deserialize<JsonElement[]>(json);

            if (certificates == null || certificates.Length == 0)
            {
                _logger.LogError("No certificates found in public key response");
                return null;
            }

            // Find the certificate with "SymmetricKeyEncryption" usage
            JsonElement? encryptionCert = null;
            foreach (var cert in certificates)
            {
                if (cert.TryGetProperty("usage", out var usage))
                {
                    foreach (var usageItem in usage.EnumerateArray())
                    {
                        if (usageItem.GetString() == "SymmetricKeyEncryption")
                        {
                            encryptionCert = cert;
                            break;
                        }
                    }
                }
                if (encryptionCert.HasValue) break;
            }

            if (!encryptionCert.HasValue)
            {
                _logger.LogError("No certificate with SymmetricKeyEncryption usage found");
                return null;
            }

            // Get the certificate (base64 encoded X.509)
            var certBase64 = encryptionCert.Value.GetProperty("certificate").GetString();

            if (string.IsNullOrEmpty(certBase64))
            {
                _logger.LogError("Empty encryption certificate in response");
                return null;
            }

            // Parse X.509 certificate and extract public key
            var certBytes = Convert.FromBase64String(certBase64);
            using var x509Cert = new X509Certificate2(certBytes);
            var rsa = x509Cert.GetRSAPublicKey();

            if (rsa == null)
            {
                _logger.LogError("Failed to extract RSA public key from encryption certificate");
                return null;
            }

            // Cache the key for 24 hours
            _cachedEncryptionPublicKey = rsa;
            _encryptionPublicKeyExpiry = DateTime.UtcNow.AddHours(24);

            _logger.LogInformation("KSeF encryption public key downloaded and cached successfully");
            return rsa;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading or parsing KSeF encryption public key");
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

    private byte[] ComputeSHA256HashBytes(byte[] data)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(data);
    }
}
