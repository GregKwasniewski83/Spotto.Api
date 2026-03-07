using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.Configuration;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using System.Text.Json;

namespace PlaySpace.Services.Services;

public class KSeFInvoiceService : IKSeFInvoiceService
{
    private readonly ILogger<KSeFInvoiceService> _logger;
    private readonly IKSeFInvoiceRepository _invoiceRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IFacilityRepository _facilityRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IUserRepository _userRepository;
    private readonly IProductRepository _productRepository;
    private readonly IKSeFApiService _ksefApiService;
    private readonly IFAXmlGeneratorService _faXmlGenerator;
    private readonly IEmailService _emailService;
    private readonly IInvoicePdfGeneratorService _pdfGenerator;
    private readonly IBusinessParentChildAssociationRepository _parentChildAssociationRepository;
    private readonly KSeFOptions _ksefOptions;

    public KSeFInvoiceService(
        ILogger<KSeFInvoiceService> logger,
        IKSeFInvoiceRepository invoiceRepository,
        IPaymentRepository paymentRepository,
        IReservationRepository reservationRepository,
        IFacilityRepository facilityRepository,
        IBusinessProfileRepository businessProfileRepository,
        IUserRepository userRepository,
        IProductRepository productRepository,
        IKSeFApiService ksefApiService,
        IFAXmlGeneratorService faXmlGenerator,
        IEmailService emailService,
        IInvoicePdfGeneratorService pdfGenerator,
        IBusinessParentChildAssociationRepository parentChildAssociationRepository,
        IOptions<KSeFOptions> ksefOptions)
    {
        _logger = logger;
        _invoiceRepository = invoiceRepository;
        _paymentRepository = paymentRepository;
        _reservationRepository = reservationRepository;
        _facilityRepository = facilityRepository;
        _businessProfileRepository = businessProfileRepository;
        _userRepository = userRepository;
        _productRepository = productRepository;
        _ksefApiService = ksefApiService;
        _faXmlGenerator = faXmlGenerator;
        _emailService = emailService;
        _pdfGenerator = pdfGenerator;
        _parentChildAssociationRepository = parentChildAssociationRepository;
        _ksefOptions = ksefOptions.Value;
    }

