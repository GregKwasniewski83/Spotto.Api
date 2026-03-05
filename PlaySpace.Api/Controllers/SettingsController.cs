using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly IGlobalSettingsService _settingsService;
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(IGlobalSettingsService settingsService, ILogger<SettingsController> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<GlobalSettingsDto>>> GetAllSettings()
        {
            try
            {
                var settings = await _settingsService.GetAllSettingsAsync();
                return Ok(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all settings");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve settings" });
            }
        }

        [HttpGet("{key}")]
        public async Task<ActionResult<string>> GetSetting(string key)
        {
            try
            {
                var value = await _settingsService.GetSettingAsync(key);
                if (value == null)
                    return NotFound(new { error = "SETTING_NOT_FOUND", message = $"Setting with key '{key}' not found" });

                return Ok(new { key = key, value = value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving setting {Key}", key);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve setting" });
            }
        }

        [HttpPost]
        public async Task<ActionResult<GlobalSettingsDto>> CreateSetting([FromBody] CreateSettingDto createSettingDto)
        {
            try
            {
                var setting = await _settingsService.SetSettingAsync(
                    createSettingDto.Key, 
                    createSettingDto.Value, 
                    createSettingDto.Description);

                return StatusCode(201, setting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating setting {Key}", createSettingDto.Key);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to create setting" });
            }
        }

        [HttpPut("{key}")]
        public async Task<ActionResult<GlobalSettingsDto>> UpdateSetting(string key, [FromBody] UpdateSettingDto updateSettingDto)
        {
            try
            {
                var setting = await _settingsService.SetSettingAsync(key, updateSettingDto.Value, updateSettingDto.Description);
                return Ok(setting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating setting {Key}", key);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to update setting" });
            }
        }

        [HttpDelete("{key}")]
        public async Task<ActionResult> DeleteSetting(string key)
        {
            try
            {
                var deleted = await _settingsService.DeleteSettingAsync(key);
                if (!deleted)
                    return NotFound(new { error = "SETTING_NOT_FOUND", message = $"Setting with key '{key}' not found" });

                return Ok(new { message = "Setting deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting setting {Key}", key);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to delete setting" });
            }
        }

        [HttpGet("refund")]
        public async Task<ActionResult<RefundSettingsDto>> GetRefundSettings()
        {
            try
            {
                var refundSettings = await _settingsService.GetRefundSettingsAsync();
                return Ok(refundSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving refund settings");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve refund settings" });
            }
        }

        [HttpPut("refund")]
        public async Task<ActionResult<RefundSettingsDto>> UpdateRefundSettings([FromBody] RefundSettingsDto refundSettings)
        {
            try
            {
                // Validate input
                if (refundSettings.RefundFeePercentage < 0 || refundSettings.RefundFeePercentage > 100)
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Refund fee percentage must be between 0 and 100" });

                if (refundSettings.MaxRefundDaysAdvance < 0 || refundSettings.MaxRefundDaysAdvance > 365)
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Max refund days advance must be between 0 and 365" });

                var updatedSettings = await _settingsService.UpdateRefundSettingsAsync(refundSettings);
                return Ok(updatedSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating refund settings");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to update refund settings" });
            }
        }

        [HttpPost("refund/calculate")]
        public async Task<ActionResult> CalculateRefundAmount([FromBody] CalculateRefundRequest request)
        {
            try
            {
                if (request.OriginalAmount <= 0)
                    return BadRequest(new { error = "VALIDATION_ERROR", message = "Original amount must be greater than 0" });

                var refundAmount = await _settingsService.CalculateRefundAmountAsync(request.OriginalAmount);
                var feePercentage = await _settingsService.GetRefundFeePercentageAsync();
                var feeAmount = request.OriginalAmount - refundAmount;

                return Ok(new
                {
                    originalAmount = request.OriginalAmount,
                    refundAmount = refundAmount,
                    feeAmount = feeAmount,
                    feePercentage = feePercentage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating refund amount");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to calculate refund amount" });
            }
        }

        [HttpPost("initialize")]
        public async Task<ActionResult> InitializeDefaultSettings()
        {
            try
            {
                // Check if refund fee percentage already exists
                var existingRefundFee = await _settingsService.GetSettingAsync(SettingsKeys.RefundFeePercentage);
                
                if (string.IsNullOrEmpty(existingRefundFee))
                {
                    // Initialize default settings
                    await _settingsService.SetSettingAsync(SettingsKeys.RefundFeePercentage, "20", 
                        "Percentage fee deducted from refund amount (e.g., 20 = 20% fee, user gets 80% refund)");
                    
                    await _settingsService.SetSettingAsync(SettingsKeys.MaxRefundDaysAdvance, "30", 
                        "Maximum number of days in advance that refunds are allowed");
                    
                    await _settingsService.SetSettingAsync(SettingsKeys.EnableRefunds, "true", 
                        "Whether refunds are enabled system-wide");

                    _logger.LogInformation("Default settings initialized: 20% refund fee, 30 days max advance, refunds enabled");
                    
                    return Ok(new { 
                        message = "Default settings initialized successfully",
                        settings = new {
                            refundFeePercentage = 20,
                            maxRefundDaysAdvance = 30,
                            enableRefunds = true
                        }
                    });
                }
                else
                {
                    var refundSettings = await _settingsService.GetRefundSettingsAsync();
                    return Ok(new { 
                        message = "Settings already exist",
                        settings = refundSettings
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing default settings");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to initialize settings" });
            }
        }
    }

    public class CalculateRefundRequest
    {
        public decimal OriginalAmount { get; set; }
    }
}