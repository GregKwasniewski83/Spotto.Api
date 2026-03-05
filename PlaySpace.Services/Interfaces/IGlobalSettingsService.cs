using PlaySpace.Domain.DTOs;

namespace PlaySpace.Services.Interfaces
{
    public interface IGlobalSettingsService
    {
        Task<string?> GetSettingAsync(string key);
        Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default) where T : struct;
        Task<decimal?> GetDecimalSettingAsync(string key, decimal? defaultValue = null);
        Task<bool?> GetBooleanSettingAsync(string key, bool? defaultValue = null);
        Task<int?> GetIntegerSettingAsync(string key, int? defaultValue = null);
        
        Task<GlobalSettingsDto> SetSettingAsync(string key, string value, string? description = null);
        Task<bool> DeleteSettingAsync(string key);
        
        Task<List<GlobalSettingsDto>> GetAllSettingsAsync();
        Task<RefundSettingsDto> GetRefundSettingsAsync();
        Task<RefundSettingsDto> UpdateRefundSettingsAsync(RefundSettingsDto refundSettings);
        
        // Helper methods for refund calculations
        Task<decimal> CalculateRefundAmountAsync(decimal originalAmount);
        Task<decimal> GetRefundFeePercentageAsync();
    }
}