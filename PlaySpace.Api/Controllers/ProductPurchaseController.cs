using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PlaySpace.Domain.Attributes;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/purchases")]
[Authorize]
public class ProductPurchaseController : ControllerBase
{
    private readonly IProductPurchaseService _productPurchaseService;
    private readonly ILogger<ProductPurchaseController> _logger;

    public ProductPurchaseController(
        IProductPurchaseService productPurchaseService,
        ILogger<ProductPurchaseController> logger)
    {
        _productPurchaseService = productPurchaseService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult> PurchaseProduct([FromBody] CreateProductPurchaseDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Product purchase failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("User {UserId} initiating purchase for product {ProductId}",
                userId, dto.ProductId);

            var (purchase, payment) = await _productPurchaseService.InitiatePurchaseAsync(dto, userId);

            _logger.LogInformation("Product purchase initiated: PaymentId={PaymentId}, ProductId={ProductId}",
                payment.Id, dto.ProductId);

            return Ok(new
            {
                purchase,
                payment
            });
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Product purchase validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Product purchase argument error: {Message}", ex.Message);
            return BadRequest(new { error = "INVALID_ARGUMENT", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product purchase failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while processing the purchase" });
        }
    }

    [HttpGet("my-products")]
    public async Task<ActionResult<UserProductsResponseDto>> GetMyProducts()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Get my products failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("User {UserId} retrieving purchased products", userId);

            var products = await _productPurchaseService.GetUserPurchasesAsync(userId);

            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve user products: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while retrieving products" });
        }
    }

    [HttpPost("{purchaseId}/use")]
    public async Task<ActionResult<ProductUsageResponseDto>> UseProduct(
        Guid purchaseId,
        [FromBody] UseProductDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Product usage failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("User {UserId} using product purchase {PurchaseId}", userId, purchaseId);

            var result = await _productPurchaseService.UseProductAsync(purchaseId, userId, dto);

            _logger.LogInformation("Product used successfully: PurchaseId={PurchaseId}, RemainingUsage={RemainingUsage}",
                purchaseId, result.RemainingUsage);

            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Product purchase not found: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized product usage attempt: {Message}", ex.Message);
            return Forbid();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Product usage validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Product usage failed: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while using the product" });
        }
    }

    [HttpGet("{purchaseId}")]
    public async Task<ActionResult<ProductPurchaseResponseDto>> GetPurchase(Guid purchaseId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Get purchase failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("User {UserId} retrieving purchase {PurchaseId}", userId, purchaseId);

            var purchase = await _productPurchaseService.GetPurchaseAsync(purchaseId, userId);

            return Ok(purchase);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Product purchase not found: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized purchase access attempt: {Message}", ex.Message);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve purchase: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while retrieving the purchase" });
        }
    }

    // Agent endpoints
    [HttpGet("business/{businessProfileId}")]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<AgentProductPurchaseListDto>> GetBusinessPurchases(
        Guid businessProfileId,
        [FromQuery] AgentProductPurchaseFilterDto filter)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Get business purchases failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Agent {UserId} retrieving purchases for business {BusinessProfileId}",
                userId, businessProfileId);

            var result = await _productPurchaseService.GetBusinessPurchasesAsync(
                businessProfileId, userId, filter);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized business purchases access: {Message}", ex.Message);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve business purchases: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while retrieving purchases" });
        }
    }

    [HttpPost("{purchaseId}/mark-used")]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<AgentProductPurchaseDto>> MarkPurchaseAsUsed(Guid purchaseId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Mark purchase as used failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Agent {UserId} marking purchase {PurchaseId} as used", userId, purchaseId);

            var result = await _productPurchaseService.MarkPurchaseAsUsedAsync(purchaseId, userId);

            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Purchase not found: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized mark-used attempt: {Message}", ex.Message);
            return Forbid();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Mark purchase as used validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark purchase as used: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while marking the purchase as used" });
        }
    }

    [HttpPut("{purchaseId}/extend")]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<AgentProductPurchaseDto>> ExtendPurchaseExpiry(
        Guid purchaseId,
        [FromBody] ExtendProductPurchaseDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Extend purchase failed: User not authenticated");
                return Unauthorized("User not authenticated");
            }

            _logger.LogInformation("Agent {UserId} extending purchase {PurchaseId} to {NewExpiryDate}",
                userId, purchaseId, dto.NewExpiryDate);

            var result = await _productPurchaseService.ExtendPurchaseExpiryAsync(purchaseId, userId, dto);

            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Purchase not found: {Message}", ex.Message);
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized purchase extension attempt: {Message}", ex.Message);
            return Forbid();
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Purchase extension validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend purchase: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while extending the purchase" });
        }
    }
}
