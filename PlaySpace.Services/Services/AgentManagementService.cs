using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.Configuration;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace PlaySpace.Services.Services;

public class AgentManagementService : IAgentManagementService
{
    private readonly IAgentInvitationRepository _agentInvitationRepository;
    private readonly IBusinessProfileAgentRepository _businessProfileAgentRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailService _emailService;
    private readonly IRoleService _roleService;
    private readonly FrontendConfiguration _frontendConfig;
    private readonly ILogger<AgentManagementService> _logger;

    public AgentManagementService(
        IAgentInvitationRepository agentInvitationRepository,
        IBusinessProfileAgentRepository businessProfileAgentRepository,
        IBusinessProfileRepository businessProfileRepository,
        IUserRepository userRepository,
        IEmailService emailService,
        IRoleService roleService,
        IOptions<FrontendConfiguration> frontendConfig,
        ILogger<AgentManagementService> logger)
    {
        _agentInvitationRepository = agentInvitationRepository;
        _businessProfileAgentRepository = businessProfileAgentRepository;
        _businessProfileRepository = businessProfileRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _roleService = roleService;
        _frontendConfig = frontendConfig.Value;
        _logger = logger;
    }

    public async Task<AgentInvitationResponse> InviteAgentAsync(Guid businessProfileId, Guid invitedByUserId, InviteAgentDto inviteDto)
    {
        try
        {
            _logger.LogInformation("Creating agent invitation for email {Email} to business profile {BusinessProfileId} by user {UserId}", 
                inviteDto.Email, businessProfileId, invitedByUserId);

            // Check if business profile exists and user owns it
            if (!await IsUserBusinessOwnerAsync(invitedByUserId, businessProfileId))
            {
                return new AgentInvitationResponse
                {
                    Success = false,
                    Message = "You don't have permission to invite agents for this business profile"
                };
            }

            // Check if user is already an agent for this business
            var existingUser = await _userRepository.GetUserByEmailAsync(inviteDto.Email);
            if (existingUser != null)
            {
                var isAlreadyAgent = await IsUserAgentForBusinessAsync(existingUser.Id, businessProfileId);
                if (isAlreadyAgent)
                {
                    return new AgentInvitationResponse
                    {
                        Success = false,
                        Message = "This user is already an agent for this business profile"
                    };
                }
            }

            // Check if there's already a pending invitation for this email and business
            var existingInvitation = await _agentInvitationRepository.GetPendingInvitationByEmailAndBusinessAsync(inviteDto.Email, businessProfileId);
            if (existingInvitation != null)
            {
                return new AgentInvitationResponse
                {
                    Success = false,
                    Message = "There's already a pending invitation for this email address"
                };
            }

            // Create invitation
            var invitation = new AgentInvitation
            {
                Id = Guid.NewGuid(),
                Email = inviteDto.Email,
                InvitationToken = GenerateInvitationToken(),
                BusinessProfileId = businessProfileId,
                InvitedByUserId = invitedByUserId,
                ExpiresAt = DateTime.UtcNow.AddDays(7), // 7 days expiry
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _agentInvitationRepository.CreateInvitationAsync(invitation);

            // Send invitation email
            await SendInvitationEmailAsync(invitation);

            var invitationDto = await MapToInvitationDto(invitation);

            _logger.LogInformation("Agent invitation created successfully with ID {InvitationId} for email {Email}", 
                invitation.Id, inviteDto.Email);

            return new AgentInvitationResponse
            {
                Success = true,
                Message = "Agent invitation sent successfully",
                Invitation = invitationDto
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent invitation for email {Email} to business profile {BusinessProfileId}", 
                inviteDto.Email, businessProfileId);
            
            return new AgentInvitationResponse
            {
                Success = false,
                Message = "Failed to send agent invitation. Please try again."
            };
        }
    }

    public async Task<AgentOperationResponse> GetBusinessAgentsAsync(Guid businessProfileId)
    {
        try
        {
            var agents = await _businessProfileAgentRepository.GetActiveAgentsByBusinessProfileIdAsync(businessProfileId);
            var agentDtos = new List<BusinessAgentDto>();

            foreach (var agent in agents)
            {
                var user = await _userRepository.GetUserByIdAsync(agent.AgentUserId);
                var assignedByUser = await _userRepository.GetUserByIdAsync(agent.AssignedByUserId);
                
                if (user != null && assignedByUser != null)
                {
                    agentDtos.Add(new BusinessAgentDto
                    {
                        Id = agent.Id,
                        UserId = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        AssignedAt = agent.AssignedAt,
                        AssignedByUserName = $"{assignedByUser.FirstName} {assignedByUser.LastName}",
                        IsActive = agent.IsActive
                    });
                }
            }

            return new AgentOperationResponse
            {
                Success = true,
                Message = "Agents retrieved successfully",
                Agents = agentDtos
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents for business profile {BusinessProfileId}", businessProfileId);
            
            return new AgentOperationResponse
            {
                Success = false,
                Message = "Failed to retrieve agents"
            };
        }
    }

    public async Task<AgentOperationResponse> RemoveAgentAsync(Guid businessProfileId, Guid agentUserId, Guid removedByUserId)
    {
        try
        {
            // Check if user has permission to remove agents
            if (!await IsUserBusinessOwnerAsync(removedByUserId, businessProfileId))
            {
                return new AgentOperationResponse
                {
                    Success = false,
                    Message = "You don't have permission to remove agents from this business profile"
                };
            }

            // Find and deactivate the agent assignment
            var success = await _businessProfileAgentRepository.DeactivateAgentAsync(businessProfileId, agentUserId);
            
            if (!success)
            {
                return new AgentOperationResponse
                {
                    Success = false,
                    Message = "Agent assignment not found or already removed"
                };
            }

            _logger.LogInformation("Agent {AgentUserId} removed from business profile {BusinessProfileId} by user {RemovedByUserId}",
                agentUserId, businessProfileId, removedByUserId);

            // If the agent has no other active assignments, strip the Agent role
            var remainingAssignments = await _businessProfileAgentRepository.GetBusinessProfilesByAgentUserIdAsync(agentUserId);
            if (remainingAssignments.Count == 0)
            {
                _roleService.RemoveRoleFromUser(agentUserId, "Agent");
                _logger.LogInformation("Agent role removed from user {AgentUserId} - no remaining business assignments", agentUserId);
            }

            // Return updated agent list
            return await GetBusinessAgentsAsync(businessProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing agent {AgentUserId} from business profile {BusinessProfileId}", 
                agentUserId, businessProfileId);
            
            return new AgentOperationResponse
            {
                Success = false,
                Message = "Failed to remove agent"
            };
        }
    }

    public async Task<AgentInvitationDto?> GetInvitationByTokenAsync(string token)
    {
        try
        {
            var invitation = await _agentInvitationRepository.GetInvitationByTokenAsync(token);
            if (invitation == null)
            {
                return null;
            }

            return await MapToInvitationDto(invitation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invitation by token");
            return null;
        }
    }

    public async Task<bool> AcceptInvitationAsync(string token, Guid userId)
    {
        try
        {
            var invitation = await _agentInvitationRepository.GetInvitationByTokenAsync(token);
            if (invitation == null || invitation.IsUsed || invitation.ExpiresAt < DateTime.UtcNow)
            {
                return false;
            }

            // Mark invitation as used
            invitation.IsUsed = true;
            invitation.AcceptedByUserId = userId;
            invitation.AcceptedAt = DateTime.UtcNow;
            invitation.UpdatedAt = DateTime.UtcNow;

            await _agentInvitationRepository.UpdateInvitationAsync(invitation);

            // Create business profile agent assignment
            var agentAssignment = new BusinessProfileAgent
            {
                Id = Guid.NewGuid(),
                BusinessProfileId = invitation.BusinessProfileId,
                AgentUserId = userId,
                AssignedByUserId = invitation.InvitedByUserId,
                AssignedAt = DateTime.UtcNow,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _businessProfileAgentRepository.CreateAgentAssignmentAsync(agentAssignment);

            _logger.LogInformation("Agent invitation {InvitationId} accepted by user {UserId}", invitation.Id, userId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invitation with token {Token} by user {UserId}", token, userId);
            return false;
        }
    }

    public async Task<bool> IsUserBusinessOwnerAsync(Guid userId, Guid businessProfileId)
    {
        try
        {
            var businessProfile = await _businessProfileRepository.GetBusinessProfileByIdAsync(businessProfileId);
            return businessProfile?.UserId == userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking business ownership for user {UserId} and business {BusinessProfileId}", 
                userId, businessProfileId);
            return false;
        }
    }

    public async Task<bool> IsUserAgentForBusinessAsync(Guid userId, Guid businessProfileId)
    {
        try
        {
            var assignment = await _businessProfileAgentRepository.GetActiveAgentAssignmentAsync(businessProfileId, userId);
            return assignment != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking agent assignment for user {UserId} and business {BusinessProfileId}", 
                userId, businessProfileId);
            return false;
        }
    }

    public async Task<List<Guid>> GetAgentBusinessProfilesAsync(Guid agentUserId)
    {
        try
        {
            var assignments = await _businessProfileAgentRepository.GetBusinessProfilesByAgentUserIdAsync(agentUserId);
            return assignments.Select(a => a.BusinessProfileId).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving business profiles for agent {AgentUserId}", agentUserId);
            return new List<Guid>();
        }
    }

    public async Task<List<AgentInvitationDto>> GetPendingInvitationsAsync(Guid businessProfileId)
    {
        try
        {
            var invitations = await _agentInvitationRepository.GetPendingInvitationsByBusinessProfileIdAsync(businessProfileId);
            var invitationDtos = new List<AgentInvitationDto>();

            foreach (var invitation in invitations)
            {
                invitationDtos.Add(await MapToInvitationDto(invitation));
            }

            return invitationDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending invitations for business profile {BusinessProfileId}", businessProfileId);
            return new List<AgentInvitationDto>();
        }
    }

    public async Task<bool> CancelInvitationAsync(Guid invitationId, Guid cancelledByUserId)
    {
        try
        {
            var invitation = await _agentInvitationRepository.GetInvitationByIdAsync(invitationId);
            if (invitation == null || invitation.IsUsed)
            {
                return false;
            }

            // Check if user has permission to cancel the invitation
            if (!await IsUserBusinessOwnerAsync(cancelledByUserId, invitation.BusinessProfileId))
            {
                return false;
            }

            await _agentInvitationRepository.DeleteInvitationAsync(invitationId);
            
            _logger.LogInformation("Agent invitation {InvitationId} cancelled by user {UserId}", invitationId, cancelledByUserId);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling invitation {InvitationId} by user {UserId}", invitationId, cancelledByUserId);
            return false;
        }
    }

    private string GenerateInvitationToken()
    {
        // Generate a secure random token
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private async Task SendInvitationEmailAsync(AgentInvitation invitation)
    {
        try
        {
            var businessProfile = await _businessProfileRepository.GetBusinessProfileByIdAsync(invitation.BusinessProfileId);
            var invitedByUser = await _userRepository.GetUserByIdAsync(invitation.InvitedByUserId);

            if (businessProfile == null || invitedByUser == null)
            {
                throw new InvalidOperationException("Business profile or inviting user not found");
            }

            var baseUrl = _frontendConfig.WebAppUrl.TrimEnd('/');
            var registrationUrl = $"{baseUrl}/register/agent?token={invitation.InvitationToken}";
            
            var emailBody = $@"
                <h2>Zostałeś zaproszony do zarządzania {businessProfile.DisplayName}</h2>
                <p>Cześć,</p>
                <p>{invitedByUser.FirstName} {invitedByUser.LastName} zaprosił Cię do zostania agentem dla {businessProfile.DisplayName}.</p>
                <p>Jako agent będziesz mógł zarządzać harmonogramami obiektów i slotami czasowymi dla tej firmy.</p>
                <p><a href=""{registrationUrl}"" style=""background-color: #4CAF50; color: white; padding: 14px 20px; text-decoration: none; border-radius: 4px;"">Zaakceptuj Zaproszenie</a></p>
                <p>To zaproszenie wygaśnie {invitation.ExpiresAt:yyyy-MM-dd HH:mm} UTC.</p>
                <p>Jeśli nie spodziewałeś się tego zaproszenia, możesz bezpiecznie zignorować tę wiadomość.</p>
                <p>Pozdrawiam,<br>Zespół Spotto</p>
            ";

            // Send agent invitation email
            await _emailService.SendAgentInvitationEmailAsync(invitation.Email, "Zaproszenie do zostania agentem", emailBody);
            
            _logger.LogInformation("Agent invitation email sent to {Email} for business profile {BusinessProfileId}", 
                invitation.Email, invitation.BusinessProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send agent invitation email to {Email}", invitation.Email);
            throw; // Re-throw to handle in calling method
        }
    }

    private async Task<AgentInvitationDto> MapToInvitationDto(AgentInvitation invitation)
    {
        var businessProfile = await _businessProfileRepository.GetBusinessProfileByIdAsync(invitation.BusinessProfileId);
        var invitedByUser = await _userRepository.GetUserByIdAsync(invitation.InvitedByUserId);
        var acceptedByUser = invitation.AcceptedByUserId.HasValue 
            ? await _userRepository.GetUserByIdAsync(invitation.AcceptedByUserId.Value)
            : null;

        return new AgentInvitationDto
        {
            Id = invitation.Id,
            Email = invitation.Email,
            BusinessProfileId = invitation.BusinessProfileId,
            BusinessProfileName = businessProfile?.DisplayName ?? "Unknown",
            InvitedByUserId = invitation.InvitedByUserId,
            InvitedByUserName = invitedByUser != null ? $"{invitedByUser.FirstName} {invitedByUser.LastName}" : "Unknown",
            ExpiresAt = invitation.ExpiresAt,
            IsUsed = invitation.IsUsed,
            AcceptedByUserId = invitation.AcceptedByUserId,
            AcceptedByUserName = acceptedByUser != null ? $"{acceptedByUser.FirstName} {acceptedByUser.LastName}" : null,
            AcceptedAt = invitation.AcceptedAt,
            CreatedAt = invitation.CreatedAt
        };
    }
}