    public async Task<KSeFInvoice> CreateInvoiceFromPaymentAsync(Guid paymentId)
    {
        _logger.LogInformation("[KSeF] Starting invoice creation for PaymentId: {PaymentId}", paymentId);

        // Check if invoice already exists for this payment
        var existingInvoice = _invoiceRepository.GetInvoiceByPaymentId(paymentId);
        if (existingInvoice != null)
        {
            _logger.LogInformation("[KSeF] Invoice already exists for PaymentId: {PaymentId}, InvoiceId: {InvoiceId}, Status: {Status}",
                paymentId, existingInvoice.Id, existingInvoice.Status);
            return existingInvoice;
        }

        // Get payment data
        var payment = await _paymentRepository.GetPaymentByIdAsync(paymentId);
        if (payment == null)
        {
            _logger.LogError("[KSeF] Payment not found: {PaymentId}", paymentId);
            throw new ArgumentException($"Payment not found: {paymentId}");
        }

        _logger.LogDebug("[KSeF] Payment found: Id={PaymentId}, Amount={Amount}, UserId={UserId}, ProductDetails={HasProductDetails}",
            paymentId, payment.Amount, payment.UserId, !string.IsNullOrEmpty(payment.ProductDetails));

        // Determine if this is a reservation payment or product purchase
        var reservation = _reservationRepository.GetReservationByPaymentId(paymentId);
        var isProductPurchase = !string.IsNullOrEmpty(payment.ProductDetails);
        var hasReservationDetails = !string.IsNullOrEmpty(payment.ReservationDetails);

        _logger.LogDebug("[KSeF] Payment type detection: HasReservation={HasReservation}, HasReservationDetails={HasReservationDetails}, IsProductPurchase={IsProductPurchase}",
            reservation != null, hasReservationDetails, isProductPurchase);

        BusinessProfile? businessProfile = null;
        Guid? reservationId = null;
        List<KSeFInvoiceItem> invoiceItems;

        if (reservation != null)
        {
            // FACILITY RESERVATION PAYMENT (reservation already created)
            _logger.LogInformation("[KSeF] Processing RESERVATION payment. ReservationId: {ReservationId}, FacilityId: {FacilityId}",
                reservation.Id, reservation.FacilityId);
            reservationId = reservation.Id;

            // Get all reservations in the group (or just the single one)
            var allReservations = reservation.GroupId.HasValue
                ? _reservationRepository.GetGroupReservations(reservation.GroupId.Value)
                : new List<Reservation> { reservation };

            _logger.LogInformation("[KSeF] Found {Count} reservation(s) for invoice", allReservations.Count);

            // Use the first facility to determine business profile
            var firstFacility = _facilityRepository.GetFacility(allReservations.First().FacilityId);
            if (firstFacility == null || !firstFacility.BusinessProfileId.HasValue)
            {
                _logger.LogError("[KSeF] Facility not found or has no BusinessProfileId: {FacilityId}", allReservations.First().FacilityId);
                throw new ArgumentException($"Facility not found or has no BusinessProfileId: {allReservations.First().FacilityId}");
            }

            businessProfile = _businessProfileRepository.GetBusinessProfileById(firstFacility.BusinessProfileId.Value);
            if (businessProfile == null)
            {
                _logger.LogError("[KSeF] Business profile not found: {BusinessProfileId}", firstFacility.BusinessProfileId.Value);
                throw new ArgumentException($"Business profile not found: {firstFacility.BusinessProfileId.Value}");
            }

            _logger.LogDebug("[KSeF] Business profile found: {CompanyName}, NIP: {NIP}, KSeFEnabled: {KSeFEnabled}, UseParentNipForInvoices: {UseParentNip}",
                businessProfile.CompanyName, businessProfile.Nip ?? "NULL", businessProfile.KSeFEnabled, businessProfile.UseParentNipForInvoices);

            // Validate business has NIP (check parent's NIP if UseParentNipForInvoices is true)
            ValidateEffectiveNip(businessProfile);

            // Build invoice items - one per facility/reservation
            invoiceItems = new List<KSeFInvoiceItem>();
            foreach (var res in allReservations)
            {
                var facility = _facilityRepository.GetFacility(res.FacilityId);
                if (facility == null) continue;

                var resGross = res.TotalPrice;
                var resNet = Math.Round(resGross / (1 + (_ksefOptions.DefaultVATRate / 100m)), 2);
                var resVat = resGross - resNet;

                invoiceItems.AddRange(BuildReservationInvoiceItems(res, facility, resNet, resVat, resGross));
            }

            _logger.LogDebug("[KSeF] Invoice items built: {Count} item(s), Total gross={Gross}",
                invoiceItems.Count, payment.Amount);
        }
        else if (hasReservationDetails)
        {
            // FACILITY RESERVATION PAYMENT (reservation not yet created - using ReservationDetails from payment)
            _logger.LogInformation("[KSeF] Processing PENDING RESERVATION payment using ReservationDetails. PaymentId: {PaymentId}",
                paymentId);

            // Deserialize as list (new format) with fallback to single object (backward compat)
            var reservationDetailsList = new List<FacilityReservationDto>();
            try
            {
                var list = JsonSerializer.Deserialize<List<FacilityReservationDto>>(payment.ReservationDetails!);
                if (list != null && list.Count > 0)
                    reservationDetailsList = list;
            }
            catch (JsonException)
            {
                // Backward compatibility: try single object
                try
                {
                    var single = JsonSerializer.Deserialize<FacilityReservationDto>(payment.ReservationDetails!);
                    if (single != null)
                        reservationDetailsList.Add(single);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "[KSeF] Failed to deserialize ReservationDetails: {ReservationDetails}", payment.ReservationDetails);
                    throw new ArgumentException($"Invalid reservation details JSON in payment: {ex.Message}");
                }
            }

            if (reservationDetailsList.Count == 0)
            {
                _logger.LogError("[KSeF] ReservationDetails deserialized to empty: {ReservationDetails}", payment.ReservationDetails);
                throw new ArgumentException("Invalid reservation details in payment - deserialized to empty");
            }

            _logger.LogInformation("[KSeF] ReservationDetails parsed: {Count} facility reservation(s)", reservationDetailsList.Count);

            // Use the first facility to determine business profile
            var firstDetails = reservationDetailsList.First();
            var firstFacility = _facilityRepository.GetFacility(firstDetails.FacilityId);
            if (firstFacility == null || !firstFacility.BusinessProfileId.HasValue)
            {
                _logger.LogError("[KSeF] Facility not found or has no BusinessProfileId: {FacilityId}", firstDetails.FacilityId);
                throw new ArgumentException($"Facility not found or has no BusinessProfileId: {firstDetails.FacilityId}");
            }

            businessProfile = _businessProfileRepository.GetBusinessProfileById(firstFacility.BusinessProfileId.Value);
            if (businessProfile == null)
            {
                _logger.LogError("[KSeF] Business profile not found: {BusinessProfileId}", firstFacility.BusinessProfileId.Value);
                throw new ArgumentException($"Business profile not found: {firstFacility.BusinessProfileId.Value}");
            }

            _logger.LogDebug("[KSeF] Business profile found: {CompanyName}, NIP: {NIP}, KSeFEnabled: {KSeFEnabled}, UseParentNipForInvoices: {UseParentNip}",
                businessProfile.CompanyName, businessProfile.Nip ?? "NULL", businessProfile.KSeFEnabled, businessProfile.UseParentNipForInvoices);

            // Validate business has NIP (check parent's NIP if UseParentNipForInvoices is true)
            ValidateEffectiveNip(businessProfile);

            // Build invoice items - one per facility
            invoiceItems = new List<KSeFInvoiceItem>();
            foreach (var rd in reservationDetailsList)
            {
                var facility = _facilityRepository.GetFacility(rd.FacilityId);
                if (facility == null) continue;

                var slotCount = rd.TimeSlots.Count > 0 ? rd.TimeSlots.Count : 1;
                var numberOfUsers = rd.NumberOfUsers > 0 ? rd.NumberOfUsers : 1;
                var pricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;
                var facilityGross = pricePerSlot * slotCount * (facility.PricePerUser && rd.PayForAllUsers ? numberOfUsers : 1);
                var facilityNet = Math.Round(facilityGross / (1 + (_ksefOptions.DefaultVATRate / 100m)), 2);
                var facilityVat = facilityGross - facilityNet;

                invoiceItems.AddRange(BuildPendingReservationInvoiceItems(rd, facility, facilityNet, facilityVat, facilityGross));
            }

            _logger.LogDebug("[KSeF] Invoice items built: {Count} item(s), Total gross={Gross}",
                invoiceItems.Count, payment.Amount);
        }
        else if (isProductPurchase)
        {
            // PRODUCT PURCHASE PAYMENT
            _logger.LogInformation("[KSeF] Processing PRODUCT PURCHASE payment. ProductDetails: {ProductDetails}",
                payment.ProductDetails);

            ProductPurchaseDetails? productDetails;
            try
            {
                productDetails = System.Text.Json.JsonSerializer.Deserialize<ProductPurchaseDetails>(payment.ProductDetails!);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[KSeF] Failed to deserialize ProductDetails: {ProductDetails}", payment.ProductDetails);
                throw new ArgumentException($"Invalid product details JSON in payment: {ex.Message}");
            }

            if (productDetails == null)
            {
                _logger.LogError("[KSeF] ProductDetails deserialized to null: {ProductDetails}", payment.ProductDetails);
                throw new ArgumentException("Invalid product details in payment - deserialized to null");
            }

            _logger.LogDebug("[KSeF] ProductDetails parsed: ProductId={ProductId}, Quantity={Quantity}",
                productDetails.ProductId, productDetails.Quantity);

            var product = _productRepository.GetProduct(productDetails.ProductId);
            if (product == null)
            {
                _logger.LogError("[KSeF] Product not found: {ProductId}", productDetails.ProductId);
                throw new ArgumentException($"Product not found: {productDetails.ProductId}");
            }

            _logger.LogDebug("[KSeF] Product found: {ProductTitle}, BusinessProfileId: {BusinessProfileId}",
                product.Title, product.BusinessProfileId);

            businessProfile = _businessProfileRepository.GetBusinessProfileById(product.BusinessProfileId);
            if (businessProfile == null)
            {
                _logger.LogError("[KSeF] Business profile not found for product: {BusinessProfileId}", product.BusinessProfileId);
                throw new ArgumentException($"Business profile not found for product: {product.BusinessProfileId}");
            }

            _logger.LogDebug("[KSeF] Business profile found: {CompanyName}, NIP: {NIP}, KSeFEnabled: {KSeFEnabled}, UseParentNipForInvoices: {UseParentNip}",
                businessProfile.CompanyName, businessProfile.Nip ?? "NULL", businessProfile.KSeFEnabled, businessProfile.UseParentNipForInvoices);

            // Validate business has NIP (check parent's NIP if UseParentNipForInvoices is true)
            ValidateEffectiveNip(businessProfile);

            // Calculate amounts and build items for product
            var grossAmount2 = payment.Amount;
            var netAmount2 = Math.Round(grossAmount2 / (1 + (_ksefOptions.DefaultVATRate / 100m)), 2);
            var vatAmount2 = grossAmount2 - netAmount2;
            invoiceItems = BuildProductInvoiceItems(product, productDetails.Quantity, netAmount2, vatAmount2, grossAmount2);

            _logger.LogDebug("[KSeF] Invoice amounts calculated: Gross={Gross}, Net={Net}, VAT={VAT}",
                grossAmount2, netAmount2, vatAmount2);
        }
        else if (payment.FacilityId.HasValue)
        {
            // FALLBACK: Use FacilityId stored on payment (facility reservation or training)
            _logger.LogInformation("[KSeF] Using FacilityId fallback for payment {PaymentId}. FacilityId: {FacilityId}, TrainingId: {TrainingId}",
                paymentId, payment.FacilityId, payment.TrainingId);

            var facility = _facilityRepository.GetFacility(payment.FacilityId.Value);
            if (facility == null)
            {
                _logger.LogError("[KSeF] Facility not found: {FacilityId}", payment.FacilityId.Value);
                throw new ArgumentException($"Facility not found: {payment.FacilityId.Value}");
            }
            if (!facility.BusinessProfileId.HasValue)
            {
                _logger.LogError("[KSeF] Facility {FacilityId} has no BusinessProfileId", payment.FacilityId.Value);
                throw new ArgumentException($"Facility {payment.FacilityId.Value} has no BusinessProfileId");
            }

            businessProfile = _businessProfileRepository.GetBusinessProfileById(facility.BusinessProfileId.Value);
            if (businessProfile == null)
            {
                _logger.LogError("[KSeF] Business profile not found: {BusinessProfileId}", facility.BusinessProfileId.Value);
                throw new ArgumentException($"Business profile not found: {facility.BusinessProfileId.Value}");
            }

            _logger.LogDebug("[KSeF] Business profile found via FacilityId fallback: {CompanyName}, NIP: {NIP}, UseParentNipForInvoices: {UseParentNip}",
                businessProfile.CompanyName, businessProfile.Nip ?? "NULL", businessProfile.UseParentNipForInvoices);

            // Validate business has NIP (check parent's NIP if UseParentNipForInvoices is true)
            ValidateEffectiveNip(businessProfile);

            // Build invoice items based on payment type
            var grossAmount3 = payment.Amount;
            var netAmount3 = Math.Round(grossAmount3 / (1 + (_ksefOptions.DefaultVATRate / 100m)), 2);
            var vatAmount3 = grossAmount3 - netAmount3;

            if (payment.TrainingId.HasValue)
            {
                // Training payment
                invoiceItems = BuildTrainingInvoiceItems(payment, facility, netAmount3, vatAmount3, grossAmount3);
            }
            else
            {
                // Facility reservation payment (without ReservationDetails)
                invoiceItems = BuildFacilityInvoiceItems(payment, facility, netAmount3, vatAmount3, grossAmount3);
            }

            _logger.LogDebug("[KSeF] Invoice amounts calculated via fallback: Gross={Gross}, Net={Net}, VAT={VAT}",
                grossAmount3, netAmount3, vatAmount3);
        }
        else if (payment.ProductId.HasValue)
        {
            // FALLBACK: Use ProductId stored on payment
            _logger.LogInformation("[KSeF] Using ProductId fallback for payment {PaymentId}. ProductId: {ProductId}",
                paymentId, payment.ProductId);

            var product = _productRepository.GetProduct(payment.ProductId.Value);
            if (product == null)
            {
                _logger.LogError("[KSeF] Product not found: {ProductId}", payment.ProductId.Value);
                throw new ArgumentException($"Product not found: {payment.ProductId.Value}");
            }

            businessProfile = _businessProfileRepository.GetBusinessProfileById(product.BusinessProfileId);
            if (businessProfile == null)
            {
                _logger.LogError("[KSeF] Business profile not found for product: {BusinessProfileId}", product.BusinessProfileId);
                throw new ArgumentException($"Business profile not found: {product.BusinessProfileId}");
            }

            _logger.LogDebug("[KSeF] Business profile found via ProductId fallback: {CompanyName}, NIP: {NIP}, UseParentNipForInvoices: {UseParentNip}",
                businessProfile.CompanyName, businessProfile.Nip ?? "NULL", businessProfile.UseParentNipForInvoices);

            // Validate business has NIP (check parent's NIP if UseParentNipForInvoices is true)
            ValidateEffectiveNip(businessProfile);

            var grossAmount4 = payment.Amount;
            var netAmount4 = Math.Round(grossAmount4 / (1 + (_ksefOptions.DefaultVATRate / 100m)), 2);
            var vatAmount4 = grossAmount4 - netAmount4;
            invoiceItems = BuildProductInvoiceItems(product, 1, netAmount4, vatAmount4, grossAmount4);

            _logger.LogDebug("[KSeF] Invoice amounts calculated via ProductId fallback: Gross={Gross}, Net={Net}, VAT={VAT}",
                grossAmount4, netAmount4, vatAmount4);
        }
        else
        {
            // No context available - cannot create invoice
            _logger.LogWarning("[KSeF] Payment {PaymentId} has no context for KSeF invoicing. " +
                "No reservation, ReservationDetails, ProductDetails, FacilityId, TrainingId, or ProductId found. " +
                "Description: {Description}. Amount: {Amount}. Skipping invoice creation.",
                paymentId, payment.Description, payment.Amount);

            throw new InvalidOperationException($"Payment {paymentId} has no context for KSeF invoicing. " +
                $"Description: {payment.Description}");
        }

