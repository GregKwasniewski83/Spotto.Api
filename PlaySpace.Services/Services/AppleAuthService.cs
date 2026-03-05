using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;

namespace PlaySpace.Services.Services;

public class AppleAuthService : IAppleAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppleAuthService> _logger;
    private readonly HttpClient _httpClient;

    public AppleAuthService(IConfiguration configuration, ILogger<AppleAuthService> logger, HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<ExternalUserInfo> VerifyAppleTokenAsync(string idToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadJwtToken(idToken);

            // Get Apple's public keys
            var appleKeys = await GetApplePublicKeysAsync();
            
            // Find the key that matches the token's kid (Key ID)
            var kid = jsonToken.Header.Kid;
            var key = appleKeys.Keys.FirstOrDefault(k => k.Kid == kid);
            
            if (key == null)
            {
                throw new SecurityTokenValidationException("Unable to find matching key");
            }

            // Create RSA security key
            var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Convert.FromBase64String(AddPadding(key.N)),
                Exponent = Convert.FromBase64String(AddPadding(key.E))
            });

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "https://appleid.apple.com",
                ValidateAudience = true,
                ValidAudience = _configuration["AppleAuth:ClientId"], // Your app's bundle ID
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new RsaSecurityKey(rsa),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            var principal = handler.ValidateToken(idToken, validationParameters, out var validatedToken);

            // Extract claims
            var sub = principal.FindFirst("sub")?.Value;
            var email = principal.FindFirst("email")?.Value;
            var emailVerified = principal.FindFirst("email_verified")?.Value;
            var name = principal.FindFirst("name")?.Value;

            // Parse name if available (Apple sometimes provides this)
            string? firstName = null;
            string? lastName = null;
            
            if (!string.IsNullOrEmpty(name))
            {
                try
                {
                    var nameJson = JsonDocument.Parse(name);
                    firstName = nameJson.RootElement.GetProperty("firstName").GetString();
                    lastName = nameJson.RootElement.GetProperty("lastName").GetString();
                }
                catch
                {
                    // Name parsing failed, use email as fallback
                    firstName = email?.Split('@')[0];
                }
            }

            return new ExternalUserInfo
            {
                ExternalUserId = sub ?? throw new Exception("Apple token missing subject"),
                Email = email ?? throw new Exception("Apple token missing email"),
                FirstName = firstName ?? "Unknown",
                LastName = lastName ?? "User",
                DisplayName = $"{firstName} {lastName}".Trim(),
                EmailVerified = bool.TryParse(emailVerified, out var verified) ? verified : false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Apple token");
            throw new UnauthorizedAccessException("Invalid Apple token", ex);
        }
    }

    private async Task<AppleKeysResponse> GetApplePublicKeysAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://appleid.apple.com/auth/keys");
            return JsonSerializer.Deserialize<AppleKeysResponse>(response) ?? 
                   throw new Exception("Failed to parse Apple keys response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Apple public keys");
            throw new Exception("Failed to get Apple public keys", ex);
        }
    }

    private static string AddPadding(string base64String)
    {
        // Add padding if necessary
        switch (base64String.Length % 4)
        {
            case 2: base64String += "=="; break;
            case 3: base64String += "="; break;
        }
        return base64String;
    }
}

public class AppleKeysResponse
{
    public List<AppleKey> Keys { get; set; } = new();
}

public class AppleKey
{
    public string Kty { get; set; } = string.Empty;
    public string Kid { get; set; } = string.Empty;
    public string Use { get; set; } = string.Empty;
    public string Alg { get; set; } = string.Empty;
    public string N { get; set; } = string.Empty;
    public string E { get; set; } = string.Empty;
}