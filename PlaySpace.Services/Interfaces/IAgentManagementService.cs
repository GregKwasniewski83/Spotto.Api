using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IAgentManagementService
{
    Task<AgentInvitationResponse> InviteAgentAsync(Guid businessProfileId, Guid invitedByUserId, InviteAgentDto inviteDto);
    Task<AgentOperationResponse> GetBusinessAgentsAsync(Guid businessProfileId);
    Task<AgentOperationResponse> RemoveAgentAsync(Guid businessProfileId, Guid agentUserId, Guid removedByUserId);
    Task<AgentInvitationDto?> GetInvitationByTokenAsync(string token);
    Task<bool> AcceptInvitationAsync(string token, Guid userId);
    Task<bool> IsUserBusinessOwnerAsync(Guid userId, Guid businessProfileId);
    Task<bool> IsUserAgentForBusinessAsync(Guid userId, Guid businessProfileId);
    Task<List<Guid>> GetAgentBusinessProfilesAsync(Guid agentUserId);
    Task<List<AgentInvitationDto>> GetPendingInvitationsAsync(Guid businessProfileId);
    Task<bool> CancelInvitationAsync(Guid invitationId, Guid cancelledByUserId);
}