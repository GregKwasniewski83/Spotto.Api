using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using System.Globalization;

namespace PlaySpace.Services.Services
{
    public class GlobalSettingsService : IGlobalSettingsService
    {
        private readonly IGlobalSettingsRepository _settingsRepository;
        private readonly ILogger<GlobalSettingsService> _logger;

        public GlobalSettingsService(IGlobalSettingsRepository settingsRepository, ILogger<GlobalSettingsService> logger)
        {
            _settingsRepository = settingsRepository;
            _logger = logger;
        }

        public async Task<string?> GetSettingAsync(string key)
        {
            var setting = await _settingsRepository.GetByKeyAsync(key);
            return setting?.Value;
        }

        public async Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default) where T : struct
        {
            var value = await GetSettingAsync(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to convert setting {Key} value '{Value}' to type {Type}, using default", key, value, typeof(T).Name);
                return defaultValue;
            }
        }

        public async Task<decimal?> GetDecimalSettingAsync(string key, decimal? defaultValue = null)
        {
            var value = await GetSettingAsync(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            _logger.LogWarning("Failed to parse setting {Key} value '{Value}' as decimal, using default", key, value);
            return defaultValue;
        }

        public async Task<bool?> GetBooleanSettingAsync(string key, bool? defaultValue = null)
        {
            var value = await GetSettingAsync(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (bool.TryParse(value, out var result))
                return result;

            _logger.LogWarning("Failed to parse setting {Key} value '{Value}' as boolean, using default", key, value);
            return defaultValue;
        }

        public async Task<int?> GetIntegerSettingAsync(string key, int? defaultValue = null)
        {
            var value = await GetSettingAsync(key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;

            if (int.TryParse(value, out var result))
                return result;

            _logger.LogWarning("Failed to parse setting {Key} value '{Value}' as integer, using default", key, value);
            return defaultValue;
        }

        public async Task<GlobalSettingsDto> SetSettingAsync(string key, string value, string? description = null)
        {
            _logger.LogInformation("Setting configuration key {Key} to value {Value}", key, value);
            
            var setting = await _settingsRepository.UpsertAsync(key, value, description);
            return MapToDto(setting);
        }

        public async Task<bool> DeleteSettingAsync(string key)
        {
            _logger.LogInformation("Deleting configuration key {Key}", key);
            return await _settingsRepository.DeleteAsync(key);
        }

        public async Task<List<GlobalSettingsDto>> GetAllSettingsAsync()
        {
            var settings = await _settingsRepository.GetAllAsync();
            return settings.Select(MapToDto).ToList();
        }

        public async Task<RefundSettingsDto> GetRefundSettingsAsync()
        {
            var refundFeePercentage = await GetDecimalSettingAsync(SettingsKeys.RefundFeePercentage, 20m);
            var maxRefundDaysAdvance = await GetIntegerSettingAsync(SettingsKeys.MaxRefundDaysAdvance, 30);
            var enableRefunds = await GetBooleanSettingAsync(SettingsKeys.EnableRefunds, true);

            return new RefundSettingsDto
            {
                RefundFeePercentage = refundFeePercentage.Value,
                MaxRefundDaysAdvance = maxRefundDaysAdvance.Value,
                EnableRefunds = enableRefunds.Value
            };
        }

        public async Task<RefundSettingsDto> UpdateRefundSettingsAsync(RefundSettingsDto refundSettings)
        {
            _logger.LogInformation("Updating refund settings: Fee={Fee}%, MaxDays={MaxDays}, Enabled={Enabled}", 
                refundSettings.RefundFeePercentage, refundSettings.MaxRefundDaysAdvance, refundSettings.EnableRefunds);

            // Update each setting individually
            await SetSettingAsync(SettingsKeys.RefundFeePercentage, 
                refundSettings.RefundFeePercentage.ToString(CultureInfo.InvariantCulture),
                "Percentage fee deducted from refund amount (e.g., 20 = 20% fee, user gets 80% refund)");

            await SetSettingAsync(SettingsKeys.MaxRefundDaysAdvance, 
                refundSettings.MaxRefundDaysAdvance.ToString(),
                "Maximum number of days in advance that refunds are allowed");

            await SetSettingAsync(SettingsKeys.EnableRefunds, 
                refundSettings.EnableRefunds.ToString(),
                "Whether refunds are enabled system-wide");

            return refundSettings;
        }

        public async Task<decimal> CalculateRefundAmountAsync(decimal originalAmount)
        {
            var refundFeePercentage = await GetRefundFeePercentageAsync();
            var refundAmount = originalAmount * (100 - refundFeePercentage) / 100;
            
            _logger.LogDebug("Calculated refund amount: Original={Original}, Fee={Fee}%, Refund={Refund}", 
                originalAmount, refundFeePercentage, refundAmount);
            
            return refundAmount;
        }

        public async Task<decimal> GetRefundFeePercentageAsync()
        {
            return await GetDecimalSettingAsync(SettingsKeys.RefundFeePercentage, 20m) ?? 20m;
        }

        private static GlobalSettingsDto MapToDto(GlobalSettings setting)
        {
            return new GlobalSettingsDto
            {
                Id = setting.Id,
                Key = setting.Key,
                Value = setting.Value,
                Description = setting.Description,
                CreatedAt = setting.CreatedAt,
                UpdatedAt = setting.UpdatedAt
            };
        }
    }
}