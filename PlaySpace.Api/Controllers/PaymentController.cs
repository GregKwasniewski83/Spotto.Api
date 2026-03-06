using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Models;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Services.Interfaces;
using PlaySpace.Services.Services;

namespace PlaySpace.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ITPayService _tpayService;
        private readonly ILogger<PaymentController> _logger;
        private readonly TPayConfiguration _tpayConfig;
        private readonly PlaySpace.Services.BackgroundServices.AutoReservationService _autoReservationService;
        private readonly IServiceProvider _serviceProvider;

        public PaymentController(IPaymentService paymentService, ITPayService tpayService, ILogger<PaymentController> logger, IOptions<TPayConfiguration> tpayConfig, PlaySpace.Services.BackgroundServices.AutoReservationService autoReservationService, IServiceProvider serviceProvider)
        {
            _paymentService = paymentService;
            _tpayService = tpayService;
            _logger = logger;
            _tpayConfig = tpayConfig.Value;
            _autoReservationService = autoReservationService;
            _serviceProvider = serviceProvider;
        }

        [HttpPost("create")]
        public async Task<ActionResult<PaymentDto>> CreatePayment([FromBody] CreatePaymentDto payment)
        {
            try
            {
                var result = await _paymentService.ProcessPaymentAsync(payment);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment");
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult<PaymentDto> ProcessPayment([FromBody] PaymentDto payment)
        {
            var result = _paymentService.ProcessPayment(payment);
            return Ok(result);
        }

        [HttpPost("tpay-notification")]
        public async Task<IActionResult> TPayNotification()
        {
            Console.WriteLine(">>> WEBHOOK METHOD CALLED <<<");
            _logger.LogInformation("=== TPAY WEBHOOK RECEIVED ===");
            _logger.LogInformation("Headers: {Headers}", string.Join(", ", Request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));
            _logger.LogInformation("Content-Type: {ContentType}", Request.ContentType);
            
            // Parse form data manually
            var form = await Request.ReadFormAsync();
            var notification = new TPayNotification
            {
                tr_id = form["tr_id"],
                tr_status = form["tr_status"],
                tr_crc = form["tr_crc"],
                tr_amount = decimal.TryParse(form["tr_amount"], out var amount) ? amount : null,
                tr_error = form["tr_error"],
                md5sum = form["md5sum"],
                id = form["id"]
            };
            
            _logger.LogInformation("Parsed notification - tr_id: {TrId}, tr_status: {TrStatus}, tr_crc: {TrCrc}", 
                notification.tr_id, notification.tr_status, notification.tr_crc);
            
            try
            {
                _logger.LogInformation("Received TPay notification for transaction {TransactionId}", notification?.tr_id ?? "NULL");

                // Validate MD5 signature (MANDATORY for production)
                var isValid = await _tpayService.ValidateNotificationAsync(notification, _tpayConfig.Md5Key);
                if (!isValid)
                {
                    _logger.LogWarning("Invalid MD5 signature in TPay notification for transaction {TrId}", notification.tr_id);
                    return BadRequest("Invalid notification signature");
                }
                _logger.LogInformation("MD5 validation passed for transaction {TrId}", notification.tr_id);

                var payment = await _paymentService.HandleTPayNotificationAsync(notification);

                _logger.LogInformation("Payment {PaymentId} updated to status {Status}", payment.Id, payment.Status);

                // Queue completed payment for auto-reservation and push notification processing
                if (payment.Status == "COMPLETED" && !payment.IsConsumed)
                {
                    _logger.LogInformation("Queueing payment {PaymentId} for auto-reservation and push notification", payment.Id);
                    await _autoReservationService.QueuePaymentForProcessingAsync(payment.Id.Value);
                }

                // TPay expects "TRUE" response for successful processing
                return Ok("TRUE");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing TPay notification");
                return BadRequest("FALSE");
            }
        }

        [HttpPost("tpay-marketplace-notification")]
        public async Task<IActionResult> TPayMarketplaceNotification()
        {
            _logger.LogInformation("=== TPAY MARKETPLACE WEBHOOK RECEIVED ===");
            _logger.LogInformation("Headers: {Headers}", string.Join(", ", Request.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));
            _logger.LogInformation("Content-Type: {ContentType}", Request.ContentType);

            try
            {
                // Read raw body for JWS signature verification
                Request.EnableBuffering();
                var rawBody = await new StreamReader(Request.Body).ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogDebug("Raw body length: {BodyLength}", rawBody.Length);

                if (string.IsNullOrEmpty(rawBody))
                {
                    _logger.LogWarning("Received empty marketplace notification body");
                    return BadRequest(new TPayMarketplaceNotificationResponse { result = false });
                }

                // Get JWS signature from header
                var jwsSignature = Request.Headers["X-JWS-Signature"].FirstOrDefault();
                if (string.IsNullOrEmpty(jwsSignature))
                {
                    _logger.LogWarning("Missing X-JWS-Signature header in marketplace notification");
                    return BadRequest(new TPayMarketplaceNotificationResponse { result = false });
                }

                _logger.LogDebug("JWS Signature received: {JwsSignature}", jwsSignature);

                // Verify JWS signature (MANDATORY for production)
                var jwsService = _serviceProvider.GetRequiredService<ITPayJwsVerificationService>();
                var isValidSignature = await jwsService.VerifyJwsSignatureAsync(rawBody, jwsSignature);
                if (!isValidSignature)
                {
                    _logger.LogWarning("Invalid JWS signature for marketplace notification");
                    return Unauthorized(new TPayMarketplaceNotificationResponse { result = false });
                }
                _logger.LogInformation("JWS signature verified successfully");

                // Parse notification from raw body
                TPayMarketplaceNotification notification;
                try
                {
                    notification = System.Text.Json.JsonSerializer.Deserialize<TPayMarketplaceNotification>(rawBody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize marketplace notification");
                    return BadRequest(new TPayMarketplaceNotificationResponse { result = false });
                }

                if (notification == null)
                {
                    _logger.LogWarning("Failed to parse marketplace notification");
                    return BadRequest(new TPayMarketplaceNotificationResponse { result = false });
                }

                _logger.LogInformation("Notification Type: {Type}, Transaction ID: {TransactionId}, Status: {Status}", 
                    notification.type, notification.data?.transactionId, notification.data?.transactionStatus);

                _logger.LogInformation("Processing TPay marketplace notification for transaction {TransactionId} with status {Status}", 
                    notification.data?.transactionId, notification.data?.transactionStatus);

                var payment = await _paymentService.HandleTPayMarketplaceNotificationAsync(notification);
                
                _logger.LogInformation("Marketplace payment {PaymentId} updated to status {Status}", 
                    payment.Id, payment.Status);

                // Queue completed payment for auto-reservation and push notification processing
                if (payment.Status == "COMPLETED" && !payment.IsConsumed)
                {
                    _logger.LogInformation("Queueing payment {PaymentId} for auto-reservation and push notification", payment.Id);

                    // Queue auto-reservation processing in background
                    // AutoReservationService will handle both reservation creation AND push notifications
                    await _autoReservationService.QueuePaymentForProcessingAsync(payment.Id.Value);
                }

                // TPay expects {"result": true} response for successful processing
                return Ok(new TPayMarketplaceNotificationResponse { result = true });
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning("Validation error in marketplace notification: {Error}", ex.Message);
                return BadRequest(new TPayMarketplaceNotificationResponse { result = false });
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning("Payment not found in marketplace notification: {Error}", ex.Message);
                return NotFound(new TPayMarketplaceNotificationResponse { result = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing TPay marketplace notification");
                return StatusCode(500, new TPayMarketplaceNotificationResponse { result = false });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<PaymentDto>> GetPayment(Guid id)
        {
            try
            {
                _logger.LogDebug("Getting payment status for payment {PaymentId}", id);
                
                var payment = await _paymentService.GetPaymentByIdAsync(id);
                
                // Add cache headers for mobile app optimization
                Response.Headers.Add("Cache-Control", "no-cache");
                Response.Headers.Add("X-Payment-Status", payment.Status);
                
                return Ok(payment);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning("Payment {PaymentId} not found", id);
                return NotFound(new { error = "PAYMENT_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment {PaymentId}", id);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve payment" });
            }
        }

        [HttpGet("status/{transactionId}")]
        public async Task<ActionResult<PaymentDto>> GetPaymentByTransaction(string transactionId)
        {
            try
            {
                _logger.LogDebug("Getting payment status for transaction {TransactionId}", transactionId);
                
                var payment = await _paymentService.GetPaymentByTransactionIdAsync(transactionId);
                
                return Ok(new { 
                    transactionId = transactionId,
                    status = payment.Status,
                    paymentId = payment.Id,
                    completedAt = payment.TPayCompletedAt
                });
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning("Payment with transaction {TransactionId} not found", transactionId);
                return NotFound(new { error = "PAYMENT_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment status for transaction {TransactionId}", transactionId);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve payment status" });
            }
        }

        [HttpPost("training")]
        public async Task<ActionResult<PaymentDto>> CreateTrainingPayment([FromBody] CreateTrainingPaymentDto trainingPaymentDto)
        {
            try
            {
                _logger.LogInformation("Creating training payment for training {TrainingId} and user {UserId}", 
                    trainingPaymentDto.TrainingId, trainingPaymentDto.UserId);

                var result = await _paymentService.ProcessTrainingPaymentAsync(trainingPaymentDto);
                
                _logger.LogInformation("Training payment created successfully with ID {PaymentId}", result.Id);

                return StatusCode(201, result);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning("Validation error creating training payment: {Error}", ex.Message);
                return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogError(ex, "Business rule error creating training payment");
                return BadRequest(new { error = "BUSINESS_ERROR", message = ex.Message });
            }
            catch (TPayException ex)
            {
                _logger.LogError(ex, "TPay service error creating training payment");
                return StatusCode(502, new { error = "TPAY_ERROR", message = "TPay service temporarily unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating training payment");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred" });
            }
        }

        [HttpPost("marketplace")]
        public async Task<ActionResult<MarketplaceTransactionResponseDto>> CreateMarketplaceTransaction([FromBody] CreateMarketplaceTransactionDto marketplaceDto)
        {
            try
            {
                _logger.LogInformation("Creating marketplace transaction with {ItemCount} child transactions", 
                    marketplaceDto.ChildTransactions?.Count ?? 0);

                var result = await _paymentService.ProcessMarketplaceTransactionAsync(marketplaceDto);
                
                _logger.LogInformation("Marketplace transaction created successfully with ID {TransactionId}", 
                    result.TransactionId);

                return StatusCode(201, result);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning("Validation error creating marketplace transaction: {Error}", ex.Message);
                return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogError(ex, "Business rule error creating marketplace transaction");
                return BadRequest(new { error = "BUSINESS_ERROR", message = ex.Message });
            }
            catch (TPayException ex)
            {
                _logger.LogError(ex, "TPay service error creating marketplace transaction");
                return StatusCode(502, new { error = "TPAY_ERROR", message = "TPay service temporarily unavailable" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating marketplace transaction");
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred" });
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<PaymentDto>>> GetUserPayments(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                // This would require implementation in PaymentService and Repository
                // For now, return a placeholder
                _logger.LogDebug("Getting payment history for user {UserId}", userId);
                
                return StatusCode(501, new { 
                    message = "Payment history endpoint not implemented yet",
                    userId = userId,
                    page = page,
                    pageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment history for user {UserId}", userId);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve payment history" });
            }
        }

        [HttpGet("user/{userId}/pending")]
        public async Task<ActionResult<IEnumerable<PaymentDto>>> GetPendingPayments(Guid userId)
        {
            try
            {
                _logger.LogDebug("Getting pending payments for user {UserId}", userId);
                
                var pendingPayments = await _paymentService.GetPendingPaymentsForUserAsync(userId);
                
                return Ok(pendingPayments);
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogError(ex, "Business rule error getting pending payments for user {UserId}", userId);
                return BadRequest(new { error = "BUSINESS_RULE_ERROR", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending payments for user {UserId}", userId);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "Failed to retrieve pending payments" });
            }
        }

        [HttpPost("{paymentId:guid}/cancel")]
        public async Task<ActionResult<PaymentDto>> CancelPayment(Guid paymentId, [FromBody] CancelPaymentRequest request)
        {
            try
            {
                _logger.LogInformation("User {UserId} attempting to cancel payment {PaymentId}", request.UserId, paymentId);

                var cancelledPayment = await _paymentService.CancelPaymentAsync(paymentId, request.UserId);

                _logger.LogInformation("Payment {PaymentId} successfully cancelled by user {UserId}", paymentId, request.UserId);

                return Ok(cancelledPayment);
            }
            catch (NotFoundException ex)
            {
                _logger.LogWarning("Payment {PaymentId} not found for cancellation", paymentId);
                return NotFound(new { error = "PAYMENT_NOT_FOUND", message = ex.Message });
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning("Validation error cancelling payment {PaymentId}: {Error}", paymentId, ex.Message);
                return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
            }
            catch (BusinessRuleException ex)
            {
                _logger.LogError(ex, "Business rule error cancelling payment {PaymentId}", paymentId);
                return BadRequest(new { error = "BUSINESS_ERROR", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error cancelling payment {PaymentId}", paymentId);
                return StatusCode(500, new { error = "INTERNAL_ERROR", message = "An unexpected error occurred" });
            }
        }

        [HttpPost("test-push")]
        public async Task<IActionResult> TestPushNotification([FromBody] TestPushNotificationRequest request)
        {
            try
            {
                _logger.LogInformation("Testing push notification to token {Token}", request.PushToken);

                using var scope = _serviceProvider.CreateScope();
                var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

                var success = await pushNotificationService.SendCustomNotificationAsync(
                    request.PushToken,
                    request.Title ?? "Test Notification",
                    request.Body ?? "Testing push notification system"
                );

                if (success)
                {
                    _logger.LogInformation("Test push notification sent successfully");
                    return Ok(new { success = true, message = "Push notification sent successfully" });
                }
                else
                {
                    _logger.LogWarning("Test push notification failed to send");
                    return BadRequest(new { success = false, message = "Failed to send push notification" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test push notification");
                return StatusCode(500, new { success = false, error = "INTERNAL_ERROR", message = ex.Message });
            }
        }
    }
}

public class TestPushNotificationRequest
{
    public string PushToken { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Body { get; set; }
}
