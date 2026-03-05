using PlaySpace.Domain.Models;

namespace PlaySpace.Repositories.Interfaces;

public interface ITrainerBusinessAssociationRepository
{
    Task<TrainerBusinessAssociation> CreateAssociationRequestAsync(Guid trainerProfileId, Guid businessProfileId, string confirmationToken);
    Task<TrainerBusinessAssociation?> GetByIdAsync(Guid id);
    Task<TrainerBusinessAssociation?> GetByTokenAsync(string token);
    Task<TrainerBusinessAssociation?> GetByTrainerAndBusinessAsync(Guid trainerProfileId, Guid businessProfileId);
    Task<List<TrainerBusinessAssociation>> GetByTrainerProfileIdAsync(Guid trainerProfileId, AssociationStatus? status = null);
    Task<List<TrainerBusinessAssociation>> GetByBusinessProfileIdAsync(Guid businessProfileId, AssociationStatus? status = null);
    Task<List<TrainerBusinessAssociation>> GetPendingRequestsForBusinessAsync(Guid businessProfileId);
    Task<List<TrainerBusinessAssociation>> GetConfirmedAssociationsForTrainerAsync(Guid trainerProfileId);
    Task<TrainerBusinessAssociation> ConfirmAssociationAsync(Guid associationId, bool canRunOwnTrainings = false, bool isEmployee = false, string? color = null);
    Task<TrainerBusinessAssociation> RejectAssociationAsync(Guid associationId, string? reason = null);
    Task<List<string>> GetUsedColorsForTrainerAsync(Guid trainerProfileId);
    Task<bool> DeleteAssociationAsync(Guid associationId);
    Task<bool> IsAssociationConfirmedAsync(Guid trainerProfileId, Guid businessProfileId);
    Task<TrainerBusinessAssociation> UpdatePricingAsync(Guid associationId, decimal hourlyRate, decimal vatRate);
    Task<TrainerBusinessAssociation> UpdatePermissionsAsync(Guid associationId, bool canRunOwnTrainings, bool isEmployee, int? maxNumberOfUsers);
    Task<TrainerBusinessAssociation> UpdateConfirmationTokenAsync(Guid associationId, string newToken, DateTime newExpiry);
}
