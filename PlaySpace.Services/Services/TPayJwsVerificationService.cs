using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.Models;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace PlaySpace.Services.Services
{
    public interface ITPayJwsVerificationService
    {
        Task<bool> VerifyJwsSignatureAsync(string rawBody, string jwsSignature);
        Task<X509Certificate2> GetTPayCertificateAsync();
    }

    public class TPayJwsVerificationService : ITPayJwsVerificationService
    {
        private readonly ILogger<TPayJwsVerificationService> _logger;
        private readonly TPayConfiguration _tpayConfig;
        private readonly HttpClient _httpClient;
        private X509Certificate2? _cachedCertificate;
        private DateTime _certificateCacheExpiry = DateTime.MinValue;

        public TPayJwsVerificationService(
            ILogger<TPayJwsVerificationService> logger,
            IOptions<TPayConfiguration> tpayConfig,
            HttpClient httpClient)
        {
            _logger = logger;
            _tpayConfig = tpayConfig.Value;
            _httpClient = httpClient;
        }

        public async Task<bool> VerifyJwsSignatureAsync(string rawBody, string jwsSignature)
        {
            try
            {
                if (string.IsNullOrEmpty(rawBody) || string.IsNullOrEmpty(jwsSignature))
                {
                    _logger.LogWarning("Missing raw body or JWS signature for verification");
                    return false;
                }

                _logger.LogDebug("Verifying JWS signature for body length: {BodyLength}", rawBody.Length);

                // Get TPay certificate
                var certificate = await GetTPayCertificateAsync();
                if (certificate == null)
                {
                    _logger.LogError("Failed to retrieve TPay certificate for JWS verification");
                    return false;
                }

                // Parse JWS signature (format: header.payload.signature)
                var jwsParts = jwsSignature.Split('.');
                if (jwsParts.Length != 3)
                {
                    _logger.LogWarning("Invalid JWS signature format. Expected 3 parts, got {PartCount}", jwsParts.Length);
                    return false;
                }

                var header = jwsParts[0];
                var payload = jwsParts[1];
                var signature = jwsParts[2];

                // Decode header to check algorithm
                var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(header));
                var headerObj = JsonSerializer.Deserialize<JsonElement>(headerJson);
                
                if (!headerObj.TryGetProperty("alg", out var algProperty) || algProperty.GetString() != "RS256")
                {
                    _logger.LogWarning("Unsupported JWS algorithm: {Algorithm}", algProperty.GetString());
                    return false;
                }

                // Verify that payload matches the raw body
                var decodedPayload = Encoding.UTF8.GetString(Base64UrlDecode(payload));
                if (decodedPayload != rawBody)
                {
                    _logger.LogWarning("JWS payload does not match raw body");
                    return false;
                }

                // Create signing input (header.payload)
                var signingInput = $"{header}.{payload}";
                var signingInputBytes = Encoding.UTF8.GetBytes(signingInput);

                // Decode signature
                var signatureBytes = Base64UrlDecode(signature);

                // Verify signature using RSA with certificate public key
                using var rsa = certificate.GetRSAPublicKey();
                if (rsa == null)
                {
                    _logger.LogError("Failed to extract RSA public key from certificate");
                    return false;
                }

                var isValid = rsa.VerifyData(signingInputBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                
                _logger.LogInformation("JWS signature verification result: {IsValid}", isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying JWS signature");
                return false;
            }
        }

        public async Task<X509Certificate2> GetTPayCertificateAsync()
        {
            try
            {
                // Check cache first (cache for 1 hour)
                if (_cachedCertificate != null && DateTime.UtcNow < _certificateCacheExpiry)
                {
                    return _cachedCertificate;
                }

                // Determine certificate URL based on environment
                var certificateUrl = _tpayConfig.IsSandbox
                    ? "https://secure.sandbox.tpay.com/x509/notifications-jws.pem"
                    : "https://secure.tpay.com/x509/notifications-jws.pem";

                _logger.LogDebug("Fetching TPay certificate from: {CertificateUrl}", certificateUrl);

                // Download certificate
                var certificatePem = await _httpClient.GetStringAsync(certificateUrl);
                
                if (string.IsNullOrEmpty(certificatePem))
                {
                    _logger.LogError("Received empty certificate from TPay");
                    return null;
                }

                // Parse PEM certificate
                var certificate = X509Certificate2.CreateFromPem(certificatePem);
                
                // Cache certificate for 1 hour
                _cachedCertificate = certificate;
                _certificateCacheExpiry = DateTime.UtcNow.AddHours(1);

                _logger.LogInformation("Successfully loaded TPay certificate. Valid from {NotBefore} to {NotAfter}", 
                    certificate.NotBefore, certificate.NotAfter);

                return certificate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving TPay certificate");
                return null;
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            // Pad the input to make it valid base64
            var padded = input.Length % 4 == 0 ? input : input + new string('=', 4 - input.Length % 4);
            
            // Replace URL-safe characters
            var base64 = padded.Replace('-', '+').Replace('_', '/');
            
            return Convert.FromBase64String(base64);
        }
    }
}