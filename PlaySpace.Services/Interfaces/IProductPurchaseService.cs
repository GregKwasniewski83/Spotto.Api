using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces;

public interface IProductPurchaseService
{
    Task<(ProductPurchaseResponseDto purchase, PaymentDto payment)> InitiatePurchaseAsync(
        CreateProductPurchaseDto dto, Guid userId);

    Task<ProductPurchaseResponseDto> CreatePurchaseFromPaymentAsync(Guid paymentId);

    Task<ProductUsageResponseDto> UseProductAsync(Guid purchaseId, Guid userId, UseProductDto dto);

    Task<ProductPurchaseResponseDto> GetPurchaseAsync(Guid purchaseId, Guid userId);

    Task<UserProductsResponseDto> GetUserPurchasesAsync(Guid userId);

    // Agent methods
    Task<AgentProductPurchaseListDto> GetBusinessPurchasesAsync(
        Guid businessProfileId,
        Guid agentUserId,
        AgentProductPurchaseFilterDto filter);

    Task<AgentProductPurchaseDto> ExtendPurchaseExpiryAsync(
        Guid purchaseId,
        Guid agentUserId,
        ExtendProductPurchaseDto dto);

    Task<AgentProductPurchaseDto> MarkPurchaseAsUsedAsync(Guid purchaseId, Guid agentUserId);
}
