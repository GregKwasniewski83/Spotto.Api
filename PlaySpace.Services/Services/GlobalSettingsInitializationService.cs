using Microsoft.Extensions.Logging;
using PlaySpace.Domain.Models;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services
{
    public class GlobalSettingsInitializationService
    {
        private readonly IGlobalSettingsService _settingsService;
        private readonly ILogger<GlobalSettingsInitializationService> _logger;

        public GlobalSettingsInitializationService(
            IGlobalSettingsService settingsService, 
            ILogger<GlobalSettingsInitializationService> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task InitializeDefaultSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Initializing default global settings...");

                // Initialize refund fee percentage (20%)
                var refundFeePercentage = await _settingsService.GetSettingAsync(SettingsKeys.RefundFeePercentage);
                if (string.IsNullOrEmpty(refundFeePercentage))
                {
                    await _settingsService.SetSettingAsync(
                        SettingsKeys.RefundFeePercentage, 
                        "20", 
                        "Percentage fee deducted from refund amount (e.g., 20 = 20% fee, user gets 80% refund)");
                    _logger.LogInformation("Set default refund fee percentage to 20%");
                }

                // Initialize max refund days advance (30 days)
                var maxRefundDays = await _settingsService.GetSettingAsync(SettingsKeys.MaxRefundDaysAdvance);
                if (string.IsNullOrEmpty(maxRefundDays))
                {
                    await _settingsService.SetSettingAsync(
                        SettingsKeys.MaxRefundDaysAdvance, 
                        "30", 
                        "Maximum number of days in advance that refunds are allowed");
                    _logger.LogInformation("Set default max refund days advance to 30 days");
                }

                // Initialize enable refunds (true)
                var enableRefunds = await _settingsService.GetSettingAsync(SettingsKeys.EnableRefunds);
                if (string.IsNullOrEmpty(enableRefunds))
                {
                    await _settingsService.SetSettingAsync(
                        SettingsKeys.EnableRefunds, 
                        "true", 
                        "Whether refunds are enabled system-wide");
                    _logger.LogInformation("Enabled refunds by default");
                }

                _logger.LogInformation("Global settings initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during global settings initialization");
                throw;
            }
        }

        public async Task<string> GetSettingsStatusAsync()
        {
            try
            {
                var refundSettings = await _settingsService.GetRefundSettingsAsync();
                return $"Refund Settings: Fee={refundSettings.RefundFeePercentage}%, MaxDays={refundSettings.MaxRefundDaysAdvance}, Enabled={refundSettings.EnableRefunds}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting settings status");
                return "Error retrieving settings status";
            }
        }
    }
}