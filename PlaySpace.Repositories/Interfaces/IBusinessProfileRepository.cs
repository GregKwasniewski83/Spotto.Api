using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;

namespace PlaySpace.Repositories.Interfaces;

public interface IBusinessProfileRepository
{
    BusinessProfile? GetBusinessProfileByUserId(Guid userId);
    BusinessProfile? GetBusinessProfileById(Guid businessProfileId);
    Task<BusinessProfile?> GetBusinessProfileByIdAsync(Guid businessProfileId);
    BusinessProfile CreateBusinessProfile(CreateBusinessProfileDto profileDto, Guid userId);
    BusinessProfile? UpdateBusinessProfile(Guid businessProfileId, UpdateBusinessProfileDto profileDto);
    bool DeleteBusinessProfile(Guid businessProfileId);
    bool BusinessProfileExists(Guid userId);
    bool NipExists(string nip, Guid? excludeBusinessProfileId = null);
    Task UpdateAvatarAsync(Guid businessProfileId, string avatarUrl);
    void UpdateTPayMerchantData(Guid businessProfileId, string merchantId, string accountId, string? posId, string? activationLink, int verificationStatus);

    // KSeF management methods
    Task<bool> UpdateKSeFCredentialsAsync(Guid businessProfileId, string token, string environment);
    Task<bool> UpdateKSeFStatusAsync(Guid businessProfileId, bool enabled);
    Task<BusinessProfile?> GetBusinessProfileWithKSeFAsync(Guid businessProfileId);
    Task UpdateKSeFLastSyncAsync(Guid businessProfileId);

    // Parent-child relationship management
    Task<bool> UpdateParentChildRelationshipAsync(Guid childBusinessProfileId, Guid? parentBusinessProfileId, bool useParentTPay, bool useParentNipForInvoices);
    Task<bool> ClearParentChildRelationshipAsync(Guid childBusinessProfileId);
}