        // Get buyer data (user or guest)
        string buyerName;
        string? buyerEmail = null;
        string? buyerPhone = null;
        string? buyerNIP = null;
        User? user = null;

        if (reservation != null)
        {
            // Buyer data from existing reservation
            if (reservation.UserId.HasValue)
            {
                user = await _userRepository.GetUserByIdAsync(reservation.UserId.Value);
                buyerName = $"{user?.FirstName} {user?.LastName}";
                buyerEmail = user?.Email;
                buyerPhone = user?.Phone;
            }
            else
            {
                // Guest reservation
                buyerName = reservation.GuestName ?? "Guest Customer";
                buyerEmail = reservation.GuestEmail;
                buyerPhone = reservation.GuestPhone;
            }
            _logger.LogDebug("[KSeF] Buyer data from reservation: {BuyerName}, {BuyerEmail}", buyerName, buyerEmail ?? "N/A");
        }
        else
        {
            // Buyer data from payment (for pending reservation or product purchase)
            user = await _userRepository.GetUserByIdAsync(payment.UserId);
            if (user != null)
            {
                buyerName = $"{user.FirstName} {user.LastName}";
                buyerEmail = user.Email;
                buyerPhone = user.Phone;
                _logger.LogDebug("[KSeF] Buyer data from user: {BuyerName}, {BuyerEmail}", buyerName, buyerEmail ?? "N/A");
            }
            else
            {
                buyerName = "Customer";
                buyerEmail = null;
                buyerPhone = null;
                _logger.LogDebug("[KSeF] Buyer user not found for UserId {UserId}, using default 'Customer'", payment.UserId);
            }
        }

