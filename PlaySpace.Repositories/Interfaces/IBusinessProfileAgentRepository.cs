using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IBusinessProfileAgentRepository
{
    Task<BusinessProfileAgent> CreateAgentAssignmentAsync(BusinessProfileAgent agentAssignment);
    Task<BusinessProfileAgent?> GetActiveAgentAssignmentAsync(Guid businessProfileId, Guid agentUserId);
    Task<List<BusinessProfileAgent>> GetActiveAgentsByBusinessProfileIdAsync(Guid businessProfileId);
    Task<List<BusinessProfileAgent>> GetBusinessProfilesByAgentUserIdAsync(Guid agentUserId);
    Task<bool> DeactivateAgentAsync(Guid businessProfileId, Guid agentUserId);
    Task<bool> IsAgentActiveForBusinessAsync(Guid agentUserId, Guid businessProfileId);
}