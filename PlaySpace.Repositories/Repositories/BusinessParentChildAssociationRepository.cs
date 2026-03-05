using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class BusinessParentChildAssociationRepository : IBusinessParentChildAssociationRepository
{
    private readonly PlaySpaceDbContext _context;

    public BusinessParentChildAssociationRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<BusinessParentChildAssociation> CreateAssociationRequestAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId, string confirmationToken)
    {
        var association = new BusinessParentChildAssociation
        {
            Id = Guid.NewGuid(),
            ChildBusinessProfileId = childBusinessProfileId,
            ParentBusinessProfileId = parentBusinessProfileId,
            Status = ParentChildAssociationStatus.Pending,
            ConfirmationToken = confirmationToken,
            ConfirmationTokenExpiresAt = DateTime.UtcNow.AddDays(7),
            RequestedAt = DateTime.UtcNow
        };

        _context.BusinessParentChildAssociations.Add(association);
        await _context.SaveChangesAsync();

        return association;
    }

    public async Task<BusinessParentChildAssociation?> GetByIdAsync(Guid id)
    {
        return await _context.BusinessParentChildAssociations
            .Include(a => a.ChildBusinessProfile)
            .Include(a => a.ParentBusinessProfile)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<BusinessParentChildAssociation?> GetByTokenAsync(string token)
    {
        return await _context.BusinessParentChildAssociations
            .Include(a => a.ChildBusinessProfile)
            .Include(a => a.ParentBusinessProfile)
            .FirstOrDefaultAsync(a => a.ConfirmationToken == token);
    }

    public async Task<BusinessParentChildAssociation?> GetByChildAndParentAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId)
    {
        return await _context.BusinessParentChildAssociations
            .Include(a => a.ChildBusinessProfile)
            .Include(a => a.ParentBusinessProfile)
            .FirstOrDefaultAsync(a => a.ChildBusinessProfileId == childBusinessProfileId && a.ParentBusinessProfileId == parentBusinessProfileId);
    }

    public async Task<List<BusinessParentChildAssociation>> GetByChildBusinessProfileIdAsync(Guid childBusinessProfileId, ParentChildAssociationStatus? status = null)
    {
        var query = _context.BusinessParentChildAssociations
            .Include(a => a.ChildBusinessProfile)
            .Include(a => a.ParentBusinessProfile)
            .Where(a => a.ChildBusinessProfileId == childBusinessProfileId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        return await query.OrderByDescending(a => a.RequestedAt).ToListAsync();
    }

    public async Task<List<BusinessParentChildAssociation>> GetByParentBusinessProfileIdAsync(Guid parentBusinessProfileId, ParentChildAssociationStatus? status = null)
    {
        var query = _context.BusinessParentChildAssociations
            .Include(a => a.ChildBusinessProfile)
            .Include(a => a.ParentBusinessProfile)
            .Where(a => a.ParentBusinessProfileId == parentBusinessProfileId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        return await query.OrderByDescending(a => a.RequestedAt).ToListAsync();
    }

    public async Task<List<BusinessParentChildAssociation>> GetPendingRequestsForParentAsync(Guid parentBusinessProfileId)
    {
        return await _context.BusinessParentChildAssociations
            .Include(a => a.ChildBusinessProfile)
            .Include(a => a.ParentBusinessProfile)
            .Where(a => a.ParentBusinessProfileId == parentBusinessProfileId && a.Status == ParentChildAssociationStatus.Pending)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync();
    }

    public async Task<BusinessParentChildAssociation?> GetConfirmedAssociationForChildAsync(Guid childBusinessProfileId)
    {
        return await _context.BusinessParentChildAssociations
            .Include(a => a.ChildBusinessProfile)
            .Include(a => a.ParentBusinessProfile)
            .FirstOrDefaultAsync(a => a.ChildBusinessProfileId == childBusinessProfileId && a.Status == ParentChildAssociationStatus.Confirmed);
    }

    public async Task<BusinessParentChildAssociation> ConfirmAssociationAsync(Guid associationId, bool useParentTPay, bool useParentNipForInvoices)
    {
        var association = await _context.BusinessParentChildAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.Status = ParentChildAssociationStatus.Confirmed;
        association.ConfirmedAt = DateTime.UtcNow;
        association.UseParentTPay = useParentTPay;
        association.UseParentNipForInvoices = useParentNipForInvoices;
        association.ConfirmationToken = null;
        association.ConfirmationTokenExpiresAt = null;

        await _context.SaveChangesAsync();
        return association;
    }

    public async Task<BusinessParentChildAssociation> RejectAssociationAsync(Guid associationId, string? reason = null)
    {
        var association = await _context.BusinessParentChildAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.Status = ParentChildAssociationStatus.Rejected;
        association.RejectedAt = DateTime.UtcNow;
        association.RejectionReason = reason;
        association.ConfirmationToken = null;
        association.ConfirmationTokenExpiresAt = null;

        await _context.SaveChangesAsync();
        return association;
    }

    public async Task<bool> DeleteAssociationAsync(Guid associationId)
    {
        var association = await _context.BusinessParentChildAssociations.FindAsync(associationId);
        if (association == null)
            return false;

        _context.BusinessParentChildAssociations.Remove(association);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsAssociationConfirmedAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId)
    {
        return await _context.BusinessParentChildAssociations
            .AnyAsync(a => a.ChildBusinessProfileId == childBusinessProfileId &&
                          a.ParentBusinessProfileId == parentBusinessProfileId &&
                          a.Status == ParentChildAssociationStatus.Confirmed);
    }

    public async Task<BusinessParentChildAssociation> UpdatePermissionsAsync(Guid associationId, bool useParentTPay, bool useParentNipForInvoices)
    {
        var association = await _context.BusinessParentChildAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.UseParentTPay = useParentTPay;
        association.UseParentNipForInvoices = useParentNipForInvoices;

        await _context.SaveChangesAsync();
        return association;
    }
}
