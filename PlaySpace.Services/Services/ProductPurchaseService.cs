using Microsoft.Extensions.Logging;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using System.Text.Json;

namespace PlaySpace.Services.Services;

public class ProductPurchaseService : IProductPurchaseService
{
    private readonly IProductPurchaseRepository _purchaseRepository;
    private readonly IProductUsageLogRepository _usageLogRepository;
    private readonly IProductRepository _productRepository;
    private readonly IPaymentService _paymentService;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IBusinessProfileAgentRepository _businessProfileAgentRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly ILogger<ProductPurchaseService> _logger;

    public ProductPurchaseService(
        IProductPurchaseRepository purchaseRepository,
        IProductUsageLogRepository usageLogRepository,
        IProductRepository productRepository,
        IPaymentService paymentService,
        IPaymentRepository paymentRepository,
        IBusinessProfileAgentRepository businessProfileAgentRepository,
        IBusinessProfileRepository businessProfileRepository,
        ILogger<ProductPurchaseService> logger)
    {
        _purchaseRepository = purchaseRepository;
        _usageLogRepository = usageLogRepository;
        _productRepository = productRepository;
        _paymentService = paymentService;
        _paymentRepository = paymentRepository;
        _businessProfileAgentRepository = businessProfileAgentRepository;
        _businessProfileRepository = businessProfileRepository;
        _logger = logger;
    }

    public async Task<(ProductPurchaseResponseDto purchase, PaymentDto payment)> InitiatePurchaseAsync(
        CreateProductPurchaseDto dto, Guid userId)
    {
        _logger.LogInformation("Initiating product purchase for user {UserId}, product {ProductId}",
            userId, dto.ProductId);

        // Parse product ID
        if (!Guid.TryParse(dto.ProductId, out var productId))
            throw new ValidationException("Invalid product ID");

        // Validate product exists and is active
        var product = _productRepository.GetProduct(productId);
        if (product == null || !product.IsActive)
            throw new ValidationException("Product not found or inactive");

        if (product.BusinessProfile == null)
            throw new ValidationException("Product business profile not found");

        // Calculate expiry date
        var purchaseDate = DateTime.UtcNow;
        var expiryDate = CalculateExpiryDate(purchaseDate, product.Period, product.NumOfPeriods);

        // Prepare product purchase details for webhook
        var productPurchaseDetails = new ProductPurchaseDetails
        {
            ProductId = productId,
            Quantity = dto.Quantity
        };

        // Resolve effective TPay merchant ID — child businesses may delegate to their parent
        var effectiveMerchantId = (product.BusinessProfile.UseParentTPay && product.BusinessProfile.ParentBusinessProfile != null)
            ? product.BusinessProfile.ParentBusinessProfile.TPayMerchantId
            : product.BusinessProfile.TPayMerchantId;

        // Create TPay payment through PaymentService (use gross price for consumer payments)
        var paymentDto = new CreatePaymentDto
        {
            UserId = userId,
            Amount = (product.GrossPrice ?? product.Price) * dto.Quantity,
            Description = $"Product Purchase: {product.Title}",
            Breakdown = $"Product: {product.Title} x{dto.Quantity}",
            FacilityId = null,
            MerchantId = effectiveMerchantId,
            CustomerEmail = dto.CustomerEmail,
            CustomerName = dto.CustomerName,
            CustomerPhone = dto.CustomerPhone,
            ReturnUrl = "spotto://payment-success",
            ErrorUrl = "spotto://payment-error",
            PushToken = dto.PushToken,
            FacilityReservation = null
        };

        var payment = await _paymentService.ProcessPaymentAsync(paymentDto);

        // Store product details in payment for webhook processing and KSeF invoicing
        var paymentEntity = await _paymentRepository.GetPaymentByIdAsync(payment.Id!.Value);
        if (paymentEntity != null)
        {
            paymentEntity.ProductDetails = JsonSerializer.Serialize(productPurchaseDetails);
            paymentEntity.ProductId = productId; // Store ProductId for KSeF invoicing
            await _paymentRepository.UpdateAsync(paymentEntity);
        }

        // Parse FacilityIds from product for response
        List<string>? facilityIds = null;
        if (!string.IsNullOrEmpty(product.FacilityIds))
        {
            facilityIds = JsonSerializer.Deserialize<List<string>>(product.FacilityIds);
        }

        // Return payment info (purchase created later by webhook)
        var purchaseResponse = new ProductPurchaseResponseDto
        {
            PurchaseId = Guid.Empty.ToString(),
            ProductId = product.Id.ToString(),
            UserId = userId.ToString(),
            PurchaseDate = purchaseDate.ToString("O"),
            ExpiryDate = expiryDate.ToString("O"),
            RemainingUsage = product.Usage * dto.Quantity,
            TotalUsage = product.Usage * dto.Quantity,
            Price = (product.GrossPrice ?? product.Price) * dto.Quantity,
            GrossPrice = product.GrossPrice.HasValue ? product.GrossPrice.Value * dto.Quantity : null,
            PaymentId = payment.Id!.Value.ToString(),
            Status = "pending_payment",
            ProductTitle = product.Title,
            ProductDescription = product.Description,
            BusinessName = product.BusinessProfile.DisplayName ?? product.BusinessProfile.CompanyName,
            BusinessProfileId = product.BusinessProfileId.ToString(),
            AppliesToAllFacilities = product.AppliesToAllFacilities,
            FacilityIds = facilityIds
        };

        _logger.LogInformation("Product purchase payment created: PaymentId={PaymentId}, ProductId={ProductId}",
            payment.Id, productId);

        return (purchaseResponse, payment);
    }

