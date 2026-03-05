using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaySpace.Domain.DTOs;
using PlaySpace.Services.Interfaces;
using System.Security.Claims;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/business-profile")]
[Authorize]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("~/api/products")]
    [AllowAnonymous]
    public ActionResult<ProductSearchResponseDto> SearchProducts([FromQuery] ProductSearchDto searchDto)
    {
        try
        {
            var result = _productService.SearchProducts(searchDto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error searching products", error = ex.Message });
        }
    }

    /// <summary>
    /// Get product by ID - Deep linking endpoint for sharing products
    /// </summary>
    [HttpGet("~/api/products/{productId}")]
    [AllowAnonymous]
    public ActionResult<ProductResponseDto> GetProductById(Guid productId)
    {
        try
        {
            var product = _productService.GetProduct(productId);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            return Ok(product);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving product", error = ex.Message });
        }
    }

    [HttpPost("{businessProfileId}/products")]
    public ActionResult<ProductResponseDto> CreateProduct(
        Guid businessProfileId,
        [FromBody] CreateProductDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var product = _productService.CreateProduct(businessProfileId, dto, userId);
            return CreatedAtAction(
                nameof(GetProduct),
                new { businessProfileId, productId = product.Id },
                product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating product", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}/products")]
    [AllowAnonymous]
    public ActionResult<List<ProductResponseDto>> GetProductsByBusinessProfile(Guid businessProfileId)
    {
        try
        {
            var products = _productService.GetProductsByBusinessProfile(businessProfileId);
            return Ok(products);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving products", error = ex.Message });
        }
    }

    [HttpGet("{businessProfileId}/products/{productId}")]
    [AllowAnonymous]
    public ActionResult<ProductResponseDto> GetProduct(Guid businessProfileId, Guid productId)
    {
        try
        {
            var product = _productService.GetProduct(productId);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            // Verify product belongs to the specified business profile
            if (product.BusinessProfileId != businessProfileId.ToString())
            {
                return NotFound(new { message = "Product not found in this business profile" });
            }

            return Ok(product);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving product", error = ex.Message });
        }
    }

    [HttpPut("{businessProfileId}/products/{productId}")]
    public ActionResult<ProductResponseDto> UpdateProduct(
        Guid businessProfileId,
        Guid productId,
        [FromBody] UpdateProductDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var product = _productService.UpdateProduct(businessProfileId, productId, dto, userId);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            return Ok(product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating product", error = ex.Message });
        }
    }

    [HttpDelete("{businessProfileId}/products/{productId}")]
    public ActionResult DeleteProduct(Guid businessProfileId, Guid productId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var success = _productService.DeleteProduct(businessProfileId, productId, userId);
            if (!success)
            {
                return NotFound(new { message = "Product not found" });
            }

            return Ok(new { message = "Product deleted successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting product", error = ex.Message });
        }
    }

    [HttpGet("my-products")]
    public ActionResult<List<ProductResponseDto>> GetMyProducts()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(new { message = "User ID not found in token" });
            }

            var products = _productService.GetMyProducts(userId);
            return Ok(products);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving products", error = ex.Message });
        }
    }
}
