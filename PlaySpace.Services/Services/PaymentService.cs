using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;

namespace PlaySpace.Services.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly IFacilityRepository _facilityRepository;
        private readonly IBusinessProfileRepository _businessProfileRepository;
        private readonly ITrainingRepository _trainingRepository;
        private readonly ITrainerProfileRepository _trainerProfileRepository;
        private readonly ITPayService _tpayService;
        private readonly IPaymentCacheService _cacheService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IKSeFInvoiceService _ksefInvoiceService;
        private readonly IPendingTimeSlotReservationRepository _pendingReservationRepository;
        private readonly TPayConfiguration _tpayConfig;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(
            IPaymentRepository paymentRepository,
            IFacilityRepository facilityRepository,
            IBusinessProfileRepository businessProfileRepository,
            ITrainingRepository trainingRepository,
            ITrainerProfileRepository trainerProfileRepository,
            ITPayService tpayService,
            IPaymentCacheService cacheService,
            IPushNotificationService pushNotificationService,
            IKSeFInvoiceService ksefInvoiceService,
            IPendingTimeSlotReservationRepository pendingReservationRepository,
            IOptions<TPayConfiguration> tpayConfig,
            ILogger<PaymentService> logger)
        {
            _paymentRepository = paymentRepository;
            _facilityRepository = facilityRepository;
            _businessProfileRepository = businessProfileRepository;
            _trainingRepository = trainingRepository;
            _trainerProfileRepository = trainerProfileRepository;
            _tpayService = tpayService;
            _cacheService = cacheService;
            _pushNotificationService = pushNotificationService;
            _pendingReservationRepository = pendingReservationRepository;
            _ksefInvoiceService = ksefInvoiceService;
            _tpayConfig = tpayConfig.Value;
            _logger = logger;
        }

        public async Task<PaymentDto> ProcessPaymentAsync(CreatePaymentDto paymentDto)
        {
            // Validate input
            if (paymentDto == null)
                throw new ArgumentNullException(nameof(paymentDto));

            if (paymentDto.Amount <= 0)
                throw new ValidationException("Payment amount must be greater than zero.");

            if (paymentDto.UserId == Guid.Empty)
                throw new ValidationException("User ID is required for payment processing.");

            if (string.IsNullOrEmpty(paymentDto.CustomerEmail))
                throw new ValidationException("Customer email is required for payment processing.");

            // Get facility reservation from either format (singular or plural array)
            var facilityReservation = paymentDto.ResolvedFacilityReservation;

            // Log what we received for debugging
            _logger.LogInformation("[Payment] ProcessPaymentAsync called - UserId: {UserId}, Amount: {Amount}, FacilityReservation: {HasReservation}, FacilityReservations count: {Count}",
                paymentDto.UserId, paymentDto.Amount, facilityReservation != null, paymentDto.FacilityReservations?.Count ?? 0);

            if (facilityReservation != null)
            {
                _logger.LogInformation("[Payment] FacilityReservation details - FacilityId: {FacilityId}, Date: {Date}, Slots: {Slots}, NumberOfUsers: {NumberOfUsers}, PayForAllUsers: {PayForAllUsers}",
                    facilityReservation.FacilityId,
                    facilityReservation.Date,
                    string.Join(", ", facilityReservation.TimeSlots ?? new List<string>()),
                    facilityReservation.NumberOfUsers,
                    facilityReservation.PayForAllUsers);
            }

            // Generate payment ID first so we can link it to pending reservation
            var paymentId = Guid.NewGuid();

            // Create PendingTimeSlotReservation to lock slots during payment (if reservation details provided)
            if (facilityReservation != null)
            {
                var reservation = facilityReservation;
                _logger.LogInformation("Creating pending reservation for payment {PaymentId} - Facility: {FacilityId}, Date: {Date}, Slots: {Slots}, NumberOfUsers: {NumberOfUsers}",
                    paymentId, reservation.FacilityId, reservation.Date, string.Join(", ", reservation.TimeSlots), reservation.NumberOfUsers);

                try
                {
                    // Remove any existing pending reservation for this user/facility/date first
                    await _pendingReservationRepository.RemoveUserPendingReservationAsync(
                        reservation.FacilityId, reservation.Date, paymentDto.UserId);

                    // Create new pending reservation to lock slots for 15 minutes
                    await _pendingReservationRepository.CreatePendingReservationAsync(
                        reservation.FacilityId,
                        reservation.Date,
                        reservation.TimeSlots,
                        paymentDto.UserId,
                        reservation.TrainerProfileId,
                        paymentId,  // Link to payment
                        reservation.NumberOfUsers,
                        reservation.PayForAllUsers);

                    _logger.LogInformation("Pending reservation created successfully for user {UserId} linked to payment {PaymentId}", paymentDto.UserId, paymentId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create pending reservation - slots may already be taken");
                    throw new BusinessRuleException("The selected time slots are no longer available. Please select different slots.");
                }
            }

            // Create payment record in database
            var payment = new Payment
            {
                Id = paymentId,
                UserId = paymentDto.UserId,
                Amount = paymentDto.Amount,
                Status = "PENDING",
                Description = paymentDto.Description ?? "Spotto Booking Payment",
                Breakdown = paymentDto.Breakdown ?? "",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsRefunded = false,
                TPayTransactionId = "",
                TPayPaymentUrl = "",
                TPayStatus = "",
                TPayErrorMessage = "",
                PaymentMethod = "TPAY",
                PushToken = paymentDto.PushToken,
                NotificationId = Guid.NewGuid().ToString(),
                ReservationDetails = paymentDto.FacilityReservations?.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(paymentDto.FacilityReservations, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    })
                    : facilityReservation != null
                        ? System.Text.Json.JsonSerializer.Serialize(facilityReservation, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                        })
                        : null,
                // Store FacilityId for KSeF invoicing (from DTO or from FacilityReservation)
                FacilityId = paymentDto.FacilityId ?? facilityReservation?.FacilityId
            };

            // Determine MerchantId for marketplace transactions
            string? merchantId = paymentDto.MerchantId;
            _logger.LogDebug("Payment processing - Initial MerchantId: {MerchantId}, FacilityId: {FacilityId}", 
                merchantId, paymentDto.FacilityId);
            
            if (string.IsNullOrEmpty(merchantId) && paymentDto.FacilityId.HasValue)
            {
                // Look up facility to get associated business profile
                var facility = _facilityRepository.GetFacility(paymentDto.FacilityId.Value);
                _logger.LogDebug("Facility lookup result - Found: {Found}, BusinessProfileId: {BusinessProfileId}",
                    facility != null, facility?.BusinessProfileId);

                if (facility?.BusinessProfileId.HasValue == true)
                {
                    var businessProfile = _businessProfileRepository.GetBusinessProfileById(facility.BusinessProfileId.Value);
                    // Use parent's TPay if configured, otherwise use own
                    merchantId = GetEffectiveTPayMerchantId(businessProfile);
                    _logger.LogDebug("Business profile lookup - Found: {Found}, EffectiveTPayMerchantId: {MerchantId}, UseParentTPay: {UseParentTPay}",
                        businessProfile != null, merchantId, businessProfile?.UseParentTPay);
                }
            }
            
            if (string.IsNullOrEmpty(merchantId))
            {
                _logger.LogError("No MerchantId available - MerchantId: {MerchantId}, FacilityId: {FacilityId}", 
                    paymentDto.MerchantId, paymentDto.FacilityId);
                throw new TPayException("MerchantId is required for marketplace transactions. Either provide MerchantId directly or ensure the facility has an associated business profile with TPay registration.");
            }

            // Get POS information for marketplace transaction
            var posInfo = await _tpayService.GetPosInfoAsync();
            var pos = posInfo.list?.FirstOrDefault();
            if (pos == null)
            {
                throw new TPayException("No TPay POS configuration found");
            }

            // Create TPay marketplace transaction
            var tpayRequest = new TPayMarketplaceTransactionRequest
            {
                currency = "PLN",
                description = paymentDto.Description ?? "Spotto Payment",
                hiddenDescription = payment.Id.ToString(),
                languageCode = "PL",
                pos = new MarketplacePos
                {
                    id = pos.posId
                },
                billingAddress = new MarketplaceBillingAddress
                {
                    email = paymentDto.CustomerEmail,
                    name = paymentDto.CustomerName
                },
                transactionCallbacks = new List<TransactionCallback>
                {
                    new TransactionCallback
                    {
                        type = 3,
                        value = _tpayConfig.NotificationUrl
                    }
                },
                childTransactions = new List<ChildTransaction>
                {
                    new ChildTransaction
                    {
                        amount = (int)(paymentDto.Amount * 100),
                        description = paymentDto.Description ?? "Spotto Payment",
                        hiddenDescription = payment.Id.ToString(),
                        merchant = new MarketplaceMerchant
                        {
                            id = merchantId
                        }
                    }
                }
            };

            Payment savedPayment = null;

            try
            {
                savedPayment = await _paymentRepository.CreatePaymentAsync(payment);

                // Log the marketplace transaction request being sent to TPay
                _logger.LogInformation("Sending marketplace transaction request to TPay: {RequestBody}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayRequest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                var tpayResponse = await _tpayService.CreateMarketplaceTransactionAsync(tpayRequest);
                
                // Log TPay marketplace response
                _logger.LogInformation("TPay marketplace response: {Response}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayResponse, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // Update payment with TPay details
                savedPayment.TPayTransactionId = tpayResponse.transactionId;
                savedPayment.TPayPaymentUrl = tpayResponse.paymentUrl;
                savedPayment.TPayStatus = "CREATED";
                
                // Store child transaction IDs for refund purposes
                if (tpayResponse.childTransactions?.Any() == true)
                {
                    var childTransactionIds = tpayResponse.childTransactions.Select(ct => ct.transactionId).ToList();
                    savedPayment.TPayChildTransactionIds = System.Text.Json.JsonSerializer.Serialize(childTransactionIds);
                    _logger.LogDebug("Stored child transaction IDs for payment {PaymentId}: {ChildTransactionIds}", 
                        savedPayment.Id, string.Join(", ", childTransactionIds));
                }
                
                await _paymentRepository.UpdatePaymentAsync(savedPayment);

                var resultDto = MapToDto(savedPayment);
                resultDto.Id = savedPayment.Id;
                resultDto.TPayTransactionId = tpayResponse.transactionId;
                resultDto.TPayPaymentUrl = tpayResponse.paymentUrl;
                resultDto.TPayStatus = "CREATED";
                resultDto.Status = "PENDING";

                return resultDto;
            }
            catch (TPayException)
            {
                // Update payment status to failed
                if (savedPayment != null)
                {
                    savedPayment.Status = "FAILED";
                    savedPayment.TPayErrorMessage = "TPay service error occurred.";
                    await _paymentRepository.UpdatePaymentAsync(savedPayment);
                }
                
                // Re-throw to be handled by global exception middleware
                throw;
            }
            catch (Exception ex)
            {
                // Update payment status to failed for unexpected errors
                if (savedPayment != null)
                {
                    savedPayment.Status = "FAILED";
                    savedPayment.TPayErrorMessage = "An unexpected error occurred during payment processing.";
                    await _paymentRepository.UpdatePaymentAsync(savedPayment);
                }

                throw new BusinessRuleException("Payment processing failed due to an unexpected error.", ex);
            }
        }

        public async Task<PaymentDto> ProcessTrainingPaymentAsync(CreateTrainingPaymentDto trainingPaymentDto)
        {
            // Validate input
            if (trainingPaymentDto == null)
                throw new ArgumentNullException(nameof(trainingPaymentDto));

            if (trainingPaymentDto.UserId == Guid.Empty)
                throw new ValidationException("User ID is required for training payment processing.");

            if (trainingPaymentDto.TrainingId == Guid.Empty)
                throw new ValidationException("Training ID is required for payment processing.");

            if (string.IsNullOrEmpty(trainingPaymentDto.CustomerEmail))
                throw new ValidationException("Customer email is required for training payment processing.");

            // Get training details
            var training = _trainingRepository.GetTraining(trainingPaymentDto.TrainingId);
            if (training == null)
                throw new ValidationException("Training not found.");

            // Get trainer profile to get merchant ID
            var trainerProfile = _trainerProfileRepository.GetTrainerProfileById(training.TrainerProfileId);
            if (trainerProfile == null)
                throw new ValidationException("Trainer profile not found.");

            if (string.IsNullOrEmpty(trainerProfile.TPayMerchantId))
                throw new ValidationException("Trainer does not have TPay merchant registration. Cannot process marketplace payment.");

            _logger.LogDebug("Training payment processing - TrainingId: {TrainingId}, TrainerProfileId: {TrainerProfileId}, MerchantId: {MerchantId}", 
                trainingPaymentDto.TrainingId, training.TrainerProfileId, trainerProfile.TPayMerchantId);

            // Create payment record
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = trainingPaymentDto.UserId,
                Amount = training.Price,
                Description = $"Training: {training.Title}",
                Breakdown = $"Training Price: {training.Price:C}",
                Status = "PENDING",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsRefunded = false,
                IsConsumed = false,
                TPayTransactionId = "",
                TPayPaymentUrl = "",
                TPayStatus = "",
                TPayErrorMessage = "",
                PaymentMethod = "TPAY",
                PushToken = trainingPaymentDto.PushToken,
                // Store TrainingId and FacilityId for KSeF invoicing
                TrainingId = trainingPaymentDto.TrainingId,
                FacilityId = training.FacilityId
            };

            // Get POS information for marketplace transaction
            var posInfo = await _tpayService.GetPosInfoAsync();
            var pos = posInfo.list?.FirstOrDefault();
            if (pos == null)
            {
                throw new TPayException("No TPay POS configuration found");
            }

            // Create TPay marketplace transaction for training
            var tpayRequest = new TPayMarketplaceTransactionRequest
            {
                currency = "PLN",
                description = $"Training: {training.Title}",
                hiddenDescription = payment.Id.ToString(),
                languageCode = "PL",
                pos = new MarketplacePos
                {
                    id = pos.posId
                },
                billingAddress = new MarketplaceBillingAddress
                {
                    email = trainingPaymentDto.CustomerEmail,
                    name = trainingPaymentDto.CustomerName
                },
                transactionCallbacks = new List<TransactionCallback>
                {
                    new TransactionCallback
                    {
                        type = 3,
                        value = _tpayConfig.NotificationUrl
                    }
                },
                childTransactions = new List<ChildTransaction>
                {
                    new ChildTransaction
                    {
                        amount = (int)(training.Price * 100),
                        description = $"Training: {training.Title}",
                        hiddenDescription = payment.Id.ToString(),
                        merchant = new MarketplaceMerchant
                        {
                            id = trainerProfile.TPayMerchantId
                        }
                    }
                }
            };

            Payment savedPayment = null;

            try
            {
                savedPayment = await _paymentRepository.CreatePaymentAsync(payment);

                // Log the training marketplace transaction request being sent to TPay
                _logger.LogInformation("Sending training marketplace transaction request to TPay: {RequestBody}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayRequest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                var tpayResponse = await _tpayService.CreateMarketplaceTransactionAsync(tpayRequest);
                
                // Log TPay marketplace response
                _logger.LogInformation("TPay marketplace response: {Response}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayResponse, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // Update payment with TPay details
                savedPayment.TPayTransactionId = tpayResponse.transactionId;
                savedPayment.TPayPaymentUrl = tpayResponse.paymentUrl;
                savedPayment.TPayStatus = "CREATED";
                
                // Store child transaction IDs for refund purposes
                if (tpayResponse.childTransactions?.Any() == true)
                {
                    var childTransactionIds = tpayResponse.childTransactions.Select(ct => ct.transactionId).ToList();
                    savedPayment.TPayChildTransactionIds = System.Text.Json.JsonSerializer.Serialize(childTransactionIds);
                    _logger.LogDebug("Stored child transaction IDs for payment {PaymentId}: {ChildTransactionIds}", 
                        savedPayment.Id, string.Join(", ", childTransactionIds));
                }
                
                await _paymentRepository.UpdatePaymentAsync(savedPayment);

                var resultDto = MapToDto(savedPayment);
                resultDto.Id = savedPayment.Id;
                resultDto.TPayTransactionId = tpayResponse.transactionId;
                resultDto.TPayPaymentUrl = tpayResponse.paymentUrl;
                resultDto.TPayStatus = "CREATED";
                resultDto.Status = "PENDING";

                _logger.LogInformation("Training marketplace payment created successfully - PaymentId: {PaymentId}, TPayTransactionId: {TPayTransactionId}", 
                    savedPayment.Id, tpayResponse.transactionId);

                return resultDto;
            }
            catch (TPayException)
            {
                // TPay-specific exceptions should be handled by the controller
                // Update payment status to failed
                if (savedPayment != null)
                {
                    savedPayment.Status = "FAILED";
                    savedPayment.TPayErrorMessage = "TPay marketplace transaction failed.";
                    await _paymentRepository.UpdatePaymentAsync(savedPayment);
                }

                // Re-throw to be handled by global exception middleware
                throw;
            }
            catch (Exception ex)
            {
                // Update payment status to failed for unexpected errors
                if (savedPayment != null)
                {
                    savedPayment.Status = "FAILED";
                    savedPayment.TPayErrorMessage = "An unexpected error occurred during training payment processing.";
                    await _paymentRepository.UpdatePaymentAsync(savedPayment);
                }

                throw new BusinessRuleException("Training payment processing failed due to an unexpected error.", ex);
            }
        }

        public async Task<PaymentDto> HandleTPayNotificationAsync(TPayNotification notification)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            // Handle both new format (crc) and legacy format (tr_crc)
            var crc = notification.crc ?? notification.tr_crc;
            var status = notification.status ?? notification.tr_status;
            var error = notification.error ?? notification.tr_error;

            if (string.IsNullOrEmpty(crc))
                throw new ValidationException("Transaction CRC is required in TPay notification.");

            var payment = await _paymentRepository.GetPaymentByCrcAsync(crc);
            
            if (payment == null)
            {
                throw new NotFoundException("Payment", crc);
            }

            _logger.LogInformation("Processing TPay webhook notification for payment {PaymentId} with status {Status}", 
                payment.Id, status);

            // Update payment status based on notification
            payment.TPayStatus = status;
            payment.TPayCompletedAt = DateTime.UtcNow;

            switch (status?.ToUpper())
            {
                case "TRUE":
                case "CORRECT":
                    payment.Status = "COMPLETED";
                    payment.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Payment {PaymentId} completed successfully via webhook", payment.Id);

                    // Trigger KSeF invoice creation for completed payments
                    try
                    {
                        _logger.LogInformation("Triggering KSeF invoice creation for payment {PaymentId}", payment.Id);
                        var invoice = await _ksefInvoiceService.CreateInvoiceFromPaymentAsync(payment.Id);
                        _logger.LogInformation("KSeF invoice {InvoiceNumber} created for payment {PaymentId}", invoice.InvoiceNumber, payment.Id);
                    }
                    catch (Exception ex)
                    {
                        // Don't fail the payment processing if invoice creation fails
                        _logger.LogError(ex, "Failed to create KSeF invoice for payment {PaymentId}. Payment marked as completed, but invoice creation failed.", payment.Id);
                    }
                    break;
                case "FALSE":
                case "DECLINED":
                    payment.Status = "FAILED";
                    payment.UpdatedAt = DateTime.UtcNow;
                    payment.TPayErrorMessage = error;
                    _logger.LogWarning("Payment {PaymentId} failed via webhook: {Error}", payment.Id, error);
                    break;
                default:
                    payment.Status = "PENDING";
                    payment.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Payment {PaymentId} status updated to pending via webhook", payment.Id);
                    break;
            }

            // Update in database
            await _paymentRepository.UpdatePaymentAsync(payment);

            // 🔥 HYBRID ENHANCEMENT: Cache the updated payment for fast polling responses
            var paymentDto = MapToDto(payment);
            await _cacheService.CachePaymentStatusAsync(payment.Id.ToString(), paymentDto);

            _logger.LogInformation("Payment {PaymentId} webhook processed and cached successfully", payment.Id);

            return paymentDto;
        }

        public async Task<PaymentDto> HandleTPayMarketplaceNotificationAsync(TPayMarketplaceNotification notification)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            if (notification.data == null)
                throw new ValidationException("Notification data is required in TPay marketplace notification.");

            if (string.IsNullOrEmpty(notification.data.transactionHiddenDescription))
                throw new ValidationException("Transaction hidden description is required in TPay marketplace notification.");

            // Use transactionHiddenDescription to find the payment (contains our payment ID)
            if (!Guid.TryParse(notification.data.transactionHiddenDescription, out var paymentId))
                throw new ValidationException("Invalid payment ID in transaction hidden description.");

            var payment = await _paymentRepository.GetPaymentByIdAsync(paymentId);
            
            if (payment == null)
            {
                throw new NotFoundException("Payment", paymentId.ToString());
            }

            _logger.LogInformation("Processing TPay marketplace notification for payment {PaymentId} with status {Status}",
                payment.Id, notification.data.transactionStatus);

            // Idempotency check - prevent duplicate processing
            if (payment.Status == "COMPLETED" && notification.data.transactionStatus?.ToLower() == "correct")
            {
                _logger.LogInformation("Duplicate notification received for already completed payment {PaymentId}, returning existing payment", payment.Id);
                return MapToDto(payment);
            }

            // Update payment with marketplace notification data
            payment.TPayTransactionId = notification.data.transactionId;
            payment.TPayStatus = notification.data.transactionStatus;
            payment.TPayCompletedAt = DateTime.UtcNow;

            // Parse transaction date if provided
            if (DateTime.TryParse(notification.data.transactionDate, out var transactionDate))
            {
                // Ensure DateTime is in UTC for PostgreSQL compatibility
                payment.TPayCompletedAt = transactionDate.Kind == DateTimeKind.Unspecified 
                    ? DateTime.SpecifyKind(transactionDate, DateTimeKind.Utc)
                    : transactionDate.ToUniversalTime();
            }

            switch (notification.data.transactionStatus?.ToLower())
            {
                case "correct":
                    payment.Status = "COMPLETED";
                    payment.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Marketplace payment {PaymentId} completed successfully via webhook", payment.Id);

                    // Trigger KSeF invoice creation for completed payments
                    try
                    {
                        _logger.LogInformation("Triggering KSeF invoice creation for payment {PaymentId}", payment.Id);
                        var invoice = await _ksefInvoiceService.CreateInvoiceFromPaymentAsync(payment.Id);
                        _logger.LogInformation("KSeF invoice {InvoiceNumber} created for payment {PaymentId}", invoice.InvoiceNumber, payment.Id);
                    }
                    catch (Exception ex)
                    {
                        // Don't fail the payment processing if invoice creation fails
                        _logger.LogError(ex, "Failed to create KSeF invoice for payment {PaymentId}. Payment marked as completed, but invoice creation failed.", payment.Id);
                    }
                    break;
                default:
                    payment.Status = "PENDING";
                    payment.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation("Marketplace payment {PaymentId} status: {Status}", payment.Id, notification.data.transactionStatus);
                    break;
            }

            // Validate amounts match
            if (notification.data.transactionAmount != payment.Amount)
            {
                _logger.LogWarning("Marketplace payment {PaymentId} amount mismatch: expected {Expected}, got {Actual}", 
                    payment.Id, payment.Amount, notification.data.transactionAmount);
            }

            if (notification.data.transactionPaidAmount != notification.data.transactionAmount)
            {
                _logger.LogWarning("Marketplace payment {PaymentId} paid amount differs from transaction amount: {Paid} vs {Transaction}", 
                    payment.Id, notification.data.transactionPaidAmount, notification.data.transactionAmount);
            }

            // Update in database
            await _paymentRepository.UpdatePaymentAsync(payment);

            // 🔥 HYBRID ENHANCEMENT: Cache the updated payment for fast polling responses
            var paymentDto = MapToDto(payment);
            await _cacheService.CachePaymentStatusAsync(payment.Id.ToString(), paymentDto);

            _logger.LogInformation("Marketplace payment {PaymentId} webhook processed and cached successfully", payment.Id);

            return paymentDto;
        }

        public async Task<PaymentDto> ProcessSplitPaymentAsync(CreateSplitPaymentDto splitPaymentDto)
        {
            // Validate input
            if (splitPaymentDto == null)
                throw new ArgumentNullException(nameof(splitPaymentDto));

            if (splitPaymentDto.PaymentItems == null || !splitPaymentDto.PaymentItems.Any())
                throw new ValidationException("At least one payment item is required for split payments.");

            if (splitPaymentDto.PaymentItems.Any(item => item.Amount <= 0))
                throw new ValidationException("All payment item amounts must be greater than zero.");

            if (splitPaymentDto.UserId == Guid.Empty)
                throw new ValidationException("User ID is required for payment processing.");

            if (string.IsNullOrEmpty(splitPaymentDto.CustomerEmail))
                throw new ValidationException("Customer email is required for payment processing.");

            var totalAmount = splitPaymentDto.PaymentItems.Sum(item => item.Amount);

            // Create payment record in database
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = splitPaymentDto.UserId,
                Amount = totalAmount,
                Status = "PENDING",
                Description = splitPaymentDto.Description ?? "Spotto Split Payment",
                Breakdown = System.Text.Json.JsonSerializer.Serialize(splitPaymentDto.PaymentItems),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsRefunded = false,
                TPayTransactionId = "",
                TPayPaymentUrl = "",
                TPayStatus = "",
                TPayErrorMessage = "",
                PaymentMethod = "TPAY_MARKETPLACE"
            };

            // Get POS information for marketplace transaction
            var posInfo = await _tpayService.GetPosInfoAsync();
            var pos = posInfo.list?.FirstOrDefault();
            if (pos == null)
            {
                throw new TPayException("No TPay POS configuration found");
            }

            // Create TPay marketplace transaction
            var tpayRequest = new TPayMarketplaceTransactionRequest
            {
                currency = splitPaymentDto.Currency,
                description = splitPaymentDto.Description,
                hiddenDescription = splitPaymentDto.HiddenDescription ?? payment.Id.ToString(),
                languageCode = splitPaymentDto.LanguageCode,
                preSelectedChannelId = splitPaymentDto.PreSelectedChannelId,
                pos = new MarketplacePos
                {
                    id = pos.posId
                },
                billingAddress = new MarketplaceBillingAddress
                {
                    email = splitPaymentDto.CustomerEmail,
                    name = splitPaymentDto.CustomerName,
                    phone = splitPaymentDto.CustomerPhone,
                    street = splitPaymentDto.CustomerStreet,
                    postalCode = splitPaymentDto.CustomerPostalCode,
                    city = splitPaymentDto.CustomerCity,
                    country = splitPaymentDto.CustomerCountry,
                    houseNo = splitPaymentDto.CustomerHouseNo,
                    flatNo = splitPaymentDto.CustomerFlatNo
                },
                transactionCallbacks = new List<TransactionCallback>
                {
                    new TransactionCallback
                    {
                        type = 3,
                        value = _tpayConfig.NotificationUrl
                    }
                },
                childTransactions = splitPaymentDto.PaymentItems.Select(item => new ChildTransaction
                {
                    amount = item.Amount,
                    description = item.Description,
                    hiddenDescription = item.HiddenDescription ?? payment.Id.ToString(),
                    merchant = new MarketplaceMerchant
                    {
                        id = item.MerchantId
                    },
                    products = item.Products.Select(product => new MarketplaceProduct
                    {
                        name = product.Name,
                        externalId = product.ExternalId,
                        quantity = product.Quantity,
                        unitPrice = product.UnitPrice
                    }).ToList()
                }).ToList()
            };

            Payment savedPayment = null;

            try
            {
                savedPayment = await _paymentRepository.CreatePaymentAsync(payment);

                // Log the split payment marketplace transaction request being sent to TPay
                _logger.LogInformation("Sending split payment marketplace transaction request to TPay: {RequestBody}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayRequest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                var tpayResponse = await _tpayService.CreateMarketplaceTransactionAsync(tpayRequest);
                
                // Log TPay marketplace response
                _logger.LogInformation("TPay marketplace response: {Response}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayResponse, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                // Update payment with TPay details
                savedPayment.TPayTransactionId = tpayResponse.transactionId;
                savedPayment.TPayPaymentUrl = tpayResponse.paymentUrl;
                savedPayment.TPayStatus = "CREATED";
                
                // Store child transaction IDs for refund purposes
                if (tpayResponse.childTransactions?.Any() == true)
                {
                    var childTransactionIds = tpayResponse.childTransactions.Select(ct => ct.transactionId).ToList();
                    savedPayment.TPayChildTransactionIds = System.Text.Json.JsonSerializer.Serialize(childTransactionIds);
                    _logger.LogDebug("Stored child transaction IDs for payment {PaymentId}: {ChildTransactionIds}", 
                        savedPayment.Id, string.Join(", ", childTransactionIds));
                }
                
                await _paymentRepository.UpdatePaymentAsync(savedPayment);

                var resultDto = MapToDto(savedPayment);
                resultDto.Id = savedPayment.Id;
                resultDto.TPayTransactionId = tpayResponse.transactionId;
                resultDto.TPayPaymentUrl = tpayResponse.paymentUrl;
                resultDto.TPayStatus = "CREATED";
                resultDto.Status = "PENDING";

                return resultDto;
            }
            catch (TPayException)
            {
                // Update payment status to failed
                if (savedPayment != null)
                {
                    savedPayment.Status = "FAILED";
                    savedPayment.TPayErrorMessage = "TPay marketplace service error occurred.";
                    await _paymentRepository.UpdatePaymentAsync(savedPayment);
                }
                
                // Re-throw to be handled by global exception middleware
                throw;
            }
            catch (Exception ex)
            {
                // Update payment status to failed for unexpected errors
                if (savedPayment != null)
                {
                    savedPayment.Status = "FAILED";
                    savedPayment.TPayErrorMessage = "An unexpected error occurred during split payment processing.";
                    await _paymentRepository.UpdatePaymentAsync(savedPayment);
                }

                throw new BusinessRuleException("Split payment processing failed due to an unexpected error.", ex);
            }
        }

        public PaymentDto ProcessPayment(PaymentDto payment)
        {
            // Convert PaymentDto to CreatePaymentDto for the async method
            var createPaymentDto = new CreatePaymentDto
            {
                UserId = payment.UserId,
                Amount = payment.Amount,
                Description = payment.Description ?? "Spotto Payment",
                Breakdown = payment.Breakdown,
                CustomerEmail = payment.CustomerEmail ?? "",
                CustomerName = payment.CustomerName ?? "",
                CustomerPhone = payment.CustomerPhone ?? "",
                ReturnUrl = payment.ReturnUrl ?? "",
                ErrorUrl = payment.ErrorUrl ?? ""
            };
            
            return ProcessPaymentAsync(createPaymentDto).Result;
        }

        public async Task<PaymentDto> GetPaymentByIdAsync(Guid paymentId)
        {
            if (paymentId == Guid.Empty)
                throw new ValidationException("Payment ID cannot be empty.");

            try
            {
                // 🔥 HYBRID ENHANCEMENT: Check cache first for ultra-fast response
                var cachedPayment = await _cacheService.GetCachedPaymentStatusAsync(paymentId.ToString());
                if (cachedPayment != null)
                {
                    _logger.LogDebug("Returning cached payment status for {PaymentId}", paymentId);
                    return cachedPayment;
                }

                // Cache miss - get from database
                _logger.LogDebug("Cache miss for payment {PaymentId}, querying database", paymentId);
                var payment = await _paymentRepository.GetPaymentByIdAsync(paymentId);
                
                if (payment == null)
                {
                    throw new NotFoundException("Payment", paymentId.ToString());
                }

                var paymentDto = MapToDto(payment);

                // Cache the result for future requests
                await _cacheService.CachePaymentStatusAsync(paymentId.ToString(), paymentDto);

                return paymentDto;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment {PaymentId}", paymentId);
                throw new BusinessRuleException("Failed to retrieve payment information.", ex);
            }
        }

        public async Task<MarketplaceTransactionResponseDto> ProcessMarketplaceTransactionAsync(CreateMarketplaceTransactionDto marketplaceDto)
        {
            // Validate input
            if (marketplaceDto == null)
                throw new ArgumentNullException(nameof(marketplaceDto));

            if (marketplaceDto.ChildTransactions == null || !marketplaceDto.ChildTransactions.Any())
                throw new ValidationException("At least one child transaction is required for marketplace transactions.");

            if (marketplaceDto.ChildTransactions.Any(ct => ct.Amount <= 0))
                throw new ValidationException("All child transaction amounts must be greater than zero.");

            if (string.IsNullOrEmpty(marketplaceDto.BillingAddress?.Email))
                throw new ValidationException("Billing address email is required for marketplace transactions.");

            if (string.IsNullOrEmpty(marketplaceDto.BillingAddress?.Name))
                throw new ValidationException("Billing address name is required for marketplace transactions.");

            // Create PendingTimeSlotReservation to lock slots during payment (if reservation details provided)
            if (marketplaceDto.FacilityReservation != null && marketplaceDto.UserId.HasValue)
            {
                var reservation = marketplaceDto.FacilityReservation;
                _logger.LogInformation("[Marketplace] Creating pending reservation - Facility: {FacilityId}, Date: {Date}, Slots: {Slots}, User: {UserId}",
                    reservation.FacilityId, reservation.Date, string.Join(", ", reservation.TimeSlots), marketplaceDto.UserId);

                try
                {
                    // Remove any existing pending reservation for this user/facility/date first
                    await _pendingReservationRepository.RemoveUserPendingReservationAsync(
                        reservation.FacilityId, reservation.Date, marketplaceDto.UserId.Value);

                    // Create new pending reservation to lock slots for 15 minutes
                    await _pendingReservationRepository.CreatePendingReservationAsync(
                        reservation.FacilityId,
                        reservation.Date,
                        reservation.TimeSlots,
                        marketplaceDto.UserId.Value,
                        reservation.TrainerProfileId);

                    _logger.LogInformation("[Marketplace] Pending reservation created successfully for user {UserId}", marketplaceDto.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Marketplace] Failed to create pending reservation - slots may already be taken");
                    throw new BusinessRuleException("The selected time slots are no longer available. Please select different slots.");
                }
            }
            else
            {
                _logger.LogDebug("[Marketplace] No FacilityReservation provided - skipping pending reservation creation. UserId: {UserId}, HasReservation: {HasReservation}",
                    marketplaceDto.UserId, marketplaceDto.FacilityReservation != null);
            }

            // Get POS information for marketplace transaction
            var posInfo = await _tpayService.GetPosInfoAsync();
            var pos = posInfo.list?.FirstOrDefault();
            if (pos == null)
            {
                throw new TPayException("No TPay POS configuration found");
            }

            // Create TPay marketplace transaction request
            var tpayRequest = new TPayMarketplaceTransactionRequest
            {
                currency = marketplaceDto.Currency,
                description = marketplaceDto.Description,
                hiddenDescription = marketplaceDto.HiddenDescription ?? Guid.NewGuid().ToString(),
                languageCode = marketplaceDto.LanguageCode,
                preSelectedChannelId = marketplaceDto.PreSelectedChannelId,
                pos = new MarketplacePos
                {
                    id = marketplaceDto.Pos?.Id ?? pos.posId
                },
                billingAddress = new MarketplaceBillingAddress
                {
                    email = marketplaceDto.BillingAddress.Email,
                    name = marketplaceDto.BillingAddress.Name,
                    phone = marketplaceDto.BillingAddress.Phone,
                    street = marketplaceDto.BillingAddress.Street,
                    postalCode = marketplaceDto.BillingAddress.PostalCode,
                    city = marketplaceDto.BillingAddress.City,
                    country = marketplaceDto.BillingAddress.Country,
                    houseNo = marketplaceDto.BillingAddress.HouseNo,
                    flatNo = marketplaceDto.BillingAddress.FlatNo
                },
                transactionCallbacks = new List<TransactionCallback>
                {
                    new TransactionCallback
                    {
                        type = 3,
                        value = _tpayConfig.NotificationUrl
                    }
                },
                childTransactions = marketplaceDto.ChildTransactions.Select(ct => new ChildTransaction
                {
                    amount = (int)(ct.Amount * 100),
                    description = ct.Description,
                    hiddenDescription = ct.HiddenDescription ?? Guid.NewGuid().ToString(),
                    merchant = new MarketplaceMerchant
                    {
                        id = ct.Merchant.Id
                    },
                    products = ct.Products.Select(p => new MarketplaceProduct
                    {
                        name = p.Name,
                        externalId = p.ExternalId,
                        quantity = p.Quantity,
                        unitPrice = (int)(p.UnitPrice * 100)
                    }).ToList()
                }).ToList()
            };

            try
            {
                _logger.LogInformation("Creating TPay marketplace transaction with {ChildCount} child transactions", 
                    marketplaceDto.ChildTransactions.Count);

                // Log the marketplace transaction request being sent to TPay
                _logger.LogInformation("Sending marketplace transaction request to TPay: {RequestBody}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayRequest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                var tpayResponse = await _tpayService.CreateMarketplaceTransactionAsync(tpayRequest);
                
                // Log TPay marketplace response
                _logger.LogInformation("TPay marketplace response: {Response}", 
                    System.Text.Json.JsonSerializer.Serialize(tpayResponse, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                _logger.LogInformation("TPay marketplace transaction created successfully with ID {TransactionId}", 
                    tpayResponse.transactionId);

                return new MarketplaceTransactionResponseDto
                {
                    TransactionId = tpayResponse.transactionId,
                    Title = tpayResponse.title,
                    PaymentUrl = tpayResponse.paymentUrl
                };
            }
            catch (TPayException)
            {
                _logger.LogError("TPay marketplace service error occurred");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating marketplace transaction");
                throw new BusinessRuleException("Marketplace transaction processing failed due to an unexpected error.", ex);
            }
        }

        public async Task<PaymentDto> GetPaymentByTransactionIdAsync(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
                throw new ValidationException("Transaction ID cannot be empty.");

            try
            {
                var payment = await _paymentRepository.GetPaymentByTransactionIdAsync(transactionId);
                
                if (payment == null)
                {
                    throw new NotFoundException("Payment", $"with transaction ID '{transactionId}'");
                }

                return MapToDto(payment);
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment by transaction ID {TransactionId}", transactionId);
                throw new BusinessRuleException("Failed to retrieve payment information.", ex);
            }
        }

        public async Task<List<PaymentDto>> GetPendingPaymentsForUserAsync(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving pending payments for user {UserId}", userId);
                
                // Calculate 15 minutes ago
                var fifteenMinutesAgo = DateTime.UtcNow.AddMinutes(-15);
                
                var payments = await _paymentRepository.GetPendingPaymentsForUserAsync(userId, fifteenMinutesAgo);
                
                _logger.LogInformation("Found {Count} pending payments for user {UserId} within last 15 minutes", 
                    payments.Count, userId);
                
                return payments.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving pending payments for user {UserId}", userId);
                throw new BusinessRuleException("Failed to retrieve pending payments.", ex);
            }
        }

        public async Task<PaymentDto> CancelPaymentAsync(Guid paymentId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Attempting to cancel payment {PaymentId} for user {UserId}", paymentId, userId);
                
                // Get the payment
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                    throw new NotFoundException("Payment not found.");
                }

                // Verify the payment belongs to the requesting user
                if (payment.UserId != userId)
                {
                    _logger.LogWarning("User {UserId} attempted to cancel payment {PaymentId} belonging to user {PaymentUserId}", 
                        userId, paymentId, payment.UserId);
                    throw new ValidationException("You can only cancel your own payments.");
                }

                // Check if payment is in a cancellable state
                if (payment.Status != "PENDING")
                {
                    _logger.LogWarning("Cannot cancel payment {PaymentId} with status {Status}", paymentId, payment.Status);
                    throw new ValidationException($"Cannot cancel payment with status {payment.Status}. Only pending payments can be cancelled.");
                }

                // Update payment status
                payment.Status = "CANCELLED BY USER";
                payment.UpdatedAt = DateTime.UtcNow;

                var updatedPayment = await _paymentRepository.UpdateAsync(payment);
                
                _logger.LogInformation("Successfully cancelled payment {PaymentId} for user {UserId}", paymentId, userId);
                
                return MapToDto(updatedPayment);
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling payment {PaymentId} for user {UserId}", paymentId, userId);
                throw new BusinessRuleException("Failed to cancel payment.", ex);
            }
        }

        public async Task<PaymentDto> RefundPaymentAsync(Guid paymentId, decimal refundAmount, Guid facilityId)
        {
            try
            {
                _logger.LogInformation("Attempting to refund payment {PaymentId} with amount {RefundAmount}", paymentId, refundAmount);
                
                // Get the payment
                var payment = await _paymentRepository.GetByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                    throw new NotFoundException("Payment not found.");
                }

                // Validate payment is completed and can be refunded
                if (payment.Status != "COMPLETED")
                {
                    _logger.LogWarning("Cannot refund payment {PaymentId} with status {Status}", paymentId, payment.Status);
                    throw new ValidationException($"Cannot refund payment with status {payment.Status}. Only completed payments can be refunded.");
                }

                // Validate refund amount
                var remainingAmount = payment.Amount - payment.RefundedAmount;
                if (refundAmount <= 0 || refundAmount > remainingAmount)
                {
                    _logger.LogWarning("Invalid refund amount {RefundAmount} for payment {PaymentId}. Remaining refundable amount: {RemainingAmount}", 
                        refundAmount, paymentId, remainingAmount);
                    throw new ValidationException($"Invalid refund amount. Refundable amount: {remainingAmount:C}");
                }

                // Get the facility to access the business profile
                var facility = _facilityRepository.GetFacility(facilityId);
                if (facility == null)
                    throw new InvalidOperationException("Associated facility not found");

                // Get the business profile to access the TPay merchant ID
                var businessProfile = _businessProfileRepository.GetBusinessProfileById(facility.BusinessProfileId!.Value);
                var effectiveMerchantId = GetEffectiveTPayMerchantId(businessProfile);
                if (businessProfile == null || string.IsNullOrEmpty(effectiveMerchantId))
                    throw new InvalidOperationException("Business profile or TPay merchant ID not found for this facility");

                // Create TPay refund request using the effective merchantId (parent or own)
                var tpayRefundRequest = new TPayRefundRequest
                {
                    childTransactions = new List<RefundChildTransaction>
                    {
                        new RefundChildTransaction
                        {
                            merchantId = effectiveMerchantId,
                            amount = (int)(refundAmount * 100), // Convert to grosz (cents)
                            products = new List<RefundProduct>() // Empty for reservations
                        }
                    }
                };

                // Execute the refund via TPay
                var tpayRefundResponse = await _tpayService.CreateRefundAsync(payment.TPayTransactionId, tpayRefundRequest);

                // Update payment with refund information
                payment.RefundedAmount += refundAmount;
                payment.RefundedAt = DateTime.UtcNow;
                payment.RefundTransactionId = tpayRefundResponse.requestId;
                payment.IsRefunded = payment.RefundedAmount >= payment.Amount;
                payment.Status = payment.IsRefunded ? "REFUNDED" : "PARTIALLY_REFUNDED";
                payment.UpdatedAt = DateTime.UtcNow;

                var updatedPayment = await _paymentRepository.UpdateAsync(payment);
                
                _logger.LogInformation("Successfully refunded payment {PaymentId} with amount {RefundAmount}. Total refunded: {TotalRefunded}", 
                    paymentId, refundAmount, updatedPayment.RefundedAmount);
                
                return MapToDto(updatedPayment);
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (TPayException ex)
            {
                _logger.LogError(ex, "TPay refund failed for payment {PaymentId}", paymentId);
                throw new BusinessRuleException("Refund failed due to payment provider error.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding payment {PaymentId}", paymentId);
                throw new BusinessRuleException("Failed to process refund.", ex);
            }
        }

        public async Task ProcessCompletedPaymentAsync(Guid paymentId)
        {
            try
            {
                _logger.LogInformation("Processing completed payment {PaymentId} for push notification", paymentId);
                
                var payment = await _paymentRepository.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                    return;
                }

                if (payment.Status != "COMPLETED")
                {
                    _logger.LogWarning("Payment {PaymentId} not completed. Status: {Status}", paymentId, payment.Status);
                    return;
                }

                // Send push notification - let the background service handle auto-reservation
                if (!string.IsNullOrEmpty(payment.PushToken))
                {
                    await _pushNotificationService.SendPaymentCompletedAsync(payment.PushToken, payment.Id);
                    _logger.LogInformation("Push notification sent for completed payment {PaymentId}", paymentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process completed payment {PaymentId}", paymentId);
            }
        }


        /// <summary>
        /// Gets the effective TPay MerchantId for a business profile.
        /// If UseParentTPay is true and parent has TPay configured, returns parent's MerchantId.
        /// Otherwise returns the business's own MerchantId.
        /// </summary>
        private string? GetEffectiveTPayMerchantId(BusinessProfile? businessProfile)
        {
            if (businessProfile == null)
                return null;

            if (businessProfile.UseParentTPay && businessProfile.ParentBusinessProfile != null)
            {
                return businessProfile.ParentBusinessProfile.TPayMerchantId;
            }

            return businessProfile.TPayMerchantId;
        }

        private PaymentDto MapToDto(Payment payment)
        {
            return new PaymentDto
            {
                Id = payment.Id,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Status = payment.Status,
                Description = payment.Description,
                Breakdown = payment.Breakdown,
                CreatedAt = payment.CreatedAt,
                IsRefunded = payment.IsRefunded,
                IsConsumed = payment.IsConsumed,
                TPayTransactionId = payment.TPayTransactionId,
                TPayPaymentUrl = payment.TPayPaymentUrl,
                TPayStatus = payment.TPayStatus,
                TPayCompletedAt = payment.TPayCompletedAt,
                TPayErrorMessage = payment.TPayErrorMessage,
                PaymentMethod = payment.PaymentMethod,
                TPayChildTransactionIds = payment.TPayChildTransactionIds,
                RefundedAmount = payment.RefundedAmount,
                RefundedAt = payment.RefundedAt,
                RefundTransactionId = payment.RefundTransactionId,
                PushToken = payment.PushToken,
                NotificationId = payment.NotificationId,
                ReservationDetails = payment.ReservationDetails
            };
        }
    }
}