    public async Task<ProductPurchaseResponseDto> CreatePurchaseFromPaymentAsync(Guid paymentId)
    {
        _logger.LogInformation("Creating product purchase from payment {PaymentId}", paymentId);

        var payment = await _paymentRepository.GetPaymentByIdAsync(paymentId);
        if (payment == null)
            throw new NotFoundException("Payment", paymentId.ToString());

        if (string.IsNullOrEmpty(payment.ProductDetails))
            throw new ValidationException("No product details in payment");

        var productDetails = JsonSerializer.Deserialize<ProductPurchaseDetails>(payment.ProductDetails);
        if (productDetails == null)
            throw new ValidationException("Invalid product details in payment");

        var product = _productRepository.GetProduct(productDetails.ProductId);
        if (product == null)
            throw new ValidationException("Product not found");

        if (product.BusinessProfile == null)
            throw new ValidationException("Product business profile not found");

        var purchaseDate = DateTime.UtcNow;
        var expiryDate = CalculateExpiryDate(purchaseDate, product.Period, product.NumOfPeriods);

        var purchase = new ProductPurchase
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            UserId = payment.UserId,
            PaymentId = paymentId,
            PurchaseDate = purchaseDate,
            ExpiryDate = expiryDate,
            TotalUsage = product.Usage * productDetails.Quantity,
            RemainingUsage = product.Usage * productDetails.Quantity,
            Price = payment.Amount,
            GrossPrice = product.GrossPrice.HasValue ? product.GrossPrice.Value * productDetails.Quantity : null,
            VatRate = product.VatRate,
            ProductTitle = product.Title,
            ProductSubtitle = product.Subtitle,
            ProductDescription = product.Description,
            ProductPeriod = product.Period,
            ProductNumOfPeriods = product.NumOfPeriods,
            ProductPayableInApp = product.PayableInApp,
            ProductPayableWithTrainer = product.PayableWithTrainer,
            BusinessName = product.BusinessProfile.DisplayName ?? product.BusinessProfile.CompanyName,
            BusinessProfileId = product.BusinessProfileId,
            AppliesToAllFacilities = product.AppliesToAllFacilities,
            FacilityIds = product.FacilityIds,  // Snapshot facility restrictions
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _purchaseRepository.CreateAsync(purchase);

        // Mark payment as consumed
        payment.IsConsumed = true;
        await _paymentRepository.UpdateAsync(payment);

        _logger.LogInformation("Product purchase created: PurchaseId={PurchaseId}, ProductId={ProductId}",
            purchase.Id, product.Id);

        return MapToPurchaseResponse(purchase);
    }

