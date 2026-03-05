using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/tpay/dictionaries")]
public class TPayDictionaryController : ControllerBase
{
    private readonly ITPayDictionaryService _dictionaryService;
    private readonly ILogger<TPayDictionaryController> _logger;

    public TPayDictionaryController(ITPayDictionaryService dictionaryService, ILogger<TPayDictionaryController> logger)
    {
        _dictionaryService = dictionaryService;
        _logger = logger;
    }

    [HttpGet("legal-forms")]
    public async Task<ActionResult<List<TPayLegalFormDto>>> GetLegalForms()
    {
        try
        {
            var legalForms = await _dictionaryService.GetLegalFormsAsync();
            return Ok(legalForms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving TPay legal forms");
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve legal forms" });
        }
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<TPayCategoryDto>>> GetCategories([FromQuery] int? parentId = null)
    {
        try
        {
            var categories = parentId.HasValue
                ? await _dictionaryService.GetCategoriesByParentAsync(parentId.Value)
                : await _dictionaryService.GetCategoriesAsync();

            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving TPay categories");
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve categories" });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<TPayDictionariesStatusDto>> GetSyncStatus()
    {
        try
        {
            var status = await _dictionaryService.GetSyncStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving TPay dictionaries sync status");
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve sync status" });
        }
    }

    [HttpPost("sync")]
    [Authorize] // Require authentication for sync operations
    public async Task<ActionResult> SyncDictionaries([FromQuery] string? type = null)
    {
        try
        {
            bool result;
            
            switch (type?.ToLower())
            {
                case "legalforms":
                    result = await _dictionaryService.SyncLegalFormsAsync();
                    break;
                case "categories":
                    result = await _dictionaryService.SyncCategoriesAsync();
                    break;
                default:
                    result = await _dictionaryService.SyncAllDictionariesAsync();
                    break;
            }

            if (result)
            {
                return Ok(new { success = true, message = "Dictionaries synchronized successfully" });
            }
            else
            {
                return StatusCode(500, new { success = false, message = "Dictionary synchronization failed" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TPay dictionaries synchronization");
            return StatusCode(500, new { error = "SYNC_FAILED", message = "Dictionary synchronization failed", details = ex.Message });
        }
    }

    [HttpPost("seed")]
    [Authorize] // Require authentication for seed operations
    public async Task<ActionResult> SeedInitialData()
    {
        try
        {
            await _dictionaryService.SeedInitialDataAsync();
            return Ok(new { success = true, message = "Initial dictionary data seeded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during initial dictionary data seeding");
            return StatusCode(500, new { error = "SEED_FAILED", message = "Initial data seeding failed", details = ex.Message });
        }
    }
}