using PlaySpace.Domain.Models;

namespace PlaySpace.Services.Interfaces
{
    public interface ITPayService
    {
        Task<string> GetAccessTokenAsync();
        Task<TPayTransactionResponse> CreateTransactionAsync(TPayTransactionRequest request);
        Task<TPayMarketplaceTransactionResponse> CreateMarketplaceTransactionAsync(TPayMarketplaceTransactionRequest request);
        Task<bool> ValidateNotificationAsync(TPayNotification notification, string md5Key);
        string GenerateMd5Hash(TPayNotification notification, string securityCode);
        Task<TPayBusinessRegistrationResponse> RegisterBusinessAsync(TPayBusinessRegistrationRequest request);
        Task<TPayDictionaryResponse<TPayLegalFormItem>> GetLegalFormsAsync();
        Task<TPayDictionaryResponse<TPayCategoryItem>> GetCategoriesAsync();
        Task<TPayPosResponse> GetPosInfoAsync();
        Task<TPayRefundResponse> CreateRefundAsync(string transactionId, TPayRefundRequest request);
    }
}