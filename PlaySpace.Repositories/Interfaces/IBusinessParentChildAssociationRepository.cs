using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface IBusinessParentChildAssociationRepository
{
    Task<BusinessParentChildAssociation> CreateAssociationRequestAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId, string confirmationToken);
    Task<BusinessParentChildAssociation?> GetByIdAsync(Guid id);
    Task<BusinessParentChildAssociation?> GetByTokenAsync(string token);
    Task<BusinessParentChildAssociation?> GetByChildAndParentAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId);
    Task<List<BusinessParentChildAssociation>> GetByChildBusinessProfileIdAsync(Guid childBusinessProfileId, ParentChildAssociationStatus? status = null);
    Task<List<BusinessParentChildAssociation>> GetByParentBusinessProfileIdAsync(Guid parentBusinessProfileId, ParentChildAssociationStatus? status = null);
    Task<List<BusinessParentChildAssociation>> GetPendingRequestsForParentAsync(Guid parentBusinessProfileId);
    Task<BusinessParentChildAssociation?> GetConfirmedAssociationForChildAsync(Guid childBusinessProfileId);
    Task<BusinessParentChildAssociation> ConfirmAssociationAsync(Guid associationId, bool useParentTPay, bool useParentNipForInvoices);
    Task<BusinessParentChildAssociation> RejectAssociationAsync(Guid associationId, string? reason = null);
    Task<bool> DeleteAssociationAsync(Guid associationId);
    Task<bool> IsAssociationConfirmedAsync(Guid childBusinessProfileId, Guid parentBusinessProfileId);
    Task<BusinessParentChildAssociation> UpdatePermissionsAsync(Guid associationId, bool useParentTPay, bool useParentNipForInvoices);
}
