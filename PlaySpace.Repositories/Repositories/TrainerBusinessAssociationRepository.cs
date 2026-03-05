using Microsoft.EntityFrameworkCore;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Data;
using PlaySpace.Repositories.Interfaces;

namespace PlaySpace.Repositories.Repositories;

public class TrainerBusinessAssociationRepository : ITrainerBusinessAssociationRepository
{
    private readonly PlaySpaceDbContext _context;

    public TrainerBusinessAssociationRepository(PlaySpaceDbContext context)
    {
        _context = context;
    }

    public async Task<TrainerBusinessAssociation> CreateAssociationRequestAsync(Guid trainerProfileId, Guid businessProfileId, string confirmationToken)
    {
        var association = new TrainerBusinessAssociation
        {
            Id = Guid.NewGuid(),
            TrainerProfileId = trainerProfileId,
            BusinessProfileId = businessProfileId,
            Status = AssociationStatus.Pending,
            ConfirmationToken = confirmationToken,
            ConfirmationTokenExpiresAt = DateTime.UtcNow.AddDays(7), // Token valid for 7 days
            RequestedAt = DateTime.UtcNow
        };

        _context.TrainerBusinessAssociations.Add(association);
        await _context.SaveChangesAsync();

        return association;
    }

    public async Task<TrainerBusinessAssociation?> GetByIdAsync(Guid id)
    {
        return await _context.TrainerBusinessAssociations
            .Include(a => a.TrainerProfile)
            .Include(a => a.BusinessProfile)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<TrainerBusinessAssociation?> GetByTokenAsync(string token)
    {
        return await _context.TrainerBusinessAssociations
            .Include(a => a.TrainerProfile)
            .Include(a => a.BusinessProfile)
            .FirstOrDefaultAsync(a => a.ConfirmationToken == token);
    }

    public async Task<TrainerBusinessAssociation?> GetByTrainerAndBusinessAsync(Guid trainerProfileId, Guid businessProfileId)
    {
        return await _context.TrainerBusinessAssociations
            .Include(a => a.TrainerProfile)
            .Include(a => a.BusinessProfile)
            .FirstOrDefaultAsync(a => a.TrainerProfileId == trainerProfileId && a.BusinessProfileId == businessProfileId);
    }

    public async Task<List<TrainerBusinessAssociation>> GetByTrainerProfileIdAsync(Guid trainerProfileId, AssociationStatus? status = null)
    {
        var query = _context.TrainerBusinessAssociations
            .Include(a => a.TrainerProfile)
            .Include(a => a.BusinessProfile)
            .Where(a => a.TrainerProfileId == trainerProfileId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        return await query.OrderByDescending(a => a.RequestedAt).ToListAsync();
    }

    public async Task<List<TrainerBusinessAssociation>> GetByBusinessProfileIdAsync(Guid businessProfileId, AssociationStatus? status = null)
    {
        var query = _context.TrainerBusinessAssociations
            .Include(a => a.TrainerProfile)
            .Include(a => a.BusinessProfile)
            .Where(a => a.BusinessProfileId == businessProfileId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        return await query.OrderByDescending(a => a.RequestedAt).ToListAsync();
    }

    public async Task<List<TrainerBusinessAssociation>> GetPendingRequestsForBusinessAsync(Guid businessProfileId)
    {
        return await _context.TrainerBusinessAssociations
            .Include(a => a.TrainerProfile)
            .Include(a => a.BusinessProfile)
            .Where(a => a.BusinessProfileId == businessProfileId && a.Status == AssociationStatus.Pending)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync();
    }

    public async Task<List<TrainerBusinessAssociation>> GetConfirmedAssociationsForTrainerAsync(Guid trainerProfileId)
    {
        return await _context.TrainerBusinessAssociations
            .Include(a => a.TrainerProfile)
            .Include(a => a.BusinessProfile)
            .Where(a => a.TrainerProfileId == trainerProfileId && a.Status == AssociationStatus.Confirmed)
            .OrderByDescending(a => a.ConfirmedAt)
            .ToListAsync();
    }

    public async Task<TrainerBusinessAssociation> ConfirmAssociationAsync(Guid associationId, bool canRunOwnTrainings = false, bool isEmployee = false, string? color = null)
    {
        var association = await _context.TrainerBusinessAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.Status = AssociationStatus.Confirmed;
        association.ConfirmedAt = DateTime.UtcNow;
        association.CanRunOwnTrainings = canRunOwnTrainings;
        association.IsEmployee = isEmployee;
        association.Color = color;
        association.ConfirmationToken = null; // Clear token after confirmation
        association.ConfirmationTokenExpiresAt = null;

        await _context.SaveChangesAsync();
        return association;
    }

    public async Task<List<string>> GetUsedColorsForTrainerAsync(Guid trainerProfileId)
    {
        return await _context.TrainerBusinessAssociations
            .Where(a => a.TrainerProfileId == trainerProfileId &&
                        a.Status == AssociationStatus.Confirmed &&
                        a.Color != null)
            .Select(a => a.Color!)
            .ToListAsync();
    }

    public async Task<TrainerBusinessAssociation> RejectAssociationAsync(Guid associationId, string? reason = null)
    {
        var association = await _context.TrainerBusinessAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.Status = AssociationStatus.Rejected;
        association.RejectedAt = DateTime.UtcNow;
        association.RejectionReason = reason;
        association.ConfirmationToken = null;
        association.ConfirmationTokenExpiresAt = null;

        await _context.SaveChangesAsync();
        return association;
    }

    public async Task<bool> DeleteAssociationAsync(Guid associationId)
    {
        var association = await _context.TrainerBusinessAssociations.FindAsync(associationId);
        if (association == null)
            return false;

        _context.TrainerBusinessAssociations.Remove(association);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsAssociationConfirmedAsync(Guid trainerProfileId, Guid businessProfileId)
    {
        return await _context.TrainerBusinessAssociations
            .AnyAsync(a => a.TrainerProfileId == trainerProfileId &&
                          a.BusinessProfileId == businessProfileId &&
                          a.Status == AssociationStatus.Confirmed);
    }

    public async Task<TrainerBusinessAssociation> UpdatePricingAsync(Guid associationId, decimal hourlyRate, decimal vatRate)
    {
        var association = await _context.TrainerBusinessAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.HourlyRate = hourlyRate;
        association.VatRate = vatRate;
        association.GrossHourlyRate = hourlyRate * (1 + vatRate / 100);

        await _context.SaveChangesAsync();
        return association;
    }

    public async Task<TrainerBusinessAssociation> UpdatePermissionsAsync(Guid associationId, bool canRunOwnTrainings, bool isEmployee, int? maxNumberOfUsers)
    {
        var association = await _context.TrainerBusinessAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.CanRunOwnTrainings = canRunOwnTrainings;
        association.IsEmployee = isEmployee;
        association.MaxNumberOfUsers = maxNumberOfUsers;

        await _context.SaveChangesAsync();
        return association;
    }

    public async Task<TrainerBusinessAssociation> UpdateConfirmationTokenAsync(Guid associationId, string newToken, DateTime newExpiry)
    {
        var association = await _context.TrainerBusinessAssociations.FindAsync(associationId);
        if (association == null)
            throw new InvalidOperationException("Association not found");

        association.ConfirmationToken = newToken;
        association.ConfirmationTokenExpiresAt = newExpiry;

        await _context.SaveChangesAsync();
        return association;
    }
}
