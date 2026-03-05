using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlaySpace.Domain.Attributes;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Api.Controllers;

[ApiController]
[Route("api/category")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)
    {
        _categoryService = categoryService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<CategoryDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var categories = await _categoryService.GetAllAsync(includeInactive);
        return Ok(categories);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<CategoryDto>> GetById(Guid id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category == null)
            return NotFound(new { error = "NOT_FOUND", message = $"Category {id} not found" });

        return Ok(category);
    }

    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<CategoryDto>> GetBySlug(string slug)
    {
        var category = await _categoryService.GetBySlugAsync(slug);
        if (category == null)
            return NotFound(new { error = "NOT_FOUND", message = $"Category '{slug}' not found" });

        return Ok(category);
    }

    [HttpPost]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CreateCategoryDto dto)
    {
        try
        {
            var category = await _categoryService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Category creation validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create category: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while creating the category" });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, [FromBody] UpdateCategoryDto dto)
    {
        try
        {
            var category = await _categoryService.UpdateAsync(id, dto);
            if (category == null)
                return NotFound(new { error = "NOT_FOUND", message = $"Category {id} not found" });

            return Ok(category);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Category update validation failed: {Message}", ex.Message);
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update category: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while updating the category" });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [RequireRole("Business", "Agent")]
    public async Task<ActionResult> Delete(Guid id)
    {
        try
        {
            var deleted = await _categoryService.DeleteAsync(id);
            if (!deleted)
                return NotFound(new { error = "NOT_FOUND", message = $"Category {id} not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete category: {Message}", ex.Message);
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An error occurred while deleting the category" });
        }
    }
}
