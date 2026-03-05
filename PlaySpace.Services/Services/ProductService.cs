using Microsoft.Extensions.Options;
using PlaySpace.Domain.Configuration;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using System.Text.Json;

namespace PlaySpace.Services.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IBusinessProfileAgentRepository _businessProfileAgentRepository;
    private readonly IFacilityRepository _facilityRepository;
    private readonly FrontendConfiguration _frontendConfig;

    public ProductService(
        IProductRepository productRepository,
        IBusinessProfileRepository businessProfileRepository,
        IBusinessProfileAgentRepository businessProfileAgentRepository,
        IFacilityRepository facilityRepository,
        IOptions<FrontendConfiguration> frontendConfig)
    {
        _productRepository = productRepository;
        _businessProfileRepository = businessProfileRepository;
        _businessProfileAgentRepository = businessProfileAgentRepository;
        _facilityRepository = facilityRepository;
        _frontendConfig = frontendConfig.Value;
    }

    public ProductResponseDto CreateProduct(Guid businessProfileId, CreateProductDto dto, Guid userId)
    {
        // Validate input
        ValidateProductInput(dto);

        // Get business profile for authorization
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (businessProfile == null)
            throw new ArgumentException("Business profile not found");

        // Check authorization
        var canManage = CanManageProductAsync(userId, businessProfileId).Result;
        if (!canManage)
            throw new UnauthorizedAccessException("You can only manage products for your business");

        // Validate facility restrictions
        string? facilityIdsJson = null;
        if (!dto.AppliesToAllFacilities)
        {
            if (dto.FacilityIds == null || dto.FacilityIds.Count == 0)
                throw new ArgumentException("FacilityIds must be provided when AppliesToAllFacilities is false");

            // Validate all facilities belong to this business profile
            ValidateFacilitiesBelongToBusiness(dto.FacilityIds, businessProfileId);
            facilityIdsJson = JsonSerializer.Serialize(dto.FacilityIds);
        }

        // Parse period enum (sent as string from FE)
        if (!Enum.TryParse<ProductPeriod>(dto.Period, true, out var period))
            throw new ArgumentException($"Invalid period value. Valid values are: {string.Join(", ", Enum.GetNames<ProductPeriod>())}");

        // Calculate dates
        var startDate = DateTime.UtcNow;
        var endDate = CalculateEndDate(startDate, period, dto.NumOfPeriods);

        // Create entity
        var product = new Product
        {
            Id = Guid.NewGuid(),
            BusinessProfileId = businessProfileId,
            UserId = userId,
            Title = dto.Title,
            Subtitle = dto.Subtitle,
            Description = dto.Description,
            Price = dto.Price,
            VatRate = dto.VatRate,
            GrossPrice = dto.GrossPrice,
            Usage = dto.Usage,
            Period = period,
            NumOfPeriods = dto.NumOfPeriods,
            PayableInApp = dto.PayableInApp,
            PayableWithTrainer = dto.PayableWithTrainer,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true,
            AppliesToAllFacilities = dto.AppliesToAllFacilities,
            FacilityIds = facilityIdsJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = _productRepository.CreateProduct(product);
        return MapToResponseDto(created);
    }

    public ProductResponseDto? GetProduct(Guid productId)
    {
        var product = _productRepository.GetProduct(productId);
        return product != null ? MapToResponseDto(product) : null;
    }

    public List<ProductResponseDto> GetProductsByBusinessProfile(Guid businessProfileId)
    {
        var products = _productRepository.GetProductsByBusinessProfile(businessProfileId);
        return products.Select(MapToResponseDto).ToList();
    }

    public ProductResponseDto? UpdateProduct(Guid businessProfileId, Guid productId, UpdateProductDto dto, Guid userId)
    {
        // Validate input
        ValidateProductInput(dto);

        // Get existing product
        var existingProduct = _productRepository.GetProduct(productId);
        if (existingProduct == null)
            throw new ArgumentException("Product not found");

        // Verify product belongs to the specified business profile
        if (existingProduct.BusinessProfileId != businessProfileId)
            throw new ArgumentException("Product does not belong to this business profile");

        // Check authorization
        var canManage = CanManageProductAsync(userId, businessProfileId).Result;
        if (!canManage)
            throw new UnauthorizedAccessException("You can only manage products for your business");

        // Validate facility restrictions
        string? facilityIdsJson = null;
        if (!dto.AppliesToAllFacilities)
        {
            if (dto.FacilityIds == null || dto.FacilityIds.Count == 0)
                throw new ArgumentException("FacilityIds must be provided when AppliesToAllFacilities is false");

            // Validate all facilities belong to this business profile
            ValidateFacilitiesBelongToBusiness(dto.FacilityIds, businessProfileId);
            facilityIdsJson = JsonSerializer.Serialize(dto.FacilityIds);
        }

        // Parse period enum (sent as string from FE)
        if (!Enum.TryParse<ProductPeriod>(dto.Period, true, out var period))
            throw new ArgumentException($"Invalid period value. Valid values are: {string.Join(", ", Enum.GetNames<ProductPeriod>())}");

        // Recalculate dates based on current time and new period/numOfPeriods
        var startDate = DateTime.UtcNow;
        var endDate = CalculateEndDate(startDate, period, dto.NumOfPeriods);

        // Update entity
        existingProduct.Title = dto.Title;
        existingProduct.Subtitle = dto.Subtitle;
        existingProduct.Description = dto.Description;
        existingProduct.Price = dto.Price;
        existingProduct.VatRate = dto.VatRate;
        existingProduct.GrossPrice = dto.GrossPrice;
        existingProduct.Usage = dto.Usage;
        existingProduct.Period = period;
        existingProduct.NumOfPeriods = dto.NumOfPeriods;
        existingProduct.PayableInApp = dto.PayableInApp;
        existingProduct.PayableWithTrainer = dto.PayableWithTrainer;
        existingProduct.StartDate = startDate;
        existingProduct.EndDate = endDate;
        existingProduct.AppliesToAllFacilities = dto.AppliesToAllFacilities;
        existingProduct.FacilityIds = facilityIdsJson;
        existingProduct.UpdatedAt = DateTime.UtcNow;

        var updated = _productRepository.UpdateProduct(existingProduct);
        return updated != null ? MapToResponseDto(updated) : null;
    }

    public bool DeleteProduct(Guid businessProfileId, Guid productId, Guid userId)
    {
        // Get existing product
        var existingProduct = _productRepository.GetProduct(productId);
        if (existingProduct == null)
            throw new ArgumentException("Product not found");

        // Verify product belongs to the specified business profile
        if (existingProduct.BusinessProfileId != businessProfileId)
            throw new ArgumentException("Product does not belong to this business profile");

        // Check authorization
        var canManage = CanManageProductAsync(userId, businessProfileId).Result;
        if (!canManage)
            throw new UnauthorizedAccessException("You can only manage products for your business");

        return _productRepository.SoftDeleteProduct(productId);
    }

    public List<ProductResponseDto> GetMyProducts(Guid userId)
    {
        var products = _productRepository.GetUserProducts(userId);
        return products.Select(MapToResponseDto).ToList();
    }

    // Private helper methods

    private async Task<bool> CanManageProductAsync(Guid userId, Guid businessProfileId)
    {
        // Check if user is business profile owner
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(businessProfileId);
        if (businessProfile?.UserId == userId)
            return true;

        // Check if user is authorized agent for this business
        return await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(userId, businessProfileId);
    }

    private void ValidateProductInput(CreateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length > 255)
            throw new ArgumentException("Title must be between 1 and 255 characters");

        if (dto.Subtitle != null && dto.Subtitle.Length > 500)
            throw new ArgumentException("Subtitle must not exceed 500 characters");

        if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 2000)
            throw new ArgumentException("Description must be between 1 and 2000 characters");

        if (dto.Price <= 0)
            throw new ArgumentException("Price must be greater than 0");

        if (dto.Usage <= 0)
            throw new ArgumentException("Usage must be greater than 0");

        if (dto.NumOfPeriods <= 0)
            throw new ArgumentException("Number of periods must be greater than 0");
    }

    private void ValidateProductInput(UpdateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length > 255)
            throw new ArgumentException("Title must be between 1 and 255 characters");

        if (dto.Subtitle != null && dto.Subtitle.Length > 500)
            throw new ArgumentException("Subtitle must not exceed 500 characters");

        if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 2000)
            throw new ArgumentException("Description must be between 1 and 2000 characters");

        if (dto.Price <= 0)
            throw new ArgumentException("Price must be greater than 0");

        if (dto.Usage <= 0)
            throw new ArgumentException("Usage must be greater than 0");

        if (dto.NumOfPeriods <= 0)
            throw new ArgumentException("Number of periods must be greater than 0");
    }

    private DateTime CalculateEndDate(DateTime startDate, ProductPeriod period, int numOfPeriods)
    {
        return period switch
        {
            ProductPeriod.Days => startDate.AddDays(numOfPeriods),
            ProductPeriod.Weeks => startDate.AddDays(numOfPeriods * 7),
            ProductPeriod.Months => startDate.AddMonths(numOfPeriods),
            ProductPeriod.Years => startDate.AddYears(numOfPeriods),
            ProductPeriod.Lifetime => startDate.AddYears(100), // Lifetime = 100 years from start
            _ => throw new ArgumentException("Invalid period type")
        };
    }

    public ProductSearchResponseDto SearchProducts(ProductSearchDto searchDto)
    {
        var (products, total) = _productRepository.SearchProducts(searchDto);

        var limit = searchDto.Limit ?? 20;
        if (limit > 100) limit = 100;
        if (limit < 1) limit = 20;

        var page = searchDto.Page ?? 1;
        if (page < 1) page = 1;

        var totalPages = (int)Math.Ceiling((double)total / limit);

        return new ProductSearchResponseDto
        {
            Products = products.Select(MapToResponseDto).ToList(),
            Total = total,
            Page = page,
            Limit = limit,
            TotalPages = totalPages
        };
    }

    private ProductResponseDto MapToResponseDto(Product product)
    {
        // Parse FacilityIds from JSON
        List<string>? facilityIds = null;
        if (!string.IsNullOrEmpty(product.FacilityIds))
        {
            facilityIds = JsonSerializer.Deserialize<List<string>>(product.FacilityIds);
        }

        // Generate deep linking URLs
        var productId = product.Id.ToString();
        string? publicLinkUrl = null;
        string? deepLinkUrl = null;

        if (!string.IsNullOrEmpty(_frontendConfig.WebAppUrl))
        {
            publicLinkUrl = $"{_frontendConfig.WebAppUrl.TrimEnd('/')}/product/{productId}";
        }

        if (!string.IsNullOrEmpty(_frontendConfig.DeepLinkScheme))
        {
            deepLinkUrl = $"{_frontendConfig.DeepLinkScheme}://product/{productId}";
        }

        return new ProductResponseDto
        {
            Id = productId,
            BusinessProfileId = product.BusinessProfileId.ToString(),
            Title = product.Title,
            Subtitle = product.Subtitle,
            Description = product.Description,
            Price = product.Price,
            VatRate = product.VatRate,
            GrossPrice = product.GrossPrice,
            Usage = product.Usage,
            Period = product.Period.ToString(),
            NumOfPeriods = product.NumOfPeriods,
            PayableInApp = product.PayableInApp,
            PayableWithTrainer = product.PayableWithTrainer,
            StartDate = product.StartDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            EndDate = product.EndDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            IsActive = product.IsActive,
            AppliesToAllFacilities = product.AppliesToAllFacilities,
            FacilityIds = facilityIds,
            CreatedAt = product.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            UpdatedAt = product.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            BusinessName = product.BusinessProfile?.DisplayName ?? product.BusinessProfile?.CompanyName,
            BusinessCity = product.BusinessProfile?.City,
            PublicLinkUrl = publicLinkUrl,
            DeepLinkUrl = deepLinkUrl
        };
    }

    private void ValidateFacilitiesBelongToBusiness(List<string> facilityIds, Guid businessProfileId)
    {
        foreach (var facilityIdStr in facilityIds)
        {
            if (!Guid.TryParse(facilityIdStr, out var facilityId))
                throw new ArgumentException($"Invalid facility ID format: {facilityIdStr}");

            var facility = _facilityRepository.GetFacility(facilityId);
            if (facility == null)
                throw new ArgumentException($"Facility not found: {facilityIdStr}");

            if (facility.BusinessProfileId != businessProfileId)
                throw new ArgumentException($"Facility {facilityIdStr} does not belong to this business profile");
        }
    }
}