        _logger.LogDebug("[KSeF] Buyer data resolved: Name={BuyerName}, Email={BuyerEmail}, UserId={UserId}",
            buyerName, buyerEmail ?? "NULL", user?.Id.ToString() ?? "NULL");

        // Generate invoice number (per business profile)
        var invoiceNumber = _invoiceRepository.GenerateInvoiceNumber(_ksefOptions.InvoicePrefix, businessProfile.Id);
        _logger.LogDebug("[KSeF] Generated invoice number: {InvoiceNumber} for business {BusinessProfileId}", invoiceNumber, businessProfile.Id);

        // Calculate total amounts (already calculated per payment type above, but get from payment for consistency)
        var grossAmount = payment.Amount;
        var netAmount = Math.Round(grossAmount / (1 + (_ksefOptions.DefaultVATRate / 100m)), 2);
        var vatAmount = grossAmount - netAmount;

        // Get effective seller info (from parent if UseParentNipForInvoices is true)
        var (sellerNip, sellerName, sellerAddress, sellerCity, sellerPostalCode) = GetEffectiveSellerInfo(businessProfile);

        // Create invoice entity
        var invoice = new KSeFInvoice
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            ReservationId = reservationId,
            BusinessProfileId = businessProfile.Id,
            UserId = user?.Id,
            InvoiceNumber = invoiceNumber,
            IssueDate = DateTime.UtcNow,

            // Seller (Business - may be from parent if UseParentNipForInvoices)
            SellerNIP = sellerNip,
            SellerName = sellerName,
            SellerAddress = sellerAddress,
            SellerCity = sellerCity,
            SellerPostalCode = sellerPostalCode,

            // Buyer (Customer)
            BuyerNIP = buyerNIP,
            BuyerName = buyerName,
            BuyerEmail = buyerEmail,
            BuyerPhone = buyerPhone,

            // Amounts
            NetAmount = netAmount,
            VATAmount = vatAmount,
            GrossAmount = grossAmount,
            VATRate = _ksefOptions.DefaultVATRate,

            // Invoice items
            InvoiceItems = JsonSerializer.Serialize(invoiceItems),

            // Status
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("[KSeF] Creating invoice record: InvoiceId={InvoiceId}, InvoiceNumber={InvoiceNumber}, " +
            "Seller={SellerName} (NIP: {SellerNIP}), Buyer={BuyerName}, Amount={GrossAmount}",
            invoice.Id, invoiceNumber, businessProfile.CompanyName, businessProfile.Nip, buyerName, grossAmount);

        // Save invoice to database
        var createdInvoice = _invoiceRepository.CreateInvoice(invoice);
        _logger.LogInformation("[KSeF] Invoice record saved to database: {InvoiceId}", createdInvoice.Id);