    public async Task<ProductUsageResponseDto> UseProductAsync(
        Guid purchaseId, Guid userId, UseProductDto dto)
    {
        _logger.LogInformation("User {UserId} using product purchase {PurchaseId}", userId, purchaseId);

        var purchase = await _purchaseRepository.GetByIdAsync(purchaseId);

        if (purchase == null)
            throw new NotFoundException("Purchase", purchaseId.ToString());

        if (purchase.UserId != userId)
            throw new UnauthorizedAccessException("Not your purchase");

        if (purchase.Status != "active")
            throw new ValidationException($"Purchase is {purchase.Status}");

        if (purchase.RemainingUsage <= 0)
            throw new ValidationException("No remaining usage");

        if (purchase.ExpiryDate < DateTime.UtcNow)
        {
            purchase.Status = "expired";
            await _purchaseRepository.UpdateAsync(purchase);
            throw new ValidationException("Purchase has expired");
        }

        // Decrement usage
        purchase.RemainingUsage--;
        purchase.UpdatedAt = DateTime.UtcNow;

        if (purchase.RemainingUsage == 0)
            purchase.Status = "depleted";

        await _purchaseRepository.UpdateAsync(purchase);

        // Parse facility ID if provided
        Guid? facilityId = null;
        if (!string.IsNullOrEmpty(dto.FacilityId))
        {
            if (Guid.TryParse(dto.FacilityId, out var parsedFacilityId))
                facilityId = parsedFacilityId;
        }

        // Log usage
        var usageLog = new ProductUsageLog
        {
            Id = Guid.NewGuid(),
            ProductPurchaseId = purchaseId,
            UserId = userId,
            FacilityId = facilityId,
            UsageDate = DateTime.UtcNow,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow
        };

        await _usageLogRepository.CreateAsync(usageLog);

        _logger.LogInformation("Product usage logged: PurchaseId={PurchaseId}, RemainingUsage={RemainingUsage}",
            purchaseId, purchase.RemainingUsage);

        return new ProductUsageResponseDto
        {
            PurchaseId = purchaseId.ToString(),
            RemainingUsage = purchase.RemainingUsage,
            UsageDate = usageLog.UsageDate.ToString("O"),
            Status = purchase.Status
        };
    }

    public async Task<ProductPurchaseResponseDto> GetPurchaseAsync(Guid purchaseId, Guid userId)
    {
        var purchase = await _purchaseRepository.GetByIdAsync(purchaseId);

        if (purchase == null)
            throw new NotFoundException("Purchase", purchaseId.ToString());

        if (purchase.UserId != userId)
            throw new UnauthorizedAccessException("Not your purchase");

        return MapToPurchaseResponse(purchase);
    }

    public async Task<UserProductsResponseDto> GetUserPurchasesAsync(Guid userId)
    {
        var purchases = await _purchaseRepository.GetUserPurchasesAsync(userId);

        var response = new UserProductsResponseDto
        {
            Purchases = purchases.Select(p =>
            {
                // Parse FacilityIds from JSON
                List<string>? facilityIds = null;
                if (!string.IsNullOrEmpty(p.FacilityIds))
                {
                    facilityIds = JsonSerializer.Deserialize<List<string>>(p.FacilityIds);
                }

                return new ProductPurchaseDetailDto
                {
                    PurchaseId = p.Id.ToString(),
                    ProductId = p.ProductId.ToString(),
                    ProductTitle = p.ProductTitle,
                    ProductSubtitle = p.ProductSubtitle,
                    ProductDescription = p.ProductDescription,
                    BusinessName = p.BusinessName,
                    BusinessProfileId = p.BusinessProfileId.ToString(),
                    PurchaseDate = p.PurchaseDate.ToString("O"),
                    ExpiryDate = p.ExpiryDate.ToString("O"),
                    RemainingUsage = p.RemainingUsage,
                    TotalUsage = p.TotalUsage,
                    Price = p.Price,
                    GrossPrice = p.GrossPrice,
                    PayableInApp = p.ProductPayableInApp,
                    PayableWithTrainer = p.ProductPayableWithTrainer,
                    Status = p.Status,
                    AppliesToAllFacilities = p.AppliesToAllFacilities,
                    FacilityIds = facilityIds
                };
            }).ToArray()
        };

        return response;
    }

