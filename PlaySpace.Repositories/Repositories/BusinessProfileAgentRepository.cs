using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class BusinessProfileAgentRepository : IBusinessProfileAgentRepository
{
    private readonly PlaySpaceDbContext _context;

    public BusinessProfileAgentRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<BusinessProfileAgent> CreateAgentAssignmentAsync(BusinessProfileAgent agentAssignment)
    {
        _context.BusinessProfileAgents.Add(agentAssignment);
        await _context.SaveChangesAsync();
        return agentAssignment;
    }

    public async Task<BusinessProfileAgent?> GetActiveAgentAssignmentAsync(Guid businessProfileId, Guid agentUserId)
    {
        return await _context.BusinessProfileAgents
            .Include(bpa => bpa.BusinessProfile)
            .Include(bpa => bpa.AgentUser)
            .Include(bpa => bpa.AssignedByUser)
            .FirstOrDefaultAsync(bpa => bpa.BusinessProfileId == businessProfileId && 
                                      bpa.AgentUserId == agentUserId && 
                                      bpa.IsActive);
    }

    public async Task<List<BusinessProfileAgent>> GetActiveAgentsByBusinessProfileIdAsync(Guid businessProfileId)
    {
        return await _context.BusinessProfileAgents
            .Include(bpa => bpa.AgentUser)
            .Include(bpa => bpa.AssignedByUser)
            .Where(bpa => bpa.BusinessProfileId == businessProfileId && bpa.IsActive)
            .OrderBy(bpa => bpa.AssignedAt)
            .ToListAsync();
    }

    public async Task<List<BusinessProfileAgent>> GetBusinessProfilesByAgentUserIdAsync(Guid agentUserId)
    {
        return await _context.BusinessProfileAgents
            .Include(bpa => bpa.BusinessProfile)
            .Include(bpa => bpa.AssignedByUser)
            .Where(bpa => bpa.AgentUserId == agentUserId && bpa.IsActive)
            .OrderBy(bpa => bpa.AssignedAt)
            .ToListAsync();
    }

    public async Task<bool> DeactivateAgentAsync(Guid businessProfileId, Guid agentUserId)
    {
        var agentAssignment = await _context.BusinessProfileAgents
            .FirstOrDefaultAsync(bpa => bpa.BusinessProfileId == businessProfileId && 
                                      bpa.AgentUserId == agentUserId && 
                                      bpa.IsActive);

        if (agentAssignment == null)
            return false;

        agentAssignment.IsActive = false;
        agentAssignment.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsAgentActiveForBusinessAsync(Guid agentUserId, Guid businessProfileId)
    {
        return await _context.BusinessProfileAgents
            .AnyAsync(bpa => bpa.AgentUserId == agentUserId && 
                           bpa.BusinessProfileId == businessProfileId && 
                           bpa.IsActive);
    }
}