using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using PlaySpace.Services.Interfaces;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Domain.DTOs;
using System.Text.Json;
using System.Threading.Channels;

namespace PlaySpace.Services.BackgroundServices
{
    public class AutoReservationService : BackgroundService
    {
        private readonly ILogger<AutoReservationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly Channel<Guid> _paymentQueue;
        private readonly ChannelWriter<Guid> _paymentWriter;

        public AutoReservationService(ILogger<AutoReservationService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // Create an unbounded channel for processing payments
            var channel = Channel.CreateUnbounded<Guid>();
            _paymentQueue = channel;
            _paymentWriter = channel.Writer;
        }

        public async Task QueuePaymentForProcessingAsync(Guid paymentId)
        {
            await _paymentWriter.WriteAsync(paymentId);
            _logger.LogInformation("Payment {PaymentId} queued for auto-reservation processing", paymentId);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AutoReservationService started");

            await foreach (var paymentId in _paymentQueue.Reader.ReadAllAsync(stoppingToken))
            {
                // Process each payment in its own task with proper scoping
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessPaymentForAutoReservationAsync(paymentId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process payment {PaymentId} for auto-reservation", paymentId);
                    }
                }, stoppingToken);
            }
        }

        private async Task ProcessPaymentForAutoReservationAsync(Guid paymentId)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var paymentRepository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
                var reservationRepository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
                var pendingReservationRepository = scope.ServiceProvider.GetRequiredService<IPendingTimeSlotReservationRepository>();
                var pushNotificationService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                var productPurchaseService = scope.ServiceProvider.GetRequiredService<IProductPurchaseService>();

                await ProcessPaymentWithScopedServicesAsync(paymentId, paymentRepository, reservationService, reservationRepository, pendingReservationRepository, pushNotificationService, productPurchaseService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process auto-reservation for payment {PaymentId}", paymentId);
            }
        }

        private async Task ProcessPaymentWithScopedServicesAsync(
            Guid paymentId,
            IPaymentRepository paymentRepository,
            IReservationService reservationService,
            IReservationRepository reservationRepository,
            IPendingTimeSlotReservationRepository pendingReservationRepository,
            IPushNotificationService pushNotificationService,
            IProductPurchaseService productPurchaseService)
        {
            try
            {
                _logger.LogInformation("Processing payment {PaymentId} for auto-reservation", paymentId);

                var payment = await paymentRepository.GetPaymentByIdAsync(paymentId);
                if (payment == null)
                {
                    _logger.LogWarning("Payment {PaymentId} not found", paymentId);
                    return;
                }

                if (payment.Status != "COMPLETED" || payment.IsConsumed)
                {
                    _logger.LogWarning("Payment {PaymentId} not eligible for auto-reservation. Status: {Status}, IsConsumed: {IsConsumed}",
                        paymentId, payment.Status, payment.IsConsumed);
                    return;
                }

                // Check for product purchase
                if (!string.IsNullOrEmpty(payment.ProductDetails) && string.IsNullOrEmpty(payment.ReservationDetails))
                {
                    _logger.LogInformation("Processing product purchase for payment {PaymentId}", paymentId);

                    var purchase = await productPurchaseService.CreatePurchaseFromPaymentAsync(paymentId);

                    _logger.LogInformation("Product purchase {PurchaseId} created for payment {PaymentId}",
                        purchase.PurchaseId, paymentId);

                    // Send product purchase notification
                    if (!string.IsNullOrEmpty(payment.PushToken))
                    {
                        await pushNotificationService.SendCustomNotificationAsync(
                            payment.PushToken,
                            "Zakup produktu zakończony",
                            "Produkt został pomyślnie zakupiony",
                            new Dictionary<string, string>
                            {
                                ["type"] = "PRODUCT_PURCHASE_COMPLETED",
                                ["purchaseId"] = purchase.PurchaseId,
                                ["paymentId"] = paymentId.ToString()
                            });
                        _logger.LogInformation("Product purchase notification sent for payment {PaymentId}", paymentId);
                    }

                    return;
                }

                // Check if we have reservation details
                var reservationDetailsList = new List<FacilityReservationDto>();

                if (!string.IsNullOrEmpty(payment.ReservationDetails))
                {
                    var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    // Try deserialize as list first (new format), fall back to single object (backward compat)
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<FacilityReservationDto>>(payment.ReservationDetails, jsonOptions);
                        if (list != null && list.Count > 0)
                            reservationDetailsList = list;
                    }
                    catch (JsonException)
                    {
                        // Backward compatibility: try single object
                        var single = JsonSerializer.Deserialize<FacilityReservationDto>(payment.ReservationDetails, jsonOptions);
                        if (single != null)
                            reservationDetailsList.Add(single);
                    }
                }

                // Fallback: try to get reservation details from PendingTimeSlotReservation if not in payment
                if (reservationDetailsList.Count == 0)
                {
                    _logger.LogWarning("Payment {PaymentId} has no reservation details in payload - attempting fallback from pending reservation", paymentId);

                    var pendingReservation = await pendingReservationRepository.GetPendingReservationByPaymentIdAsync(payment.Id);
                    if (pendingReservation != null)
                    {
                        _logger.LogInformation("Found pending reservation for payment {PaymentId} - using as fallback", paymentId);
                        reservationDetailsList.Add(new FacilityReservationDto
                        {
                            FacilityId = pendingReservation.FacilityId,
                            Date = pendingReservation.Date,
                            TimeSlots = pendingReservation.TimeSlots,
                            TrainerProfileId = pendingReservation.TrainerProfileId,
                            NumberOfUsers = pendingReservation.NumberOfUsers,
                            PayForAllUsers = pendingReservation.PayForAllUsers
                        });
                    }
                }

                // If still no reservation details, send manual reservation notification
                if (reservationDetailsList.Count == 0)
                {
                    _logger.LogInformation("Payment {PaymentId} has no reservation details and no pending reservation found - sending manual reservation notification", paymentId);

                    // Send notification for manual reservation
                    if (!string.IsNullOrEmpty(payment.PushToken))
                    {
                        await pushNotificationService.SendPaymentCompletedAsync(payment.PushToken, payment.Id);
                        _logger.LogInformation("Manual reservation notification sent for payment {PaymentId}", paymentId);
                    }
                    return;
                }

                _logger.LogInformation("Deserialized {Count} reservation(s) for payment {PaymentId}", reservationDetailsList.Count, paymentId);
                foreach (var rd in reservationDetailsList)
                {
                    _logger.LogInformation("  FacilityId={FacilityId}, Date={Date}, Slots={Slots}, NumberOfUsers={NumberOfUsers}, PayForAllUsers={PayForAllUsers}",
                        rd.FacilityId, rd.Date, string.Join(",", rd.TimeSlots), rd.NumberOfUsers, rd.PayForAllUsers);
                }

                // Check if a reservation already exists for this payment (handles duplicate webhook calls)
                var existingReservation = reservationRepository.GetReservationByPaymentId(payment.Id);
                if (existingReservation != null)
                {
                    _logger.LogWarning("Reservation already exists for payment {PaymentId}, skipping duplicate creation", paymentId);

                    // Ensure payment is marked as consumed
                    if (!payment.IsConsumed)
                    {
                        payment.IsConsumed = true;
                        await paymentRepository.UpdatePaymentAsync(payment);
                    }
                    return;
                }

                // Create group reservation with all facilities
                var groupReservationDto = new CreateGroupReservationDto
                {
                    PaymentId = payment.Id,
                    FacilityReservations = reservationDetailsList
                };

                var groupReservation = await reservationService.CreateGroupReservationAsync(groupReservationDto, payment.UserId);

                _logger.LogInformation("Auto-reservation created successfully for payment {PaymentId}, reservation group {GroupId}",
                    payment.Id, groupReservation.GroupId);

                // Mark payment as consumed to prevent duplicate reservation attempts
                payment.IsConsumed = true;
                await paymentRepository.UpdatePaymentAsync(payment);

                // Send success notification with reservation details
                if (!string.IsNullOrEmpty(payment.PushToken) && groupReservation.Reservations.Any())
                {
                    var firstReservation = groupReservation.Reservations.First();
                    await pushNotificationService.SendPaymentCompletedAsync(payment.PushToken, payment.Id, firstReservation.Id);
                    _logger.LogInformation("Success notification sent for payment {PaymentId} with reservation {ReservationId}",
                        payment.Id, firstReservation.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process auto-reservation for payment {PaymentId}", paymentId);

                try
                {
                    // Reload payment to get current state with a new scope
                    var payment = await paymentRepository.GetPaymentByIdAsync(paymentId);
                    if (payment != null)
                    {
                        await SendFailureNotificationAsync(pushNotificationService, payment, ex.Message);
                    }
                }
                catch (Exception notificationEx)
                {
                    _logger.LogError(notificationEx, "Failed to send failure notification for payment {PaymentId}", paymentId);
                }
            }
        }

        private async Task SendFailureNotificationAsync(IPushNotificationService pushNotificationService, Domain.Models.Payment payment, string error)
        {
            if (string.IsNullOrEmpty(payment.PushToken))
                return;

            try
            {
                await pushNotificationService.SendCustomNotificationAsync(
                    payment.PushToken,
                    "rezerwacja nie powiodła się.",
                    "Proszę spróbować ponownie lub skontaktować się z obiektem.",
                    new Dictionary<string, string>
                    {
                        ["type"] = "AUTO_RESERVATION_FAILED",
                        ["paymentId"] = payment.Id.ToString(),
                        ["error"] = error
                    });

                _logger.LogInformation("Failure notification sent for payment {PaymentId}", payment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send failure notification for payment {PaymentId}", payment.Id);
            }
        }

        public override void Dispose()
        {
            _paymentWriter.Complete();
            base.Dispose();
        }
    }
}