    private DateTime CalculateExpiryDate(DateTime purchaseDate, ProductPeriod period, int numOfPeriods)
    {
        return period switch
        {
            ProductPeriod.Days => purchaseDate.AddDays(numOfPeriods),
            ProductPeriod.Weeks => purchaseDate.AddDays(numOfPeriods * 7),
            ProductPeriod.Months => purchaseDate.AddMonths(numOfPeriods),
            ProductPeriod.Years => purchaseDate.AddYears(numOfPeriods),
            ProductPeriod.Lifetime => purchaseDate.AddYears(100),
            _ => throw new ArgumentException("Invalid period")
        };
    }

    private ProductPurchaseResponseDto MapToPurchaseResponse(ProductPurchase p)
    {
        // Parse FacilityIds from JSON
        List<string>? facilityIds = null;
        if (!string.IsNullOrEmpty(p.FacilityIds))
        {
            facilityIds = JsonSerializer.Deserialize<List<string>>(p.FacilityIds);
        }

        return new ProductPurchaseResponseDto
        {
            PurchaseId = p.Id.ToString(),
            ProductId = p.ProductId.ToString(),
            UserId = p.UserId.ToString(),
            PurchaseDate = p.PurchaseDate.ToString("O"),
            ExpiryDate = p.ExpiryDate.ToString("O"),
            RemainingUsage = p.RemainingUsage,
            TotalUsage = p.TotalUsage,
            Price = p.Price,
            GrossPrice = p.GrossPrice,
            PaymentId = p.PaymentId.ToString(),
            Status = p.Status,
            ProductTitle = p.ProductTitle,
            ProductDescription = p.ProductDescription,
            PayableInApp = p.ProductPayableInApp,
            PayableWithTrainer = p.ProductPayableWithTrainer,
            BusinessName = p.BusinessName,
            BusinessProfileId = p.BusinessProfileId.ToString(),
            AppliesToAllFacilities = p.AppliesToAllFacilities,
            FacilityIds = facilityIds
        };
    }

