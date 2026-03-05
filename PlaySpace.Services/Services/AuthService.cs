using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services;

public class AuthService
{
    private readonly IUserService _userService;
    private readonly IConfiguration _config;
    private readonly IRoleService _roleService;
    private readonly IEmailService _emailService;
    private readonly IPrivacySettingsService _privacySettingsService;
    private readonly FrontendConfiguration _frontendConfig;

    public AuthService(
        IUserService userService,
        IConfiguration config,
        IRoleService roleService,
        IEmailService emailService,
        IPrivacySettingsService privacySettingsService,
        IOptions<FrontendConfiguration> frontendConfig)
    {
        _userService = userService;
        _config = config;
        _roleService = roleService;
        _emailService = emailService;
        _privacySettingsService = privacySettingsService;
        _frontendConfig = frontendConfig.Value;
    }

    public async Task<RegistrationResponse> RegisterAsync(RegisterRequest request)
    {
        // Ensure default roles exist
        _roleService.EnsureDefaultRolesExist();

        // Ensure Player role is always included
        var roles = request.Roles.ToList();
        if (!roles.Contains("Player"))
        {
            roles.Add("Player");
        }

        // Check if user exists, hash password, create user
        var user = _userService.CreateUser(new UserDto
        {
            Email = request.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            ActivityInterests = request.ActivityInterests,
            Roles = roles
        });

        // Create default privacy settings for the new user
        await _privacySettingsService.CreateDefaultSettingsAsync(user.Id);

        // Generate email verification token
        var verificationToken = await _userService.GenerateEmailVerificationTokenAsync(user.Id);

        // Build verification URLs
        var webUrl = $"{_frontendConfig.WebAppUrl}/verify-email?token={verificationToken}";
        var deepLinkUrl = $"{_frontendConfig.DeepLinkScheme}://verify-email?token={verificationToken}";

        // Send verification email
        await _emailService.SendEmailVerificationAsync(user.Email, user.FirstName, webUrl, deepLinkUrl);

        return new RegistrationResponse
        {
            Success = true,
            RequiresEmailVerification = true,
            Message = "Registration successful. Please check your email to verify your account.",
            UserId = user.Id
        };
    }

    // Keep the old sync method for backward compatibility (without email verification)
    public AuthResponse Register(RegisterRequest request)
    {
        // Ensure default roles exist
        _roleService.EnsureDefaultRolesExist();

        // Ensure Player role is always included
        var roles = request.Roles.ToList();
        if (!roles.Contains("Player"))
        {
            roles.Add("Player");
        }

        // Check if user exists, hash password, create user
        var user = _userService.CreateUser(new UserDto
        {
            Email = request.Email,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            DateOfBirth = request.DateOfBirth,
            ActivityInterests = request.ActivityInterests,
            Roles = roles
        });

        // Create default privacy settings for the new user
        _privacySettingsService.CreateDefaultSettingsAsync(user.Id).GetAwaiter().GetResult();

        return GenerateAuthResponse(user);
    }

    public AuthResponse Login(string email, string password)
    {
        var user = _userService.GetUserByEmail(email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            throw new UnauthorizedAccessException("Invalid email or password");

        // Check if email is verified
        if (!user.IsEmailVerified)
            throw new InvalidOperationException("EMAIL_NOT_VERIFIED");

        return GenerateAuthResponse(user);
    }

    public AuthResponse Refresh(string refreshToken)
    {
        // Validate refresh token, get user, generate new tokens
        var user = _userService.GetUserByRefreshToken(refreshToken);
        if (user == null)
            throw new UnauthorizedAccessException();

        return GenerateAuthResponse(user);
    }

    public AuthResponse GenerateAuthResponse(UserDto user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"]);
        var expires = DateTime.UtcNow.AddDays(100);

        // Build claims list including email_verified
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FirstName + " " + user.LastName),
            new Claim("email_verified", user.IsEmailVerified.ToString().ToLower())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = "PlaySpace_issuer",
            Audience = "PlaySpace_audience",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwtToken = tokenHandler.WriteToken(token);

        // Generate refresh token
        var refreshToken = Guid.NewGuid().ToString();

        // Save refresh token to user
        _userService.SetRefreshToken(user.Id, refreshToken);

        return new AuthResponse
        {
            User = new User
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.Phone,
                DateOfBirth = user.DateOfBirth
            },
            Token = jwtToken,
            RefreshToken = refreshToken,
            ExpiresAt = expires.ToString("o")
        };
    }
}
