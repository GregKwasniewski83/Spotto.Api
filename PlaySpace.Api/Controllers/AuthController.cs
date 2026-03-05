using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Services.Services;
using PlaySpace.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;


namespace PlaySpace.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IExternalAuthService _externalAuthService;
        private readonly IAgentManagementService _agentManagementService;
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, IExternalAuthService externalAuthService, IAgentManagementService agentManagementService, IUserService userService, IRoleService roleService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _authService = authService;
            _externalAuthService = externalAuthService;
            _agentManagementService = agentManagementService;
            _userService = userService;
            _roleService = roleService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("signup")]
        public async Task<ActionResult<RegistrationResponse>> SignUp([FromBody] RegisterRequest request)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Registration validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Invalid registration data", details = errors });
                }

                // Validate email format
                if (!new EmailAddressAttribute().IsValid(request.Email))
                {
                    _logger.LogWarning("Registration failed: Invalid email format {Email}", request.Email);
                    return BadRequest(new { error = "INVALID_EMAIL", message = "Please provide a valid email address" });
                }

                // Validate password strength
                if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 6)
                {
                    _logger.LogWarning("Registration failed: Weak password for email {Email}", request.Email);
                    return BadRequest(new { error = "WEAK_PASSWORD", message = "Password must be at least 6 characters long" });
                }

                _logger.LogInformation("Processing registration for email {Email}", request.Email);
                var response = await _authService.RegisterAsync(request);

                _logger.LogInformation("Registration successful for email {Email}, userId {UserId}", request.Email, response.UserId);
                return Ok(response);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate key") == true || ex.InnerException?.Message?.Contains("unique constraint") == true)
            {
                _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
                return Conflict(new { error = "EMAIL_EXISTS", message = "An account with this email address already exists" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Registration failed: Invalid operation for email {Email}", request.Email);
                return BadRequest(new { error = "INVALID_OPERATION", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Registration failed: Invalid argument for email {Email}", request.Email);
                return BadRequest(new { error = "INVALID_DATA", message = ex.Message });
            }
            catch (System.ComponentModel.DataAnnotations.ValidationException ex)
            {
                _logger.LogWarning(ex, "Registration failed: Validation error for email {Email}", request.Email);
                return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registration failed: Unexpected error for email {Email}", request.Email);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred during registration. Please try again." });
            }
        }

        [HttpPost("signin")]
        public ActionResult<AuthResponse> SignIn([FromBody] LoginRequest request)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Login validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Invalid login data", details = errors });
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    _logger.LogWarning("Login failed: Empty email");
                    return BadRequest(new { error = "MISSING_EMAIL", message = "Email is required" });
                }

                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    _logger.LogWarning("Login failed: Empty password for email {Email}", request.Email);
                    return BadRequest(new { error = "MISSING_PASSWORD", message = "Password is required" });
                }

                // Validate email format
                if (!new EmailAddressAttribute().IsValid(request.Email))
                {
                    _logger.LogWarning("Login failed: Invalid email format {Email}", request.Email);
                    return BadRequest(new { error = "INVALID_EMAIL", message = "Please provide a valid email address" });
                }

                _logger.LogInformation("Processing login for email {Email}", request.Email);
                var response = _authService.Login(request.Email, request.Password);
                
                _logger.LogInformation("Login successful for email {Email}, userId {UserId}", request.Email, response.User.Id);
                return Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Login failed: Invalid credentials for email {Email}", request.Email);
                return Unauthorized(new { error = "INVALID_CREDENTIALS", message = "Invalid email or password" });
            }
            catch (InvalidOperationException ex) when (ex.Message == "EMAIL_NOT_VERIFIED")
            {
                _logger.LogWarning("Login failed: Email not verified for email {Email}", request.Email);
                return Unauthorized(new { error = "EMAIL_NOT_VERIFIED", message = "Please verify your email address before logging in. Check your inbox for the verification link." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Login failed: Invalid operation for email {Email}", request.Email);
                return BadRequest(new { error = "INVALID_OPERATION", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Login failed: Invalid argument for email {Email}", request.Email);
                return BadRequest(new { error = "INVALID_DATA", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed: Unexpected error for email {Email}", request.Email);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred during login. Please try again." });
            }
        }

        [HttpPost("refresh")]
        public ActionResult<AuthResponse> Refresh([FromBody] RefreshRequest request)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Token refresh validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Invalid refresh request", details = errors });
                }

                // Validate refresh token
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                {
                    _logger.LogWarning("Token refresh failed: Empty refresh token");
                    return BadRequest(new { error = "MISSING_REFRESH_TOKEN", message = "Refresh token is required" });
                }

                _logger.LogDebug("Processing token refresh");
                var response = _authService.Refresh(request.RefreshToken);
                
                _logger.LogDebug("Token refresh successful for userId {UserId}", response.User.Id);
                return Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Token refresh failed: Invalid or expired refresh token");
                return Unauthorized(new { error = "INVALID_REFRESH_TOKEN", message = "Invalid or expired refresh token. Please login again." });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Token refresh failed: Invalid operation");
                return BadRequest(new { error = "INVALID_OPERATION", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token refresh failed: Unexpected error");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred during token refresh. Please login again." });
            }
        }

        [HttpPost("external/google")]
        public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] ExternalLoginRequest request)
        {
            try
            {
                request.Provider = "google";
                var response = await _externalAuthService.ExternalLoginAsync(request);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "External authentication failed", error = ex.Message });
            }
        }

        [HttpPost("external/apple")]
        public async Task<ActionResult<AuthResponse>> AppleLogin([FromBody] ExternalLoginRequest request)
        {
            try
            {
                request.Provider = "apple";
                var response = await _externalAuthService.ExternalLoginAsync(request);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "External authentication failed", error = ex.Message });
            }
        }

        [HttpPost("external/link")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> LinkExternalAccount([FromBody] LinkExternalAccountRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var response = await _externalAuthService.LinkExternalAccountAsync(userId, request);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Account linking failed", error = ex.Message });
            }
        }

        [HttpDelete("external/{provider}")]
        [Authorize]
        public async Task<ActionResult> UnlinkExternalAccount(string provider)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var success = await _externalAuthService.UnlinkExternalAccountAsync(userId, provider);
                if (!success)
                {
                    return NotFound(new { message = $"No {provider} account linked to this user" });
                }

                return Ok(new { message = $"{provider} account unlinked successfully" });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Account unlinking failed", error = ex.Message });
            }
        }

        [HttpGet("external/accounts")]
        [Authorize]
        public async Task<ActionResult<List<ExternalAuthDto>>> GetLinkedAccounts()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized("User ID not found in token");
                }

                var accounts = await _externalAuthService.GetUserExternalAccountsAsync(userId);
                return Ok(accounts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve linked accounts", error = ex.Message });
            }
        }

        [HttpPost("register/agent")]
        public async Task<ActionResult<AuthResponse>> RegisterAgent([FromBody] RegisterAgentDto registerDto)
        {
            try
            {
                // Validate model state
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Agent registration validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Invalid registration data", details = errors });
                }

                // Get invitation details
                var invitation = await _agentManagementService.GetInvitationByTokenAsync(registerDto.InvitationToken);
                if (invitation == null)
                {
                    _logger.LogWarning("Agent registration failed: Invalid invitation token {Token}", registerDto.InvitationToken);
                    return BadRequest(new { error = "INVALID_INVITATION", message = "Invitation not found or expired" });
                }

                // Validate email format from invitation
                if (!new EmailAddressAttribute().IsValid(invitation.Email))
                {
                    _logger.LogWarning("Agent registration failed: Invalid email format in invitation {Email}", invitation.Email);
                    return BadRequest(new { error = "INVALID_EMAIL", message = "The invitation contains an invalid email address" });
                }

                if (invitation.IsUsed)
                {
                    _logger.LogWarning("Agent registration failed: Invitation already used {Token}", registerDto.InvitationToken);
                    return BadRequest(new { error = "INVITATION_USED", message = "This invitation has already been used" });
                }

                if (invitation.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning("Agent registration failed: Invitation expired {Token}", registerDto.InvitationToken);
                    return BadRequest(new { error = "INVITATION_EXPIRED", message = "This invitation has expired" });
                }

                // Check if user already exists with this email
                var existingUser = _userService.GetUserByEmail(invitation.Email);

                if (existingUser != null)
                {
                    // User already exists - add Agent role and accept invitation
                    _logger.LogInformation("Existing user {UserId} accepting agent invitation for email {Email}", existingUser.Id, invitation.Email);

                    // Add Agent role if not already present
                    var currentRoles = _roleService.GetUserRoles(existingUser.Id);
                    if (!currentRoles.Contains("Agent"))
                    {
                        _roleService.AssignRoleToUser(existingUser.Id, "Agent");
                        _logger.LogInformation("Added Agent role to existing user {UserId}", existingUser.Id);
                    }

                    // Accept the invitation
                    var invitationAccepted = await _agentManagementService.AcceptInvitationAsync(registerDto.InvitationToken, existingUser.Id);

                    if (!invitationAccepted)
                    {
                        _logger.LogError("Failed to accept invitation for existing user {UserId} with token {Token}", existingUser.Id, registerDto.InvitationToken);
                        return BadRequest(new { error = "INVITATION_FAILED", message = "Failed to accept the invitation. Please try again." });
                    }

                    // Mark email as verified - invitation proves email ownership
                    await _userService.MarkEmailVerifiedAsync(existingUser.Id);

                    _logger.LogInformation("Agent invitation accepted by existing user {UserId}", existingUser.Id);

                    // Generate auth response for the existing user (refresh their user data to include new role)
                    var updatedUser = _userService.GetUserByEmail(invitation.Email);
                    var authResponse = _authService.GenerateAuthResponse(updatedUser);

                    return Ok(authResponse);
                }
                else
                {
                    // New user - require password and register
                    if (string.IsNullOrEmpty(registerDto.Password) || registerDto.Password.Length < 6)
                    {
                        _logger.LogWarning("Agent registration failed: Weak password");
                        return BadRequest(new { error = "WEAK_PASSWORD", message = "Password must be at least 6 characters long" });
                    }

                    // Create regular registration request with Agent and Player roles
                    var registerRequest = new RegisterRequest
                    {
                        Email = invitation.Email,
                        FirstName = registerDto.FirstName,
                        LastName = registerDto.LastName,
                        Password = registerDto.Password,
                        Phone = registerDto.Phone,
                        DateOfBirth = registerDto.DateOfBirth,
                        ActivityInterests = registerDto.ActivityInterests,
                        Roles = new List<string> { "Player", "Agent" }, // Agent gets both Player and Agent roles
                        PlayerTerms = true, // Auto-accept terms for invited agents
                        BusinessTerms = false,
                        TrainerTerms = false
                    };

                    _logger.LogInformation("Processing agent registration for new user with email {Email}", invitation.Email);

                    // Register the user
                    var authResponse = _authService.Register(registerRequest);

                    // If registration successful, accept the invitation and verify email
                    if (authResponse.User != null)
                    {
                        var invitationAccepted = await _agentManagementService.AcceptInvitationAsync(registerDto.InvitationToken, authResponse.User.Id);

                        if (!invitationAccepted)
                        {
                            _logger.LogError("Failed to accept invitation for user {UserId} with token {Token}", authResponse.User.Id, registerDto.InvitationToken);
                            // Note: User is still registered, but invitation acceptance failed
                        }
                        else
                        {
                            // Mark email as verified - invitation proves email ownership
                            await _userService.MarkEmailVerifiedAsync(authResponse.User.Id);
                            _logger.LogInformation("Agent registration and invitation acceptance successful for user {UserId}", authResponse.User.Id);
                        }
                    }

                    return Ok(authResponse);
                }
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate key") == true || ex.InnerException?.Message?.Contains("unique constraint") == true)
            {
                _logger.LogWarning("Agent registration failed: Email already exists for token {Token}", registerDto.InvitationToken);
                return Conflict(new { error = "EMAIL_EXISTS", message = "An account with this email address already exists" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Agent registration failed: Invalid operation for token {Token}", registerDto.InvitationToken);
                return BadRequest(new { error = "INVALID_OPERATION", message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Agent registration failed: Invalid argument for token {Token}", registerDto.InvitationToken);
                return BadRequest(new { error = "INVALID_DATA", message = ex.Message });
            }
            catch (System.ComponentModel.DataAnnotations.ValidationException ex)
            {
                _logger.LogWarning(ex, "Agent registration failed: Validation error for token {Token}", registerDto.InvitationToken);
                return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Agent registration failed: Unexpected error for token {Token}", registerDto.InvitationToken);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred during registration. Please try again." });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Forgot password validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Invalid request data", details = errors });
                }

                // Validate email format
                if (!new EmailAddressAttribute().IsValid(request.Email))
                {
                    _logger.LogWarning("Forgot password failed: Invalid email format {Email}", request.Email);
                    return BadRequest(new { error = "INVALID_EMAIL", message = "Please provide a valid email address" });
                }

                _logger.LogInformation("Processing password reset request for email {Email}", request.Email);

                // Get the frontend URL from configuration
                var frontendUrl = _configuration["FrontendConfiguration:WebAppUrl"] ?? "http://spotto.pl";
                var resetUrl = $"{frontendUrl}/reset-password";

                await _userService.RequestPasswordResetAsync(request.Email, resetUrl);

                // Always return success to prevent email enumeration
                _logger.LogInformation("Password reset email sent (if user exists) for email {Email}", request.Email);
                return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Forgot password failed: Unexpected error for email {Email}", request.Email);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Reset password validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Invalid request data", details = errors });
                }

                // Validate email format
                if (!new EmailAddressAttribute().IsValid(request.Email))
                {
                    _logger.LogWarning("Reset password failed: Invalid email format {Email}", request.Email);
                    return BadRequest(new { error = "INVALID_EMAIL", message = "Please provide a valid email address" });
                }

                // Validate password strength
                if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 6)
                {
                    _logger.LogWarning("Reset password failed: Weak password for email {Email}", request.Email);
                    return BadRequest(new { error = "WEAK_PASSWORD", message = "Password must be at least 6 characters long" });
                }

                _logger.LogInformation("Processing password reset for email {Email}", request.Email);

                await _userService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);

                _logger.LogInformation("Password reset successful for email {Email}", request.Email);
                return Ok(new { message = "Password has been reset successfully. You can now login with your new password." });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Reset password failed: Invalid or expired token for email {Email}", request.Email);
                return BadRequest(new { error = "INVALID_TOKEN", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reset password failed: Unexpected error for email {Email}", request.Email);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<ActionResult> VerifyEmail([FromQuery] string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("Email verification failed: Empty token");
                    return BadRequest(new { error = "MISSING_TOKEN", message = "Verification token is required" });
                }

                _logger.LogInformation("Processing email verification for token");
                var verified = await _userService.VerifyEmailAsync(token);

                if (!verified)
                {
                    _logger.LogWarning("Email verification failed: Invalid or expired token");
                    return BadRequest(new { error = "INVALID_TOKEN", message = "Invalid or expired verification token. Please request a new verification email." });
                }

                _logger.LogInformation("Email verified successfully");
                return Ok(new { success = true, message = "Email verified successfully! You can now log in to your account." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email verification failed: Unexpected error");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpPost("resend-verification")]
        public async Task<ActionResult> ResendVerificationEmail([FromBody] ResendVerificationRequest request)
        {
            try
            {
                // Validate email format
                if (string.IsNullOrWhiteSpace(request.Email) || !new EmailAddressAttribute().IsValid(request.Email))
                {
                    _logger.LogWarning("Resend verification failed: Invalid email format {Email}", request.Email);
                    return BadRequest(new { error = "INVALID_EMAIL", message = "Please provide a valid email address" });
                }

                _logger.LogInformation("Processing resend verification email for {Email}", request.Email);

                // Get user by email to find userId
                var user = _userService.GetUserByEmail(request.Email);
                if (user == null)
                {
                    // Don't reveal if user exists
                    _logger.LogInformation("Resend verification - user not found (silent) for email {Email}", request.Email);
                    return Ok(new { message = "If an account with that email exists and is not verified, a verification email has been sent." });
                }

                if (user.IsEmailVerified)
                {
                    _logger.LogInformation("Resend verification - email already verified for {Email}", request.Email);
                    return Ok(new { message = "This email address is already verified. You can log in." });
                }

                var frontendUrl = _configuration["FrontendConfiguration:WebAppUrl"] ?? "http://spotto.pl";
                var deepLinkScheme = _configuration["FrontendConfiguration:DeepLinkScheme"] ?? "spotto";

                var sent = await _userService.ResendVerificationEmailAsync(user.Id, frontendUrl, deepLinkScheme);

                _logger.LogInformation("Verification email resent for {Email}", request.Email);
                return Ok(new { message = "If an account with that email exists and is not verified, a verification email has been sent." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resend verification failed: Unexpected error for email {Email}", request.Email);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpPost("delete-account")]
        [Authorize]
        public async Task<ActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
        {
            try
            {
                // Validate request
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    _logger.LogWarning("Delete account validation failed: {Errors}", string.Join(", ", errors));
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Invalid request data", details = errors });
                }

                // Validate email format
                if (!new EmailAddressAttribute().IsValid(request.Email))
                {
                    _logger.LogWarning("Delete account failed: Invalid email format {Email}", request.Email);
                    return BadRequest(new { error = "INVALID_EMAIL", message = "Please provide a valid email address" });
                }

                // Get user ID from JWT claims
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                {
                    _logger.LogWarning("Delete account failed: User ID not found in token");
                    return Unauthorized(new { error = "UNAUTHORIZED", message = "User ID not found in token" });
                }

                _logger.LogInformation("Processing account deletion for userId {UserId}, email {Email}", userId, request.Email);

                // Delete the account
                var deleted = await _userService.DeleteAccountAsync(userId, request.Email, request.Reason);

                if (!deleted)
                {
                    _logger.LogWarning("Delete account failed: User not found for userId {UserId}", userId);
                    return NotFound(new { error = "USER_NOT_FOUND", message = "User not found" });
                }

                _logger.LogInformation("Account deleted successfully for userId {UserId}, email {Email}, reason: {Reason}",
                    userId, request.Email, request.Reason ?? "not provided");

                return Ok(new { message = "Account deletion request received successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Delete account failed: Email mismatch for request {Email}", request.Email);
                return Unauthorized(new { error = "EMAIL_MISMATCH", message = "Email does not match authenticated user" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Delete account failed: Unexpected error for email {Email}", request.Email);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred. Please try again." });
            }
        }
    }

    public class LoginRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }

    public class RefreshRequest
    {
        public required string RefreshToken { get; set; }
    }

    public class ResendVerificationRequest
    {
        public required string Email { get; set; }
    }
}