    // Agent methods
    public async Task<AgentProductPurchaseListDto> GetBusinessPurchasesAsync(
        Guid businessProfileId,
        Guid agentUserId,
        AgentProductPurchaseFilterDto filter)
    {
        _logger.LogInformation("User {AgentUserId} fetching purchases for business {BusinessProfileId}",
            agentUserId, businessProfileId);

        // Check if user is business owner or authorized agent
        var businessProfile = await _businessProfileRepository.GetBusinessProfileByIdAsync(businessProfileId);
        var isBusinessOwner = businessProfile?.UserId == agentUserId;
        var isAuthorizedAgent = await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(
            agentUserId, businessProfileId);

        if (!isBusinessOwner && !isAuthorizedAgent)
            throw new UnauthorizedAccessException("You are not authorized to view purchases for this business");

        var (purchases, totalCount) = await _purchaseRepository.GetBusinessPurchasesAsync(
            businessProfileId, filter);

        var pageSize = Math.Max(1, Math.Min(filter.PageSize, 100));
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new AgentProductPurchaseListDto
        {
            Purchases = purchases.Select(MapToAgentPurchaseDto).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<AgentProductPurchaseDto> ExtendPurchaseExpiryAsync(
        Guid purchaseId,
        Guid agentUserId,
        ExtendProductPurchaseDto dto)
    {
        _logger.LogInformation("User {AgentUserId} extending purchase {PurchaseId} to {NewExpiryDate}",
            agentUserId, purchaseId, dto.NewExpiryDate);

        var purchase = await _purchaseRepository.GetByIdAsync(purchaseId);
        if (purchase == null)
            throw new NotFoundException("Purchase", purchaseId.ToString());

        // Check if user is business owner or authorized agent
        var businessProfile = await _businessProfileRepository.GetBusinessProfileByIdAsync(purchase.BusinessProfileId);
        var isBusinessOwner = businessProfile?.UserId == agentUserId;
        var isAuthorizedAgent = await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(
            agentUserId, purchase.BusinessProfileId);

        if (!isBusinessOwner && !isAuthorizedAgent)
            throw new UnauthorizedAccessException("You are not authorized to modify purchases for this business");

        // Validate new expiry date
        var newExpiryDate = DateTime.SpecifyKind(dto.NewExpiryDate, DateTimeKind.Utc);
        if (newExpiryDate <= purchase.ExpiryDate)
            throw new ValidationException("New expiry date must be after the current expiry date");

        // Update expiry date
        purchase.ExpiryDate = newExpiryDate;
        purchase.UpdatedAt = DateTime.UtcNow;

        // If status was 'expired' and new date is in the future, reactivate
        if (purchase.Status == "expired" && newExpiryDate > DateTime.UtcNow)
        {
            purchase.Status = purchase.RemainingUsage > 0 ? "active" : "depleted";
        }

        await _purchaseRepository.UpdateAsync(purchase);

        _logger.LogInformation("Purchase {PurchaseId} extended to {NewExpiryDate}, status: {Status}",
            purchaseId, newExpiryDate, purchase.Status);

        return MapToAgentPurchaseDto(purchase);
    }

    public async Task<AgentProductPurchaseDto> MarkPurchaseAsUsedAsync(Guid purchaseId, Guid agentUserId)
    {
        _logger.LogInformation("Agent {AgentUserId} marking purchase {PurchaseId} as used", agentUserId, purchaseId);

        var purchase = await _purchaseRepository.GetByIdAsync(purchaseId);
        if (purchase == null)
            throw new NotFoundException("Purchase", purchaseId.ToString());

        var businessProfile = await _businessProfileRepository.GetBusinessProfileByIdAsync(purchase.BusinessProfileId);
        var isBusinessOwner = businessProfile?.UserId == agentUserId;
        var isAuthorizedAgent = await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(
            agentUserId, purchase.BusinessProfileId);

        if (!isBusinessOwner && !isAuthorizedAgent)
            throw new UnauthorizedAccessException("You are not authorized to manage purchases for this business");

        if (purchase.Status == "depleted")
            throw new ValidationException("Purchase is already fully used");

        if (purchase.Status == "expired")
            throw new ValidationException("Purchase has expired");

        purchase.RemainingUsage = 0;
        purchase.Status = "depleted";
        purchase.UpdatedAt = DateTime.UtcNow;

        await _purchaseRepository.UpdateAsync(purchase);

        _logger.LogInformation("Purchase {PurchaseId} marked as used by agent {AgentUserId}", purchaseId, agentUserId);

        return MapToAgentPurchaseDto(purchase);
    }

    private AgentProductPurchaseDto MapToAgentPurchaseDto(ProductPurchase p)
    {
        var customerName = p.User != null
            ? $"{p.User.FirstName} {p.User.LastName}".Trim()
            : "Unknown";

        return new AgentProductPurchaseDto
        {
            Id = p.Id,
            ProductId = p.ProductId,
            ProductTitle = p.ProductTitle,
            ProductSubtitle = p.ProductSubtitle,
            UserId = p.UserId,
            CustomerName = customerName,
            CustomerEmail = p.User?.Email,
            CustomerPhone = p.User?.Phone,
            PurchaseDate = p.PurchaseDate,
            ExpiryDate = p.ExpiryDate,
            TotalUsage = p.TotalUsage,
            RemainingUsage = p.RemainingUsage,
            Price = p.Price,
            GrossPrice = p.GrossPrice,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}
