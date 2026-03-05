using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IAgentInvitationRepository
{
    Task<AgentInvitation> CreateInvitationAsync(AgentInvitation invitation);
    Task<AgentInvitation?> GetInvitationByIdAsync(Guid id);
    Task<AgentInvitation?> GetInvitationByTokenAsync(string token);
    Task<AgentInvitation?> GetPendingInvitationByEmailAndBusinessAsync(string email, Guid businessProfileId);
    Task<List<AgentInvitation>> GetPendingInvitationsByBusinessProfileIdAsync(Guid businessProfileId);
    Task<AgentInvitation> UpdateInvitationAsync(AgentInvitation invitation);
    Task<bool> DeleteInvitationAsync(Guid invitationId);
    Task CleanupExpiredInvitationsAsync();
}