        // Send to KSeF (if auto-invoicing is enabled)
        if (_ksefOptions.EnableAutoInvoicing)
        {
            _logger.LogInformation("[KSeF] Auto-invoicing enabled, sending invoice {InvoiceId} to KSeF API", createdInvoice.Id);
            try
            {
                await SendInvoiceToKSeFAsync(createdInvoice);
                _logger.LogInformation("[KSeF] Invoice {InvoiceId} sent to KSeF. Status: {Status}, KSeFRef: {KSeFRef}",
                    createdInvoice.Id, createdInvoice.Status, createdInvoice.KSeFReferenceNumber ?? "N/A");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[KSeF] Exception while sending invoice {InvoiceId} to KSeF: {ErrorMessage}",
                    createdInvoice.Id, ex.Message);
                // Update invoice status to Error
                createdInvoice.Status = "Error";
                createdInvoice.KSeFErrorMessage = ex.Message;
                _invoiceRepository.UpdateInvoice(createdInvoice);
            }
        }
        else
        {
            _logger.LogInformation("[KSeF] Auto-invoicing disabled. Invoice {InvoiceId} saved with status: {Status}",
                createdInvoice.Id, createdInvoice.Status);
        }

        return createdInvoice;
    }

    public KSeFInvoice? GetInvoice(Guid invoiceId)
    {
        return _invoiceRepository.GetInvoiceById(invoiceId);
    }

    public KSeFInvoice? GetInvoiceByPayment(Guid paymentId)
    {
        return _invoiceRepository.GetInvoiceByPaymentId(paymentId);
    }

    public List<KSeFInvoice> GetInvoicesByBusinessProfile(Guid businessProfileId)
    {
        return _invoiceRepository.GetInvoicesByBusinessProfile(businessProfileId);
    }

    public async Task<KSeFInvoice> CheckInvoiceStatusAsync(Guid invoiceId)
    {
        var invoice = _invoiceRepository.GetInvoiceById(invoiceId);
        if (invoice == null)
            throw new ArgumentException("Invoice not found");

        // Can't check status if invoice was never sent
        if (string.IsNullOrEmpty(invoice.KSeFReferenceNumber))
        {
            return invoice;
        }

        // Get business profile for credentials
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(invoice.BusinessProfileId);
        if (businessProfile == null || !businessProfile.KSeFEnabled || string.IsNullOrEmpty(businessProfile.KSeFToken))
        {
            return invoice;
        }

        try
        {
            // Initialize session
            var sessionResult = await _ksefApiService.InitializeSessionAsync(
                businessProfile.Nip,
                businessProfile.KSeFToken,
                businessProfile.KSeFEnvironment);

            if (!sessionResult.Success)
            {
                return invoice;
            }

            // Check status
            var statusResult = await _ksefApiService.CheckInvoiceStatusAsync(
                sessionResult.SessionToken!,
                invoice.KSeFReferenceNumber,
                businessProfile.KSeFEnvironment);

            if (statusResult.Success)
            {
                invoice.KSeFStatus = statusResult.Status;

                if (statusResult.Status == "Accepted" && invoice.Status != "Accepted")
                {
                    invoice.Status = "Accepted";
                    invoice.KSeFAcceptedAt = statusResult.ProcessedAt ?? DateTime.UtcNow;
                }
                else if (statusResult.Status == "Rejected")
                {
                    invoice.Status = "Rejected";
                    invoice.KSeFErrorMessage = statusResult.ErrorMessage;
                }

                _invoiceRepository.UpdateInvoice(invoice);
            }

            // Close session
            await _ksefApiService.CloseSessionAsync(sessionResult.SessionToken!, businessProfile.KSeFEnvironment);
        }
        catch (Exception ex)
        {
            // Log error but don't fail - return current invoice state
            _logger.LogError(ex, "[KSeF] Error checking invoice status for InvoiceId {InvoiceId}: {ErrorMessage}",
                invoiceId, ex.Message);
        }

        return invoice;
    }

    // Private helper methods

    private List<KSeFInvoiceItem> BuildReservationInvoiceItems(
        Reservation reservation,
        Facility facility,
        decimal netAmount,
        decimal vatAmount,
        decimal grossAmount)
    {
        var items = new List<KSeFInvoiceItem>();

        // Main item - facility reservation
        var item = new KSeFInvoiceItem
        {
            Name = $"Rezerwacja: {facility.Name}",
            Description = $"Data: {reservation.Date:yyyy-MM-dd}, Godziny: {string.Join(", ", reservation.TimeSlots)}",
            Quantity = reservation.TimeSlots.Count,
            Unit = "godz",
            UnitPrice = Math.Round(netAmount / reservation.TimeSlots.Count, 2),
            NetAmount = netAmount,
            VATRate = _ksefOptions.DefaultVATRate,
            VATAmount = vatAmount,
            GrossAmount = grossAmount
        };

        items.Add(item);

        // TODO: Add trainer services if applicable

        return items;
    }

    private List<KSeFInvoiceItem> BuildProductInvoiceItems(
        Product product,
        int quantity,
        decimal netAmount,
        decimal vatAmount,
        decimal grossAmount)
    {
        var items = new List<KSeFInvoiceItem>();

        // Product purchase item
        var item = new KSeFInvoiceItem
        {
            Name = product.Title,
            Description = $"{product.Description} - Ważność: {product.NumOfPeriods} {product.Period}, Ilość użyć: {product.Usage}",
            Quantity = quantity,
            Unit = "szt",
            UnitPrice = Math.Round(netAmount / quantity, 2),
            NetAmount = netAmount,
            VATRate = _ksefOptions.DefaultVATRate,
            VATAmount = vatAmount,
            GrossAmount = grossAmount
        };

        items.Add(item);

        return items;
    }

    private List<KSeFInvoiceItem> BuildPendingReservationInvoiceItems(
        FacilityReservationDto reservationDetails,
        Facility facility,
        decimal netAmount,
        decimal vatAmount,
        decimal grossAmount)
    {
        var items = new List<KSeFInvoiceItem>();
        var timeSlotCount = reservationDetails.TimeSlots.Count > 0 ? reservationDetails.TimeSlots.Count : 1;

        // Main item - facility reservation (from pending reservation details)
        var item = new KSeFInvoiceItem
        {
            Name = $"Rezerwacja: {facility.Name}",
            Description = $"Data: {reservationDetails.Date:yyyy-MM-dd}, Godziny: {string.Join(", ", reservationDetails.TimeSlots)}",
            Quantity = timeSlotCount,
            Unit = "godz",
            UnitPrice = Math.Round(netAmount / timeSlotCount, 2),
            NetAmount = netAmount,
            VATRate = _ksefOptions.DefaultVATRate,
            VATAmount = vatAmount,
            GrossAmount = grossAmount
        };

        items.Add(item);

        return items;
    }

    private List<KSeFInvoiceItem> BuildFacilityInvoiceItems(
        Payment payment,
        Facility facility,
        decimal netAmount,
        decimal vatAmount,
        decimal grossAmount)
    {
        var items = new List<KSeFInvoiceItem>();

        // Facility reservation item (fallback when no detailed reservation info available)
        var item = new KSeFInvoiceItem
        {
            Name = $"Rezerwacja: {facility.Name}",
            Description = payment.Description ?? $"Rezerwacja obiektu {facility.Name}",
            Quantity = 1,
            Unit = "usł",
            UnitPrice = netAmount,
            NetAmount = netAmount,
            VATRate = _ksefOptions.DefaultVATRate,
            VATAmount = vatAmount,
            GrossAmount = grossAmount
        };

        items.Add(item);

        return items;
    }

    private List<KSeFInvoiceItem> BuildTrainingInvoiceItems(
        Payment payment,
        Facility facility,
        decimal netAmount,
        decimal vatAmount,
        decimal grossAmount)
    {
        var items = new List<KSeFInvoiceItem>();

        // Training item
        var item = new KSeFInvoiceItem
        {
            Name = payment.Description ?? $"Trening w {facility.Name}",
            Description = payment.Breakdown ?? $"Usługa treningowa w obiekcie {facility.Name}",
            Quantity = 1,
            Unit = "usł",
            UnitPrice = netAmount,
            NetAmount = netAmount,
            VATRate = _ksefOptions.DefaultVATRate,
            VATAmount = vatAmount,
            GrossAmount = grossAmount
        };

        items.Add(item);

        return items;
    }

    private async Task SendInvoiceToKSeFAsync(KSeFInvoice invoice)
    {
        _logger.LogInformation("[KSeF] SendInvoiceToKSeFAsync started for InvoiceId: {InvoiceId}, BusinessProfileId: {BusinessProfileId}",
            invoice.Id, invoice.BusinessProfileId);

        // Get the business profile (child or standalone)
        var businessProfile = _businessProfileRepository.GetBusinessProfileById(invoice.BusinessProfileId);
        if (businessProfile == null)
        {
            _logger.LogError("[KSeF] Business profile not found: {BusinessProfileId}", invoice.BusinessProfileId);
            invoice.Status = "Error";
            invoice.KSeFErrorMessage = "Business profile not found";
            _invoiceRepository.UpdateInvoice(invoice);
            return;
        }

        // Determine which business profile's KSeF credentials to use
        // If UseParentNipForInvoices is true and parent exists, use parent's KSeF credentials
        BusinessProfile ksefCredentialsProfile = businessProfile;
        if (businessProfile.UseParentNipForInvoices && businessProfile.ParentBusinessProfile != null)
        {
            ksefCredentialsProfile = businessProfile.ParentBusinessProfile;
            _logger.LogInformation("[KSeF] Child business uses parent's NIP for invoices. Using parent's KSeF credentials. " +
                "Parent: {ParentCompanyName} (ID: {ParentId})",
                ksefCredentialsProfile.CompanyName, ksefCredentialsProfile.Id);
        }

        _logger.LogDebug("[KSeF] KSeF credentials profile: {CompanyName}, KSeFEnabled={KSeFEnabled}, HasToken={HasToken}, Environment={Environment}",
            ksefCredentialsProfile.CompanyName, ksefCredentialsProfile.KSeFEnabled,
            !string.IsNullOrEmpty(ksefCredentialsProfile.KSeFToken), ksefCredentialsProfile.KSeFEnvironment ?? "Not set");

        // Check if the credentials profile has KSeF enabled
        if (!ksefCredentialsProfile.KSeFEnabled)
        {
            var isParent = ksefCredentialsProfile.Id != businessProfile.Id;
            var profileType = isParent ? "Parent business" : "Business";
            _logger.LogWarning("[KSeF] {ProfileType} {CompanyName} (ID: {BusinessProfileId}) has KSeF disabled. Invoice marked as PendingBusinessKSeFSetup.",
                profileType, ksefCredentialsProfile.CompanyName, ksefCredentialsProfile.Id);
            invoice.Status = "PendingBusinessKSeFSetup";
            invoice.KSeFErrorMessage = isParent
                ? "Parent business has not enabled KSeF integration. Please configure KSeF credentials in parent business settings."
                : "Business has not enabled KSeF integration. Please configure KSeF credentials in business settings.";
            _invoiceRepository.UpdateInvoice(invoice);
            return;
        }

        // Check if the credentials profile has configured KSeF token
        if (string.IsNullOrEmpty(ksefCredentialsProfile.KSeFToken))
        {
            var isParent = ksefCredentialsProfile.Id != businessProfile.Id;
            var profileType = isParent ? "Parent business" : "Business";
            _logger.LogWarning("[KSeF] {ProfileType} {CompanyName} (ID: {BusinessProfileId}) has no KSeF token. Invoice marked as PendingBusinessKSeFSetup.",
                profileType, ksefCredentialsProfile.CompanyName, ksefCredentialsProfile.Id);
            invoice.Status = "PendingBusinessKSeFSetup";
            invoice.KSeFErrorMessage = isParent
                ? "Parent business has not configured KSeF token. Please add KSeF token in parent business settings."
                : "Business has not configured KSeF token. Please add KSeF token in business settings.";
            _invoiceRepository.UpdateInvoice(invoice);
            return;
        }

        try
        {
            // Step 1: Generate FA XML
            _logger.LogDebug("[KSeF] Step 1: Generating FA XML for invoice {InvoiceId}", invoice.Id);
            var faXml = _faXmlGenerator.GenerateFAXml(invoice);
            invoice.InvoiceXML = faXml;
            _logger.LogDebug("[KSeF] FA XML generated. Length: {XmlLength} characters", faXml.Length);

            // Step 2: Validate FA XML
            _logger.LogDebug("[KSeF] Step 2: Validating FA XML");
            var validation = _faXmlGenerator.ValidateFAXml(faXml);
            if (!validation.IsValid)
            {
                _logger.LogError("[KSeF] FA XML validation failed for invoice {InvoiceId}. Errors: {Errors}",
                    invoice.Id, string.Join("; ", validation.Errors));
                invoice.Status = "Error";
                invoice.KSeFErrorMessage = $"FA XML validation failed: {string.Join(", ", validation.Errors)}";
                _invoiceRepository.UpdateInvoice(invoice);
                return;
            }
            _logger.LogDebug("[KSeF] FA XML validation passed");

            // Step 3: Initialize KSeF session with BUSINESS credentials (from parent if UseParentNipForInvoices)
            _logger.LogInformation("[KSeF] Step 3: Initializing KSeF session for NIP: {NIP}, Environment: {Environment}",
                ksefCredentialsProfile.Nip, ksefCredentialsProfile.KSeFEnvironment ?? "Test");
            var sessionResult = await _ksefApiService.InitializeSessionAsync(
                ksefCredentialsProfile.Nip,
                ksefCredentialsProfile.KSeFToken,
                ksefCredentialsProfile.KSeFEnvironment);

            if (!sessionResult.Success)
            {
                _logger.LogError("[KSeF] Failed to initialize KSeF session for invoice {InvoiceId}. Error: {Error}",
                    invoice.Id, sessionResult.ErrorMessage);
                invoice.Status = "Error";
                invoice.KSeFErrorMessage = $"Failed to initialize KSeF session: {sessionResult.ErrorMessage}";
                _invoiceRepository.UpdateInvoice(invoice);
                return;
            }

            if (string.IsNullOrEmpty(sessionResult.SessionReferenceNumber))
            {
                _logger.LogError("[KSeF] No session reference number received for invoice {InvoiceId}", invoice.Id);
                invoice.Status = "Error";
                invoice.KSeFErrorMessage = "No session reference number received from KSeF";
                _invoiceRepository.UpdateInvoice(invoice);
                return;
            }

            _logger.LogInformation("[KSeF] KSeF session initialized successfully. RefNum: {RefNum}, ExpiresAt: {ExpiresAt}",
                sessionResult.SessionReferenceNumber, sessionResult.ExpiresAt);

            // Verify encryption keys were returned
            if (sessionResult.SymmetricKey == null || sessionResult.InitializationVector == null)
            {
                _logger.LogError("[KSeF] No encryption keys received from session initialization for invoice {InvoiceId}", invoice.Id);
                invoice.Status = "Error";
                invoice.KSeFErrorMessage = "No encryption keys received from KSeF session - cannot encrypt invoice";
                _invoiceRepository.UpdateInvoice(invoice);
                return;
            }

            // Step 4: Submit encrypted invoice to KSeF (KSeF 2.0)
            _logger.LogInformation("[KSeF] Step 4: Submitting encrypted invoice {InvoiceId} ({InvoiceNumber}) to KSeF 2.0",
                invoice.Id, invoice.InvoiceNumber);
            var submissionResult = await _ksefApiService.SendInvoiceAsync(
                sessionResult.SessionToken!,
                sessionResult.SessionReferenceNumber,
                faXml,
                sessionResult.SymmetricKey,
                sessionResult.InitializationVector,
                ksefCredentialsProfile.KSeFEnvironment);

            if (submissionResult.Success)
            {
                // Success - update invoice with KSeF reference
                _logger.LogInformation("[KSeF] Invoice {InvoiceId} submitted successfully to KSeF. " +
                    "KSeFReferenceNumber: {KSeFRef}, Status: {Status}",
                    invoice.Id, submissionResult.KSeFReferenceNumber, submissionResult.Status);
                invoice.KSeFReferenceNumber = submissionResult.KSeFReferenceNumber;
                invoice.Status = "Sent";
                invoice.KSeFStatus = submissionResult.Status;
                invoice.KSeFSentAt = DateTime.UtcNow;

                // Update KSeF credentials profile's last sync time (parent if using parent's credentials)
                await _businessProfileRepository.UpdateKSeFLastSyncAsync(ksefCredentialsProfile.Id);

                // Send invoice emails to buyer and seller
                await SendInvoiceEmailsAsync(invoice, businessProfile);
            }
            else
            {
                // Submission failed
                _logger.LogError("[KSeF] Invoice submission failed for {InvoiceId}. Error: {Error}, Code: {Code}",
                    invoice.Id, submissionResult.ErrorMessage, submissionResult.ErrorCode);
                invoice.Status = "Error";
                invoice.KSeFErrorMessage = $"Invoice submission failed: {submissionResult.ErrorMessage} (Code: {submissionResult.ErrorCode})";
            }

            // Step 5: Close KSeF session
            _logger.LogDebug("[KSeF] Step 5: Closing KSeF session");
            await _ksefApiService.CloseSessionAsync(sessionResult.SessionToken!, ksefCredentialsProfile.KSeFEnvironment);
            _logger.LogDebug("[KSeF] KSeF session closed");

            _invoiceRepository.UpdateInvoice(invoice);
            _logger.LogInformation("[KSeF] Invoice {InvoiceId} processing completed. Final status: {Status}",
                invoice.Id, invoice.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KSeF] Unexpected error during KSeF integration for invoice {InvoiceId}: {ErrorMessage}",
                invoice.Id, ex.Message);
            invoice.Status = "Error";
            invoice.KSeFErrorMessage = $"KSeF integration error: {ex.Message}";
            _invoiceRepository.UpdateInvoice(invoice);
        }
    }

    private async Task SendInvoiceEmailsAsync(KSeFInvoice invoice, BusinessProfile businessProfile)
    {
        try
        {
            // Generate invoice PDF
            byte[]? invoicePdf = null;
            try
            {
                _logger.LogInformation("[KSeF] Generating PDF for invoice {InvoiceNumber}", invoice.InvoiceNumber);
                invoicePdf = _pdfGenerator.GenerateInvoicePdf(invoice);
                _logger.LogInformation("[KSeF] PDF generated successfully, size: {Size} bytes", invoicePdf.Length);
            }
            catch (Exception pdfEx)
            {
                _logger.LogError(pdfEx, "[KSeF] Failed to generate PDF for invoice {InvoiceNumber}", invoice.InvoiceNumber);
                // Continue without PDF - emails will still be sent
            }

            // Get invoice description from items
            var description = "Uslugi";
            if (!string.IsNullOrEmpty(invoice.InvoiceItems))
            {
                try
                {
                    var items = JsonSerializer.Deserialize<List<KSeFInvoiceItem>>(invoice.InvoiceItems);
                    if (items?.Count > 0)
                    {
                        description = items[0].Name ?? description;
                    }
                }
                catch
                {
                    // Use default description
                }
            }

            // Send email to buyer (customer) if email is available
            if (!string.IsNullOrEmpty(invoice.BuyerEmail))
            {
                _logger.LogInformation("[KSeF] Sending invoice email to buyer: {BuyerEmail}", invoice.BuyerEmail);
                await _emailService.SendInvoiceToBuyerEmailAsync(
                    invoice.BuyerEmail,
                    invoice.BuyerName ?? "Klient",
                    invoice.InvoiceNumber,
                    invoice.SellerName,
                    invoice.GrossAmount,
                    invoice.KSeFReferenceNumber,
                    invoice.IssueDate,
                    description,
                    invoicePdf);
            }
            else
            {
                _logger.LogWarning("[KSeF] Cannot send invoice email to buyer - no email address for invoice {InvoiceId}", invoice.Id);
            }

            // Send email to seller (child business) if email is available
            if (!string.IsNullOrEmpty(businessProfile.Email))
            {
                _logger.LogInformation("[KSeF] Sending invoice email to seller (child): {SellerEmail}", businessProfile.Email);
                await _emailService.SendInvoiceToSellerEmailAsync(
                    businessProfile.Email,
                    businessProfile.DisplayName ?? businessProfile.CompanyName,
                    invoice.InvoiceNumber,
                    invoice.BuyerName ?? "Klient",
                    invoice.GrossAmount,
                    invoice.KSeFReferenceNumber,
                    invoice.IssueDate,
                    description,
                    invoicePdf);
            }
            else
            {
                _logger.LogWarning("[KSeF] Cannot send invoice email to seller - no email address for business {BusinessProfileId}", businessProfile.Id);
            }

            // Send email to parent business if there's a confirmed association
            var parentAssociation = await _parentChildAssociationRepository.GetConfirmedAssociationForChildAsync(businessProfile.Id);
            if (parentAssociation != null)
            {
                var parentProfile = parentAssociation.ParentBusinessProfile ??
                    _businessProfileRepository.GetBusinessProfileById(parentAssociation.ParentBusinessProfileId);

                if (parentProfile != null && !string.IsNullOrEmpty(parentProfile.Email))
                {
                    _logger.LogInformation("[KSeF] Sending invoice email to parent business: {ParentEmail}", parentProfile.Email);
                    // Use parent-specific email that includes child business info
                    await _emailService.SendInvoiceToParentBusinessEmailAsync(
                        parentProfile.Email,
                        parentProfile.DisplayName ?? parentProfile.CompanyName,
                        businessProfile.DisplayName ?? businessProfile.CompanyName, // child business name
                        invoice.InvoiceNumber,
                        invoice.BuyerName ?? "Klient",
                        invoice.GrossAmount,
                        invoice.KSeFReferenceNumber,
                        invoice.IssueDate,
                        description,
                        invoicePdf);
                }
                else
                {
                    _logger.LogWarning("[KSeF] Cannot send invoice email to parent business - no email address for parent {ParentBusinessProfileId}",
                        parentAssociation.ParentBusinessProfileId);
                }
            }
        }
        catch (Exception ex)
        {
            // Don't fail invoice creation if email fails
            _logger.LogError(ex, "[KSeF] Failed to send invoice emails for invoice {InvoiceId}: {ErrorMessage}",
                invoice.Id, ex.Message);
        }
    }

    /// <summary>
    /// Validates that the effective NIP for invoices is configured.
    /// If UseParentNipForInvoices is true, validates parent's NIP.
    /// Otherwise validates the business's own NIP.
    /// </summary>
    private void ValidateEffectiveNip(BusinessProfile businessProfile)
    {
        if (businessProfile.UseParentNipForInvoices)
        {
            if (businessProfile.ParentBusinessProfile == null)
            {
                _logger.LogError("[KSeF] Business profile {BusinessProfileId} uses parent NIP but has no parent configured",
                    businessProfile.Id);
                throw new InvalidOperationException($"Business profile {businessProfile.CompanyName} is configured to use parent NIP but has no parent business profile");
            }

            if (string.IsNullOrEmpty(businessProfile.ParentBusinessProfile.Nip))
            {
                _logger.LogError("[KSeF] Parent business profile {ParentBusinessProfileId} has no NIP configured",
                    businessProfile.ParentBusinessProfileId);
                throw new InvalidOperationException($"Parent business profile {businessProfile.ParentBusinessProfile.CompanyName} must have NIP to issue invoices");
            }
        }
        else
        {
            if (string.IsNullOrEmpty(businessProfile.Nip))
            {
                _logger.LogError("[KSeF] Business profile {BusinessProfileId} ({CompanyName}) has no NIP configured",
                    businessProfile.Id, businessProfile.CompanyName);
                throw new InvalidOperationException($"Business profile {businessProfile.CompanyName} must have NIP to issue invoices");
            }
        }
    }

    /// <summary>
    /// Gets the effective seller information for invoices.
    /// If UseParentNipForInvoices is true and parent exists, returns parent's data.
    /// Otherwise returns the business's own data.
    /// </summary>
    private (string Nip, string Name, string Address, string City, string PostalCode) GetEffectiveSellerInfo(BusinessProfile businessProfile)
    {
        if (businessProfile.UseParentNipForInvoices && businessProfile.ParentBusinessProfile != null)
        {
            var parent = businessProfile.ParentBusinessProfile;
            _logger.LogInformation("[KSeF] Using parent business profile for invoice seller info. Parent: {ParentId}, ParentNIP: {ParentNIP}",
                parent.Id, parent.Nip);

            return (
                parent.Nip,
                parent.CompanyName,
                parent.Address,
                parent.City,
                parent.PostalCode
            );
        }

        return (
            businessProfile.Nip,
            businessProfile.CompanyName,
            businessProfile.Address,
            businessProfile.City,
            businessProfile.PostalCode
        );
    }
}
