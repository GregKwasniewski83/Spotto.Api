using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class AgentInvitationRepository : IAgentInvitationRepository
{
    private readonly PlaySpaceDbContext _context;

    public AgentInvitationRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<AgentInvitation> CreateInvitationAsync(AgentInvitation invitation)
    {
        _context.AgentInvitations.Add(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task<AgentInvitation?> GetInvitationByIdAsync(Guid id)
    {
        return await _context.AgentInvitations
            .Include(i => i.BusinessProfile)
            .Include(i => i.InvitedByUser)
            .Include(i => i.AcceptedByUser)
            .FirstOrDefaultAsync(i => i.Id == id);
    }

    public async Task<AgentInvitation?> GetInvitationByTokenAsync(string token)
    {
        return await _context.AgentInvitations
            .Include(i => i.BusinessProfile)
            .Include(i => i.InvitedByUser)
            .Include(i => i.AcceptedByUser)
            .FirstOrDefaultAsync(i => i.InvitationToken == token);
    }

    public async Task<AgentInvitation?> GetPendingInvitationByEmailAndBusinessAsync(string email, Guid businessProfileId)
    {
        return await _context.AgentInvitations
            .FirstOrDefaultAsync(i => i.Email == email && 
                                    i.BusinessProfileId == businessProfileId && 
                                    !i.IsUsed && 
                                    i.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<List<AgentInvitation>> GetPendingInvitationsByBusinessProfileIdAsync(Guid businessProfileId)
    {
        return await _context.AgentInvitations
            .Include(i => i.InvitedByUser)
            .Where(i => i.BusinessProfileId == businessProfileId && 
                       !i.IsUsed && 
                       i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<AgentInvitation> UpdateInvitationAsync(AgentInvitation invitation)
    {
        invitation.UpdatedAt = DateTime.UtcNow;
        _context.AgentInvitations.Update(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task<bool> DeleteInvitationAsync(Guid invitationId)
    {
        var invitation = await _context.AgentInvitations.FindAsync(invitationId);
        if (invitation == null)
            return false;

        _context.AgentInvitations.Remove(invitation);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task CleanupExpiredInvitationsAsync()
    {
        var expiredInvitations = await _context.AgentInvitations
            .Where(i => !i.IsUsed && i.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        _context.AgentInvitations.RemoveRange(expiredInvitations);
        await _context.SaveChangesAsync();
    }
}