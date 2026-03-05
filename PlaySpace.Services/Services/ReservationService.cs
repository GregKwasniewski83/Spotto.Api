using PlaySpace.Domain.DTOs;
using PlaySpace.Domain.Exceptions;
using PlaySpace.Domain.Models;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PlaySpace.Services.Services;

public class ReservationService : IReservationService
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITrainerProfileRepository _trainerProfileRepository;
    private readonly IBusinessProfileRepository _businessProfileRepository;
    private readonly IPaymentService _paymentService;
    private readonly IFacilityRepository _facilityRepository;
    private readonly IPendingTimeSlotReservationRepository _pendingReservationRepository;
    private readonly ITrainingRepository _trainingRepository;
    private readonly IGlobalSettingsService _settingsService;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _userRepository;
    private readonly IBusinessProfileAgentRepository _businessProfileAgentRepository;
    private readonly IProductPurchaseRepository _productPurchaseRepository;
    private readonly ITrainerBusinessAssociationRepository _associationRepository;
    private readonly IBusinessParentChildAssociationRepository _parentChildAssociationRepository;
    private readonly ILogger<ReservationService> _logger;

    public ReservationService(IReservationRepository reservationRepository, ITrainerProfileRepository trainerProfileRepository, IBusinessProfileRepository businessProfileRepository, IPaymentService paymentService, IFacilityRepository facilityRepository, IPendingTimeSlotReservationRepository pendingReservationRepository, ITrainingRepository trainingRepository, IGlobalSettingsService settingsService, IConfiguration configuration, IEmailService emailService, IUserRepository userRepository, IBusinessProfileAgentRepository businessProfileAgentRepository, IProductPurchaseRepository productPurchaseRepository, ITrainerBusinessAssociationRepository associationRepository, IBusinessParentChildAssociationRepository parentChildAssociationRepository, ILogger<ReservationService> logger)
    {
        _reservationRepository = reservationRepository;
        _trainerProfileRepository = trainerProfileRepository;
        _businessProfileRepository = businessProfileRepository;
        _paymentService = paymentService;
        _facilityRepository = facilityRepository;
        _pendingReservationRepository = pendingReservationRepository;
        _trainingRepository = trainingRepository;
        _settingsService = settingsService;
        _configuration = configuration;
        _emailService = emailService;
        _userRepository = userRepository;
        _businessProfileAgentRepository = businessProfileAgentRepository;
        _productPurchaseRepository = productPurchaseRepository;
        _associationRepository = associationRepository;
        _parentChildAssociationRepository = parentChildAssociationRepository;
        _logger = logger;
    }

    public async Task<ReservationDto> CreateReservationAsync(CreateReservationDto reservationDto, Guid userId)
    {
        // Validate payment exists and is completed
        var payment = await _paymentService.GetPaymentByIdAsync((Guid)reservationDto.PaymentId);
        if (payment == null)
            throw new NotFoundException("Payment", reservationDto.PaymentId.ToString());

        if (payment.Status != "COMPLETED")
            throw new BusinessRuleException("Payment is not completed");

        if (payment.IsConsumed)
            throw new BusinessRuleException("Payment has already been used");

        // Check if a reservation already exists with this PaymentId (but allow group reservations)
        var existingReservation = _reservationRepository.GetReservationByPaymentId((Guid)reservationDto.PaymentId);
        if (existingReservation != null && existingReservation.GroupId == null)
            throw new ConflictException("A reservation already exists for this payment");

        // Validate payment amount matches reservation cost
        await ValidatePaymentAmount(payment, reservationDto);

        var reservation = _reservationRepository.CreateReservation(reservationDto, userId);
        
        // Remove any pending reservation for this user/facility/date since reservation was created successfully
        await _pendingReservationRepository.RemoveUserPendingReservationAsync(reservationDto.FacilityId, reservationDto.Date, userId);
        
        // Mark payment as consumed to prevent reuse
        // Note: You'll need to add this method to IPaymentService
        // await _paymentService.MarkPaymentAsConsumedAsync(reservationDto.PaymentId);
        
        // Send reservation confirmation email to customer
        try
        {
            var user = _userRepository.GetUser(userId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                var reservationDtos = MapToDto(reservation);
                var customerName = $"{user.FirstName} {user.LastName}".Trim();
                await _emailService.SendReservationCreatedEmailAsync(reservationDtos, user.Email, customerName);
            }
        }
        catch (Exception emailEx)
        {
            // Log email error but don't fail the reservation creation
            System.Diagnostics.Debug.WriteLine($"Failed to send reservation confirmation email: {emailEx.Message}");
        }

        // Send notification to business (child and parent)
        await SendBusinessReservationNotificationsAsync(reservation);

        return MapToDto(reservation);
    }

    public ReservationDto CreateReservation(CreateReservationDto reservationDto, Guid userId)
    {
        // Sync wrapper that calls the async version
        return CreateReservationAsync(reservationDto, userId).Result;
    }

    public ReservationDto? GetReservation(Guid id)
    {
        var reservation = _reservationRepository.GetReservation(id);
        return reservation == null ? null : MapToDto(reservation);
    }

    public List<ReservationDto> GetUserReservations(Guid userId)
    {
        var reservations = _reservationRepository.GetUserReservations(userId);
        return reservations.Select(MapToDto).ToList();
    }

    public bool CancelReservation(Guid reservationId, Guid userId)
    {
        return _reservationRepository.CancelReservation(reservationId, userId);
    }

    public async Task<ReservationDto> CancelReservationWithRefundAsync(Guid reservationId, Guid userId)
    {
        // Get the reservation
        var reservation = _reservationRepository.GetReservation(reservationId);
        if (reservation == null)
            throw new NotFoundException("Reservation", reservationId.ToString());

        // Verify the reservation belongs to the requesting user
        if (reservation.UserId != userId)
            throw new ForbiddenException("You can only cancel your own reservations");

        // Check if reservation is active or partial (partially cancelled)
        if (reservation.Status != "Active" && reservation.Status != "Partial")
            throw new BusinessRuleException($"Cannot cancel reservation with status {reservation.Status}");

        // Check if cancellation is at least 48 hours before the reservation
        var earliestSlot = reservation.TimeSlots?.OrderBy(t => t).FirstOrDefault() ?? "00:00";
        var slotTime = TimeSpan.TryParse(earliestSlot, out var ts) ? ts : TimeSpan.Zero;
        var reservationDateTime = reservation.Date.Add(slotTime);
        var hoursUntilReservation = (reservationDateTime - DateTime.UtcNow).TotalHours;

        if (hoursUntilReservation < 48)
            throw new BusinessRuleException("Reservations can only be cancelled at least 48 hours before the scheduled time");

        try
        {
            // Cancel the reservation first
            var cancelled = _reservationRepository.CancelReservation(reservationId, userId);
            if (!cancelled)
                throw new BusinessRuleException("Failed to cancel reservation");

            // Only process refund if reservation has a payment (regular reservations)
            // Admin/agent created reservations without payment will just be cancelled
            if (reservation.PaymentId.HasValue)
            {
                // Get the associated payment and process refund
                var payment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
                if (payment == null)
                    throw new NotFoundException("Payment", reservation.PaymentId.Value.ToString());

                // Only refund if payment was completed and not already refunded
                if (payment.Status == "COMPLETED" && !payment.IsRefunded)
                {
                    // Check if refunds are enabled system-wide
                    var refundSettings = await _settingsService.GetRefundSettingsAsync();
                    if (!refundSettings.EnableRefunds)
                    {
                        // Send email to user with business contact details
                        try
                        {
                            var user = _userRepository.GetUser(userId);
                            var facility = _facilityRepository.GetFacility(reservation.FacilityId);

                            if (user != null && facility != null && !string.IsNullOrEmpty(user.Email))
                            {
                                var businessProfile = _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);
                                if (businessProfile != null)
                                {
                                    var reservationDto = MapToDto(reservation);
                                    var businessDto = MapBusinessToDto(businessProfile);
                                    var userName = $"{user.FirstName} {user.LastName}".Trim();

                                    await _emailService.SendReservationCancelledNoRefundEmailAsync(
                                        reservationDto,
                                        user.Email,
                                        userName,
                                        businessDto
                                    );
                                }
                            }
                        }
                        catch (Exception emailEx)
                        {
                            // Log email error but don't fail the cancellation
                            // Email sending is not critical to the cancellation process
                            System.Diagnostics.Debug.WriteLine($"Failed to send cancellation email: {emailEx.Message}");
                        }

                        // Refunds are disabled - reservation is cancelled but no refund will be processed
                        // Email has been sent to user with business contact details
                        // Don't throw exception - just skip refund processing and return cancelled reservation
                    }
                    else
                    {
                        // Check if reservation is within refund time limit
                        var daysUntilReservation = (reservation.Date - DateTime.UtcNow.Date).Days;
                        if (daysUntilReservation > refundSettings.MaxRefundDaysAdvance)
                            throw new BusinessRuleException($"Refunds are only allowed up to {refundSettings.MaxRefundDaysAdvance} days in advance");

                        // Calculate refund amount using the configured fee percentage
                        var refundAmount = await _settingsService.CalculateRefundAmountAsync(payment.Amount);
                        var refundFee = payment.Amount - refundAmount;

                        // Process the refund
                        await _paymentService.RefundPaymentAsync(reservation.PaymentId.Value, refundAmount, reservation.FacilityId);

                        // Send cancellation email with refund details
                        try
                        {
                            var user = _userRepository.GetUser(userId);
                            if (user != null && !string.IsNullOrEmpty(user.Email))
                            {
                                var reservationDto = MapToDto(reservation);
                                var userName = $"{user.FirstName} {user.LastName}".Trim();
                                await _emailService.SendReservationCancelledWithRefundEmailAsync(
                                    reservationDto,
                                    user.Email,
                                    userName,
                                    refundAmount,
                                    refundFee
                                );
                            }
                        }
                        catch (Exception emailEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to send cancellation with refund email: {emailEx.Message}");
                        }
                    }
                }
            }

            // Return the updated reservation
            var updatedReservation = _reservationRepository.GetReservation(reservationId);
            return MapToDto(updatedReservation);
        }
        catch (Exception)
        {
            // If refund fails, we might need to revert the cancellation
            // This would require more sophisticated transaction management
            throw;
        }
    }

    public async Task<ReservationDto> CancelReservationByAgentAsync(Guid reservationId, Guid agentUserId, AgentCancelReservationDto cancelDto)
    {
        // Get the reservation
        var reservation = _reservationRepository.GetReservation(reservationId);
        if (reservation == null)
            throw new NotFoundException("Reservation", reservationId.ToString());

        // Get the facility to find its owner (business profile)
        var facility = _facilityRepository.GetFacility(reservation.FacilityId);
        if (facility == null)
            throw new NotFoundException("Facility", reservation.FacilityId.ToString());

        // Verify the user is either the facility owner OR an authorized agent
        var isFacilityOwner = facility.UserId == agentUserId;
        var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
            await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(agentUserId, facility.BusinessProfileId.Value);

        if (!isFacilityOwner && !isAuthorizedAgent)
            throw new ForbiddenException("You are not authorized to cancel reservations for this facility");

        // Check if reservation is active or partial (partially cancelled)
        if (reservation.Status != "Active" && reservation.Status != "Partial")
            throw new BusinessRuleException($"Cannot cancel reservation with status {reservation.Status}");

        // Check if cancellation is at least 48 hours before the reservation
        var earliestSlot = reservation.TimeSlots?.OrderBy(t => t).FirstOrDefault() ?? "00:00";
        var slotTime = TimeSpan.TryParse(earliestSlot, out var ts) ? ts : TimeSpan.Zero;
        var reservationDateTime = reservation.Date.Add(slotTime);
        var hoursUntilReservation = (reservationDateTime - DateTime.UtcNow).TotalHours;

        if (hoursUntilReservation < 48)
            throw new BusinessRuleException("Reservations can only be cancelled at least 48 hours before the scheduled time");

        // Get agent's user info for the name
        var agent = _userRepository.GetUser(agentUserId);
        var agentName = agent != null ? $"{agent.FirstName} {agent.LastName}".Trim() : null;

        try
        {
            // Cancel the reservation with agent metadata
            var cancelled = _reservationRepository.CancelReservationByAgent(
                reservationId,
                agentUserId,
                agentName,
                cancelDto.Notes
            );

            if (!cancelled)
                throw new BusinessRuleException("Failed to cancel reservation");

            // Process refund if reservation has a payment (same logic as user cancellation)
            if (reservation.PaymentId.HasValue)
            {
                var payment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
                if (payment == null)
                    throw new NotFoundException("Payment", reservation.PaymentId.Value.ToString());

                // Only refund if payment was completed and not already refunded
                if (payment.Status == "COMPLETED" && !payment.IsRefunded)
                {
                    // Determine customer email and name (guest email takes priority)
                    string? customerEmail = null;
                    string? customerName = null;

                    if (!string.IsNullOrEmpty(reservation.GuestEmail))
                    {
                        customerEmail = reservation.GuestEmail;
                        customerName = reservation.GuestName ?? "Gość";
                    }
                    else if (reservation.UserId.HasValue)
                    {
                        var user = _userRepository.GetUser(reservation.UserId.Value);
                        if (user != null && !string.IsNullOrEmpty(user.Email))
                        {
                            customerEmail = user.Email;
                            customerName = $"{user.FirstName} {user.LastName}".Trim();
                        }
                    }

                    // Check if refunds are enabled system-wide
                    var refundSettings = await _settingsService.GetRefundSettingsAsync();
                    if (!refundSettings.EnableRefunds)
                    {
                        // Send email to customer with business contact details
                        try
                        {
                            if (!string.IsNullOrEmpty(customerEmail))
                            {
                                var businessProfile = _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);
                                if (businessProfile != null)
                                {
                                    var updatedReservation = _reservationRepository.GetReservation(reservationId);
                                    var reservationDto = MapToDto(updatedReservation);
                                    var businessDto = MapBusinessToDto(businessProfile);

                                    await _emailService.SendReservationCancelledNoRefundEmailAsync(
                                        reservationDto,
                                        customerEmail,
                                        customerName ?? "Kliencie",
                                        businessDto
                                    );
                                }
                            }
                        }
                        catch (Exception emailEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to send cancellation email: {emailEx.Message}");
                        }

                        throw new BusinessRuleException("Cancelled. Refunds are currently disabled");
                    }

                    // Check if reservation is within refund time limit
                    var daysUntilReservation = (reservation.Date - DateTime.UtcNow.Date).Days;
                    if (daysUntilReservation > refundSettings.MaxRefundDaysAdvance)
                        throw new BusinessRuleException($"Refunds are only allowed up to {refundSettings.MaxRefundDaysAdvance} days in advance");

                    // Calculate refund amount using the configured fee percentage
                    var refundAmount = await _settingsService.CalculateRefundAmountAsync(payment.Amount);
                    var refundFee = payment.Amount - refundAmount;

                    // Process the refund
                    await _paymentService.RefundPaymentAsync(reservation.PaymentId.Value, refundAmount, reservation.FacilityId);

                    // Send cancellation email with refund details
                    try
                    {
                        if (!string.IsNullOrEmpty(customerEmail))
                        {
                            var reservationDto = MapToDto(reservation);
                            await _emailService.SendReservationCancelledWithRefundEmailAsync(
                                reservationDto,
                                customerEmail,
                                customerName ?? "Kliencie",
                                refundAmount,
                                refundFee
                            );
                        }
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to send cancellation with refund email: {emailEx.Message}");
                    }
                }
            }

            // Return the updated reservation with cancellation metadata
            var finalReservation = _reservationRepository.GetReservation(reservationId);
            return MapToDto(finalReservation);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> IsTimeSlotAvailableAsync(List<string> timeSlots, Guid facilityId, DateTime date, Guid? excludeUserId = null)
    {
        return await _reservationRepository.IsTimeSlotAvailableAsync(timeSlots, facilityId, date, excludeUserId);
    }

    public async Task<bool> IsTrainerAvailableAsync(Guid trainerProfileId, List<string> timeSlots, DateTime date)
    {
        var availableTrainers = _trainerProfileRepository.FindAvailableTrainers(date, timeSlots);
        return availableTrainers.Any(t => t.trainer.Id == trainerProfileId && 
                                         timeSlots.All(slot => t.availableSlots.Contains(slot)));
    }

    public ReservationDto? UpdateReservation(Guid reservationId, UpdateReservationDto updateDto, Guid userId)
    {
        var updatedReservation = _reservationRepository.UpdateReservation(reservationId, updateDto, userId);
        return updatedReservation == null ? null : MapToDto(updatedReservation);
    }

    private ReservationDto MapToDto(Reservation reservation)
    {
        string? businessAvatarUrl = null;
        string? trainerAvatarUrl = null;

        // Get business avatar
        if (reservation.Facility != null)
        {
            var businessProfile = _businessProfileRepository.GetBusinessProfileByUserId(reservation.Facility.UserId);
            if (!string.IsNullOrEmpty(businessProfile?.AvatarUrl))
            {
                businessAvatarUrl = businessProfile.AvatarUrl;
            }
            else
            {
                // Get default avatar based on facility type
                businessAvatarUrl = GetDefaultBusinessAvatar(reservation.Facility.Type);
            }
        }

        // Get trainer avatar
        if (reservation.TrainerProfile != null)
        {
            if (!string.IsNullOrEmpty(reservation.TrainerProfile.AvatarUrl))
            {
                trainerAvatarUrl = reservation.TrainerProfile.AvatarUrl;
            }
            else
            {
                trainerAvatarUrl = _configuration["DefaultAvatars:TrainerAvatar"];
            }
        }

        // Get creator info if this was created by admin/agent
        string? createdByName = null;
        if (reservation.CreatedById.HasValue && reservation.CreatedBy != null)
        {
            createdByName = $"{reservation.CreatedBy.FirstName} {reservation.CreatedBy.LastName}".Trim();
        }

        return new ReservationDto
        {
            Id = reservation.Id,
            GroupId = reservation.GroupId,
            FacilityId = reservation.FacilityId,
            UserId = reservation.UserId,
            Date = reservation.Date,
            TimeSlots = reservation.TimeSlots,
            TotalPrice = reservation.TotalPrice,
            RemainingPrice = reservation.RemainingPrice,
            Status = reservation.Status,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.UpdatedAt,
            FacilityName = reservation.Facility?.Name,
            BusinessProfileName = reservation.Facility?.BusinessProfile?.DisplayName,
            BusinessEmail = reservation.Facility?.BusinessProfile?.Email,
            BusinessPhoneNumber = reservation.Facility?.BusinessProfile?.PhoneNumber,
            TrainerProfileId = reservation.TrainerProfileId,
            TrainerPrice = reservation.TrainerPrice,
            TrainerDisplayName = reservation.TrainerProfile?.DisplayName,
            TrainerEmail = reservation.TrainerProfile?.Email,
            TrainerPhoneNumber = reservation.TrainerProfile?.PhoneNumber,
            BusinessAvatarUrl = businessAvatarUrl,
            TrainerAvatarUrl = trainerAvatarUrl,
            // Fix for CS0266 and CS8629: Explicitly cast nullable Guid to Guid, and handle possible null value.
            // Replace all instances like this:
            // PaymentId = reservation.PaymentId,
            PaymentId = reservation.PaymentId,
            ProductPurchaseId = reservation.ProductPurchaseId,
            TrainingId = null, // Will be populated in GetUserUpcomingReservations
            Notes = reservation.Notes,
            GuestName = reservation.GuestName,
            GuestPhone = reservation.GuestPhone,
            GuestEmail = reservation.GuestEmail,
            CreatedById = reservation.CreatedById,
            CreatedByName = createdByName,
            CancelledById = reservation.CancelledById,
            CancelledByName = reservation.CancelledByName,
            CancelledAt = reservation.CancelledAt,
            CancellationNotes = reservation.CancellationNotes,
            Slots = reservation.Slots?.Select(s => new SlotDetailDto
            {
                Id = s.Id,
                TimeSlot = s.TimeSlot,
                SlotPrice = s.SlotPrice,
                Status = s.Status,
                CancelledAt = s.CancelledAt,
                CancellationReason = s.CancellationReason
            }).ToList(),
            // Payment status - reservation is unpaid if no PaymentId and no ProductPurchaseId
            IsUnpaid = !reservation.PaymentId.HasValue && !reservation.ProductPurchaseId.HasValue,
            // User can pay online if: unpaid, active status, future date, and has a user assigned
            CanPayOnline = !reservation.PaymentId.HasValue &&
                          !reservation.ProductPurchaseId.HasValue &&
                          reservation.Status == "Active" &&
                          reservation.Date >= DateTime.UtcNow.Date &&
                          reservation.UserId.HasValue,
            // Multi-user booking info
            NumberOfUsers = reservation.NumberOfUsers,
            PaidForAllUsers = reservation.PaidForAllUsers
        };
    }

    private async Task ValidatePaymentAmount(PaymentDto payment, CreateReservationDto reservationDto)
    {
        // Calculate expected cost (use gross price for payment validation)
        var facility = _facilityRepository.GetFacility(reservationDto.FacilityId);
        if (facility == null)
            throw new NotFoundException("Facility", reservationDto.FacilityId.ToString());

        var basePricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;

        if (reservationDto.TrainerProfileId.HasValue)
        {
            // Get business profile directly from facility link
            BusinessProfile? businessProfile = facility.BusinessProfileId.HasValue
                ? _businessProfileRepository.GetBusinessProfileById(facility.BusinessProfileId.Value)
                : _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);

            if (businessProfile != null)
            {
                // Try to get association-specific pricing
                var association = await _associationRepository.GetByTrainerAndBusinessAsync(
                    reservationDto.TrainerProfileId.Value,
                    businessProfile.Id);

                if (association != null && association.Status == AssociationStatus.Confirmed && association.GrossHourlyRate.HasValue)
                {
                    // Use business-specific gross hourly rate (includes VAT)
                    basePricePerSlot += association.GrossHourlyRate.Value;
                }
                else
                {
                    // Fall back to trainer's own hourly rate
                    var trainer = _trainerProfileRepository.GetTrainerProfileById(reservationDto.TrainerProfileId.Value);
                    if (trainer == null)
                        throw new NotFoundException("Trainer profile", reservationDto.TrainerProfileId.Value.ToString());
                    basePricePerSlot += trainer.HourlyRate;
                }
            }
            else
            {
                // No business profile, fall back to trainer's own hourly rate
                var trainer = _trainerProfileRepository.GetTrainerProfileById(reservationDto.TrainerProfileId.Value);
                if (trainer == null)
                    throw new NotFoundException("Trainer profile", reservationDto.TrainerProfileId.Value.ToString());
                basePricePerSlot += trainer.HourlyRate;
            }
        }

        // Apply per-user pricing if applicable
        var numberOfUsers = reservationDto.NumberOfUsers > 0 ? reservationDto.NumberOfUsers : 1;
        var pricePerSlot = basePricePerSlot;

        if (facility.PricePerUser && reservationDto.PayForAllUsers)
        {
            pricePerSlot = basePricePerSlot * numberOfUsers;
        }

        var expectedCost = pricePerSlot * reservationDto.TimeSlots.Count;

        // Validate payment amount matches expected cost (round to 2 decimal places to avoid floating-point precision issues)
        if (Math.Round(payment.Amount, 2) != Math.Round(expectedCost, 2))
        {
            throw new ValidationException($"Payment amount ({payment.Amount}) does not match reservation cost ({expectedCost})");
        }
    }

    private string? GetDefaultBusinessAvatar(string facilityType)
    {
        return facilityType.ToLower() switch
        {
            "tennis" => _configuration["DefaultAvatars:TennisBusinessAvatar"],
            "shooting range" => _configuration["DefaultAvatars:ShootingRangeBusinessAvatar"],
            _ => null
        };
    }
    
    public int GetTotalReservationsCount()
    {
        return _reservationRepository.GetTotalCount();
    }

    public List<ReservationDto> GetUserUpcomingReservations(Guid userId)
    {
        var reservations = _reservationRepository.GetUserUpcomingReservations(userId);
        var reservationDtos = new List<ReservationDto>();

        foreach (var reservation in reservations)
        {
            // Find associated training if exists
            var training = _trainingRepository.SearchTrainings(new TrainingSearchDto())
                .FirstOrDefault(t => t.ReservationId == reservation.Id);

            // Get business avatar
            string? businessAvatarUrl = null;
            if (reservation.Facility != null)
            {
                var businessProfile = _businessProfileRepository.GetBusinessProfileByUserId(reservation.Facility.UserId);
                if (!string.IsNullOrEmpty(businessProfile?.AvatarUrl))
                {
                    businessAvatarUrl = businessProfile.AvatarUrl;
                }
                else
                {
                    businessAvatarUrl = GetDefaultBusinessAvatar(reservation.Facility.Type);
                }
            }

            // Get trainer avatar
            string? trainerAvatarUrl = null;
            if (reservation.TrainerProfile != null)
            {
                if (!string.IsNullOrEmpty(reservation.TrainerProfile.AvatarUrl))
                {
                    trainerAvatarUrl = reservation.TrainerProfile.AvatarUrl;
                }
                else
                {
                    trainerAvatarUrl = _configuration["DefaultAvatars:TrainerAvatar"];
                }
            }

            var reservationDto = new ReservationDto
            {
                Id = reservation.Id,
                GroupId = reservation.GroupId,
                FacilityId = reservation.FacilityId,
                UserId = reservation.UserId,
                Date = reservation.Date,
                TimeSlots = reservation.TimeSlots,
                TotalPrice = reservation.TotalPrice,
                Status = reservation.Status,
                CreatedAt = reservation.CreatedAt,
                UpdatedAt = reservation.UpdatedAt,
                FacilityName = reservation.Facility?.Name,
                BusinessProfileName = reservation.Facility?.BusinessProfile?.DisplayName,
                BusinessEmail = reservation.Facility?.BusinessProfile?.Email,
                BusinessPhoneNumber = reservation.Facility?.BusinessProfile?.PhoneNumber,
                TrainerProfileId = reservation.TrainerProfileId,
                TrainerPrice = reservation.TrainerPrice,
                TrainerDisplayName = reservation.TrainerProfile?.DisplayName,
                TrainerEmail = reservation.TrainerProfile?.Email,
                TrainerPhoneNumber = reservation.TrainerProfile?.PhoneNumber,
                BusinessAvatarUrl = businessAvatarUrl,
                TrainerAvatarUrl = trainerAvatarUrl,
                PaymentId = reservation.PaymentId,
                TrainingId = training?.Id,
                // Include guest information for agent-created reservations
                GuestName = reservation.GuestName,
                GuestPhone = reservation.GuestPhone,
                GuestEmail = reservation.GuestEmail,
                CreatedById = reservation.CreatedById,
                CreatedByName = reservation.CreatedBy != null ? $"{reservation.CreatedBy.FirstName} {reservation.CreatedBy.LastName}".Trim() : null
            };

            reservationDtos.Add(reservationDto);
        }

        return reservationDtos;
    }

    public async Task<GroupReservationResponseDto> CreateGroupReservationAsync(CreateGroupReservationDto groupReservationDto, Guid userId)
    {
        // Validate that either PaymentId or PurchaseId is provided (not both, not neither)
        if (!groupReservationDto.PaymentId.HasValue && !groupReservationDto.PurchaseId.HasValue)
            throw new ValidationException("Either PaymentId or PurchaseId must be provided");

        if (groupReservationDto.PaymentId.HasValue && groupReservationDto.PurchaseId.HasValue)
            throw new ValidationException("Cannot provide both PaymentId and PurchaseId");

        ProductPurchase? productPurchase = null;
        bool isUsingProductPurchase = groupReservationDto.PurchaseId.HasValue;

        // Validate payment or product purchase
        if (groupReservationDto.PaymentId.HasValue)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(groupReservationDto.PaymentId.Value);
            if (payment == null)
                throw new NotFoundException("Payment", groupReservationDto.PaymentId.ToString());

            // Check if a reservation already exists for this payment (handles duplicate calls / race conditions)
            var existingReservation = _reservationRepository.GetReservationByPaymentId(groupReservationDto.PaymentId.Value);
            if (existingReservation != null)
            {
                _logger.LogWarning("Reservation already exists for payment {PaymentId}, returning existing reservation", groupReservationDto.PaymentId.Value);

                // Return the existing group reservation
                if (existingReservation.GroupId.HasValue)
                {
                    var existingGroup = GetGroupReservation(existingReservation.GroupId.Value);
                    if (existingGroup != null)
                        return existingGroup;
                }

                // Fallback: return single reservation as group
                return new GroupReservationResponseDto
                {
                    GroupId = existingReservation.GroupId ?? existingReservation.Id,
                    Reservations = new List<ReservationDto> { MapToDto(existingReservation) },
                    TotalPrice = existingReservation.TotalPrice,
                    Status = existingReservation.Status,
                    CreatedAt = existingReservation.CreatedAt
                };
            }
        }
        else if (groupReservationDto.PurchaseId.HasValue)
        {
            productPurchase = await _productPurchaseRepository.GetByIdAsync(groupReservationDto.PurchaseId.Value);
            if (productPurchase == null)
                throw new NotFoundException("ProductPurchase", groupReservationDto.PurchaseId.ToString());

            if (productPurchase.UserId != userId)
                throw new UnauthorizedAccessException("This product purchase does not belong to you");

            if (productPurchase.Status != "active")
                throw new BusinessRuleException($"Product purchase is {productPurchase.Status}");

            if (productPurchase.ExpiryDate < DateTime.UtcNow)
                throw new BusinessRuleException("Product purchase has expired");

            // Validate facility restrictions
            if (!productPurchase.AppliesToAllFacilities)
            {
                var allowedFacilityIds = new HashSet<Guid>();
                if (!string.IsNullOrEmpty(productPurchase.FacilityIds))
                {
                    var facilityIdStrings = System.Text.Json.JsonSerializer.Deserialize<List<string>>(productPurchase.FacilityIds);
                    if (facilityIdStrings != null)
                    {
                        foreach (var idStr in facilityIdStrings)
                        {
                            if (Guid.TryParse(idStr, out var facilityGuid))
                                allowedFacilityIds.Add(facilityGuid);
                        }
                    }
                }

                var requestedFacilityIds = groupReservationDto.FacilityReservations
                    .Select(r => r.FacilityId)
                    .ToList();

                var invalidFacilities = requestedFacilityIds
                    .Where(id => !allowedFacilityIds.Contains(id))
                    .ToList();

                if (invalidFacilities.Any())
                {
                    throw new BusinessRuleException(
                        $"Product is not valid for the selected facilities. Invalid facility IDs: {string.Join(", ", invalidFacilities)}");
                }
            }
        }

        // Validate that all facilities belong to the same business
        var businessIds = new List<Guid>();
        decimal totalExpectedCost = 0;
        int totalSlotsNeeded = 0;

        foreach (var facilityReservation in groupReservationDto.FacilityReservations)
        {
            // Check facility availability
            var isAvailable = await _reservationRepository.IsTimeSlotAvailableAsync(
                facilityReservation.TimeSlots,
                facilityReservation.FacilityId,
                facilityReservation.Date,
                userId);

            if (!isAvailable)
                throw new ConflictException($"One or more time slots are not available for facility {facilityReservation.FacilityId}");

            // Check trainer availability if specified
            if (facilityReservation.TrainerProfileId.HasValue)
            {
                var isTrainerAvailable = await IsTrainerAvailableAsync(
                    facilityReservation.TrainerProfileId.Value,
                    facilityReservation.TimeSlots,
                    facilityReservation.Date);

                if (!isTrainerAvailable)
                    throw new ConflictException($"Trainer is not available for the selected time slots");
            }

            // Get facility and calculate cost
            var facility = _facilityRepository.GetFacility(facilityReservation.FacilityId);
            if (facility == null)
                throw new NotFoundException("Facility", facilityReservation.FacilityId.ToString());

            businessIds.Add(facility.UserId);

            // Calculate base price per slot (use gross price for payment validation)
            var basePricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;

            // Add trainer cost if applicable
            if (facilityReservation.TrainerProfileId.HasValue)
            {
                // Get business profile directly from facility link
                var businessProfile = facility.BusinessProfileId.HasValue
                    ? _businessProfileRepository.GetBusinessProfileById(facility.BusinessProfileId.Value)
                    : _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);

                if (businessProfile != null)
                {
                    // Try to get association-specific pricing
                    var association = await _associationRepository.GetByTrainerAndBusinessAsync(
                        facilityReservation.TrainerProfileId.Value,
                        businessProfile.Id);

                    if (association != null && association.Status == AssociationStatus.Confirmed && association.GrossHourlyRate.HasValue)
                    {
                        // Use business-specific gross hourly rate (includes VAT)
                        basePricePerSlot += association.GrossHourlyRate.Value;
                        _logger.LogInformation("Using association pricing for trainer {TrainerId} at business {BusinessId}: {GrossRate}/hour",
                            facilityReservation.TrainerProfileId.Value, businessProfile.Id, association.GrossHourlyRate.Value);
                    }
                    else
                    {
                        // Fall back to trainer's own hourly rate
                        var trainer = _trainerProfileRepository.GetTrainerProfileById(facilityReservation.TrainerProfileId.Value);
                        if (trainer != null)
                        {
                            basePricePerSlot += trainer.HourlyRate;
                            _logger.LogInformation("Using trainer profile pricing for trainer {TrainerId}: {Rate}/hour",
                                facilityReservation.TrainerProfileId.Value, trainer.HourlyRate);
                        }
                    }
                }
                else
                {
                    // No business profile, fall back to trainer's own hourly rate
                    var trainer = _trainerProfileRepository.GetTrainerProfileById(facilityReservation.TrainerProfileId.Value);
                    if (trainer != null)
                    {
                        basePricePerSlot += trainer.HourlyRate;
                    }
                }
            }

            // Apply per-user pricing if applicable
            var numberOfUsers = facilityReservation.NumberOfUsers > 0 ? facilityReservation.NumberOfUsers : 1;
            var pricePerSlot = basePricePerSlot;

            if (facility.PricePerUser && facilityReservation.PayForAllUsers)
            {
                pricePerSlot = basePricePerSlot * numberOfUsers;
            }

            var facilityCost = pricePerSlot * facilityReservation.TimeSlots.Count;
            totalExpectedCost += facilityCost;
            totalSlotsNeeded += facilityReservation.TimeSlots.Count * numberOfUsers;
        }

        // Ensure all facilities belong to the same business
        if (businessIds.Distinct().Count() > 1)
            throw new ArgumentException("All facilities must belong to the same business");

        // Validate payment amount or product usage
        if (isUsingProductPurchase && productPurchase != null)
        {
            // Validate sufficient usage remaining (total = time slots × participants for each facility)
            if (productPurchase.RemainingUsage < totalSlotsNeeded)
                throw new BusinessRuleException($"Insufficient product usage. Need {totalSlotsNeeded} entries (time slots × participants), but only {productPurchase.RemainingUsage} remaining");

            // Decrement product usage
            productPurchase.RemainingUsage -= totalSlotsNeeded;
            productPurchase.UpdatedAt = DateTime.UtcNow;

            // Update status if depleted
            if (productPurchase.RemainingUsage == 0)
                productPurchase.Status = "depleted";

            await _productPurchaseRepository.UpdateAsync(productPurchase);

            _logger.LogInformation("Product purchase {PurchaseId} used {SlotsUsed} entries (time slots × participants). Remaining: {RemainingUsage}",
                productPurchase.Id, totalSlotsNeeded, productPurchase.RemainingUsage);
        }
        else if (groupReservationDto.PaymentId.HasValue)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(groupReservationDto.PaymentId.Value);
            // Validate payment amount (round to 2 decimal places to avoid floating-point precision issues)
            if (Math.Round(payment.Amount, 2) != Math.Round(totalExpectedCost, 2))
                throw new ArgumentException($"Payment amount ({payment.Amount:C}) does not match total reservation cost ({totalExpectedCost:C})");
        }

        // Create group reservation
        var groupId = Guid.NewGuid();
        var reservationDtos = groupReservationDto.FacilityReservations.Select(fr => new CreateReservationDto
        {
            FacilityId = fr.FacilityId,
            Date = fr.Date,
            TimeSlots = fr.TimeSlots,
            TrainerProfileId = fr.TrainerProfileId,
            PaymentId = groupReservationDto.PaymentId,
            PurchaseId = groupReservationDto.PurchaseId,
            NumberOfUsers = fr.NumberOfUsers,
            PayForAllUsers = fr.PayForAllUsers
        }).ToList();

        var createdReservations = _reservationRepository.CreateGroupReservations(reservationDtos, userId, groupId);

        // Map to DTOs
        var reservationResponseDtos = createdReservations.Select(r => MapToDto(r)).ToList();

        // Send reservation confirmation email to customer for group reservation
        try
        {
            var user = _userRepository.GetUser(userId);
            if (user != null && !string.IsNullOrEmpty(user.Email))
            {
                var customerName = $"{user.FirstName} {user.LastName}".Trim();
                
                // Send email for the first reservation as a group confirmation
                var firstReservation = reservationResponseDtos.FirstOrDefault();
                if (firstReservation != null)
                {
                    await _emailService.SendReservationCreatedEmailAsync(firstReservation, user.Email, customerName);
                }
            }
        }
        catch (Exception emailEx)
        {
            // Log email error but don't fail the reservation creation
            System.Diagnostics.Debug.WriteLine($"Failed to send group reservation confirmation email: {emailEx.Message}");
        }

        // Send notification to business (child and parent) for each reservation in the group
        foreach (var res in createdReservations)
        {
            await SendBusinessReservationNotificationsAsync(res);
        }

        return new GroupReservationResponseDto
        {
            GroupId = groupId,
            Reservations = reservationResponseDtos,
            TotalPrice = totalExpectedCost,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
    }

    public GroupReservationResponseDto? GetGroupReservation(Guid groupId)
    {
        var reservations = _reservationRepository.GetGroupReservations(groupId);
        if (!reservations.Any())
            return null;

        var reservationDtos = reservations.Select(r => MapToDto(r)).ToList();
        var totalPrice = reservations.Sum(r => r.TotalPrice);

        return new GroupReservationResponseDto
        {
            GroupId = groupId,
            Reservations = reservationDtos,
            TotalPrice = totalPrice,
            Status = reservations.First().Status,
            CreatedAt = reservations.Min(r => r.CreatedAt)
        };
    }

    public bool CancelGroupReservation(Guid groupId, Guid userId)
    {
        return _reservationRepository.CancelGroupReservations(groupId, userId);
    }

    public async Task<ReservationDto> CreateAdminReservationAsync(AdminCreateReservationDto reservationDto, Guid createdById)
    {
        // Validate facility exists
        var facility = _facilityRepository.GetFacility(reservationDto.FacilityId);
        if (facility == null)
            throw new NotFoundException("Facility", reservationDto.FacilityId.ToString());

        // Verify the user is either the facility owner OR an authorized agent
        var isFacilityOwner = facility.UserId == createdById;
        var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
            await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(createdById, facility.BusinessProfileId.Value);

        if (!isFacilityOwner && !isAuthorizedAgent)
            throw new ForbiddenException("You are not authorized to create reservations for this facility");

        // Validate user exists (only if userId is provided - otherwise it's a guest reservation)
        if (reservationDto.UserId.HasValue)
        {
            var user = _userRepository.GetUser(reservationDto.UserId.Value);
            if (user == null)
                throw new NotFoundException("User", reservationDto.UserId.Value.ToString());
        }
        else
        {
            // For guest reservations, require at least guest name
            if (string.IsNullOrWhiteSpace(reservationDto.GuestName))
                throw new ArgumentException("GuestName is required for non-registered customers");
        }

        // Check time slot availability
        var isAvailable = await _reservationRepository.IsTimeSlotAvailableAsync(
            reservationDto.TimeSlots,
            reservationDto.FacilityId,
            reservationDto.Date,
            null); // Don't exclude any user for admin reservations

        if (!isAvailable)
            throw new ConflictException("One or more time slots are not available");

        // Validate trainer availability if specified
        if (reservationDto.TrainerProfileId.HasValue)
        {
            var isTrainerAvailable = await IsTrainerAvailableAsync(
                reservationDto.TrainerProfileId.Value,
                reservationDto.TimeSlots,
                reservationDto.Date);

            if (!isTrainerAvailable)
                throw new ConflictException("Trainer is not available for the selected time slots");
        }

        // Create reservation through repository
        var createdReservation = _reservationRepository.CreateAdminReservation(reservationDto, createdById);

        // Remove any pending reservation for this user/facility/date since reservation was created successfully
        // Only remove pending reservations if userId is provided (not for guest reservations)
        if (reservationDto.UserId.HasValue)
        {
            await _pendingReservationRepository.RemoveUserPendingReservationAsync(
                reservationDto.FacilityId,
                reservationDto.Date,
                reservationDto.UserId.Value);
        }

        // Send reservation confirmation email to customer (for both registered users and guests)
        try
        {
            string customerEmail = null;
            string customerName = null;
            
            if (reservationDto.UserId.HasValue)
            {
                // For registered users
                var user = _userRepository.GetUser(reservationDto.UserId.Value);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    customerEmail = user.Email;
                    customerName = $"{user.FirstName} {user.LastName}".Trim();
                }
            }
            else if (!string.IsNullOrEmpty(reservationDto.GuestEmail))
            {
                // For guest reservations
                customerEmail = reservationDto.GuestEmail;
                customerName = reservationDto.GuestName ?? "Gość";
            }
            
            if (!string.IsNullOrEmpty(customerEmail))
            {
                var reservationDtoMapped = MapToDto(createdReservation);
                await _emailService.SendReservationCreatedEmailAsync(reservationDtoMapped, customerEmail, customerName);
            }
        }
        catch (Exception emailEx)
        {
            // Log email error but don't fail the reservation creation
            System.Diagnostics.Debug.WriteLine($"Failed to send reservation confirmation email for admin reservation: {emailEx.Message}");
        }

        // Send notification to business (child and parent)
        await SendBusinessReservationNotificationsAsync(createdReservation);

        return MapToDto(createdReservation);
    }

    public async Task<GroupReservationResponseDto> CreateAdminGroupReservationAsync(CreateAdminGroupReservationDto groupDto, Guid createdById)
    {
        if (!groupDto.FacilityReservations.Any())
            throw new ArgumentException("At least one facility reservation is required");

        // Validate user exists (if userId provided) or guest info is present
        if (groupDto.UserId.HasValue)
        {
            var user = _userRepository.GetUser(groupDto.UserId.Value);
            if (user == null)
                throw new NotFoundException("User", groupDto.UserId.Value.ToString());
        }
        else
        {
            if (string.IsNullOrWhiteSpace(groupDto.GuestName))
                throw new ArgumentException("GuestName is required for non-registered customers");
        }

        // Validate authorization for all facilities and check availability
        decimal totalPrice = 0;
        foreach (var facilityReservation in groupDto.FacilityReservations)
        {
            var facility = _facilityRepository.GetFacility(facilityReservation.FacilityId);
            if (facility == null)
                throw new NotFoundException("Facility", facilityReservation.FacilityId.ToString());

            // Verify the user is either the facility owner OR an authorized agent
            var isFacilityOwner = facility.UserId == createdById;
            var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
                await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(createdById, facility.BusinessProfileId.Value);

            if (!isFacilityOwner && !isAuthorizedAgent)
                throw new ForbiddenException($"You are not authorized to create reservations for facility {facilityReservation.FacilityId}");

            // Check time slot availability
            var isAvailable = await _reservationRepository.IsTimeSlotAvailableAsync(
                facilityReservation.TimeSlots,
                facilityReservation.FacilityId,
                facilityReservation.Date,
                null);

            if (!isAvailable)
                throw new ConflictException($"One or more time slots are not available for facility {facilityReservation.FacilityId}");

            // Check trainer availability if specified
            if (facilityReservation.TrainerProfileId.HasValue)
            {
                var isTrainerAvailable = await IsTrainerAvailableAsync(
                    facilityReservation.TrainerProfileId.Value,
                    facilityReservation.TimeSlots,
                    facilityReservation.Date);

                if (!isTrainerAvailable)
                    throw new ConflictException($"Trainer is not available for the selected time slots on facility {facilityReservation.FacilityId}");
            }

            // Calculate expected price for this facility
            if (facilityReservation.CustomPrice.HasValue)
            {
                totalPrice += facilityReservation.CustomPrice.Value;
            }
            else
            {
                var basePricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;

                if (facilityReservation.TrainerProfileId.HasValue)
                {
                    var businessProfile = facility.BusinessProfileId.HasValue
                        ? _businessProfileRepository.GetBusinessProfileById(facility.BusinessProfileId.Value)
                        : _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);

                    if (businessProfile != null)
                    {
                        var association = await _associationRepository.GetByTrainerAndBusinessAsync(
                            facilityReservation.TrainerProfileId.Value,
                            businessProfile.Id);

                        if (association != null && association.Status == AssociationStatus.Confirmed && association.GrossHourlyRate.HasValue)
                        {
                            basePricePerSlot += association.GrossHourlyRate.Value;
                        }
                        else
                        {
                            var trainer = _trainerProfileRepository.GetTrainerProfileById(facilityReservation.TrainerProfileId.Value);
                            if (trainer != null)
                            {
                                basePricePerSlot += trainer.HourlyRate;
                            }
                        }
                    }
                }

                var numberOfUsers = facilityReservation.NumberOfUsers > 0 ? facilityReservation.NumberOfUsers : 1;
                var pricePerSlot = basePricePerSlot;
                if (facility.PricePerUser && facilityReservation.PayForAllUsers)
                {
                    pricePerSlot = basePricePerSlot * numberOfUsers;
                }

                totalPrice += pricePerSlot * facilityReservation.TimeSlots.Count;
            }
        }

        // Create group reservation
        var groupId = Guid.NewGuid();
        var createdReservations = _reservationRepository.CreateAdminGroupReservations(groupDto, createdById, groupId);

        // Map to DTOs
        var reservationResponseDtos = createdReservations.Select(r => MapToDto(r)).ToList();

        // Send reservation confirmation email
        try
        {
            string? customerEmail = null;
            string? customerName = null;

            if (groupDto.UserId.HasValue)
            {
                var user = _userRepository.GetUser(groupDto.UserId.Value);
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    customerEmail = user.Email;
                    customerName = $"{user.FirstName} {user.LastName}".Trim();
                }
            }
            else if (!string.IsNullOrEmpty(groupDto.GuestEmail))
            {
                customerEmail = groupDto.GuestEmail;
                customerName = groupDto.GuestName ?? "Gość";
            }

            if (!string.IsNullOrEmpty(customerEmail))
            {
                var firstReservation = reservationResponseDtos.FirstOrDefault();
                if (firstReservation != null)
                {
                    await _emailService.SendReservationCreatedEmailAsync(firstReservation, customerEmail, customerName);
                }
            }
        }
        catch (Exception emailEx)
        {
            _logger.LogWarning(emailEx, "Failed to send admin group reservation confirmation email");
        }

        // Send notification to business for each reservation
        foreach (var res in createdReservations)
        {
            await SendBusinessReservationNotificationsAsync(res);
        }

        return new GroupReservationResponseDto
        {
            GroupId = groupId,
            Reservations = reservationResponseDtos,
            TotalPrice = totalPrice,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
    }

    private BusinessProfileDto MapBusinessToDto(BusinessProfile businessProfile)
    {
        return new BusinessProfileDto
        {
            Id = businessProfile.Id,
            UserId = businessProfile.UserId,
            Nip = businessProfile.Nip,
            CompanyName = businessProfile.CompanyName,
            DisplayName = businessProfile.DisplayName,
            Address = businessProfile.Address,
            City = businessProfile.City,
            PostalCode = businessProfile.PostalCode,
            Email = businessProfile.Email,
            PhoneNumber = businessProfile.PhoneNumber,
            PhoneCountry = businessProfile.PhoneCountry,
            Regon = businessProfile.Regon,
            Krs = businessProfile.Krs,
            LegalForm = businessProfile.LegalForm,
            CategoryId = businessProfile.CategoryId,
            Mcc = businessProfile.Mcc,
            Website = businessProfile.Website,
            WebsiteDescription = businessProfile.WebsiteDescription,
            ContactPersonName = businessProfile.ContactPersonName,
            ContactPersonSurname = businessProfile.ContactPersonSurname,
            TPayMerchantId = businessProfile.TPayMerchantId,
            TPayAccountId = businessProfile.TPayAccountId,
            TPayPosId = businessProfile.TPayPosId,
            TPayActivationLink = businessProfile.TPayActivationLink,
            TPayVerificationStatus = businessProfile.TPayVerificationStatus,
            TPayRegisteredAt = businessProfile.TPayRegisteredAt,
            AvatarUrl = businessProfile.AvatarUrl,
            FacilityPlanUrl = businessProfile.FacilityPlanUrl,
            FacilityPlanFileName = businessProfile.FacilityPlanFileName,
            FacilityPlanFileType = businessProfile.FacilityPlanFileType,
            Latitude = businessProfile.Latitude,
            Longitude = businessProfile.Longitude,
            CreatedAt = businessProfile.CreatedAt,
            UpdatedAt = businessProfile.UpdatedAt
        };
    }

    // NEW: Cancel specific slots from a reservation
    public async Task<PartialCancellationResponseDto> CancelSpecificSlotsAsync(Guid reservationId, List<Guid> slotIds, Guid userId)
    {
        // 1. Get reservation with slots
        var reservation = await _reservationRepository.GetReservationWithSlotsAsync(reservationId);

        if (reservation == null)
            throw new NotFoundException("Reservation", reservationId.ToString());

        // 2. Get facility to check ownership/agent access
        var facility = reservation.Facility ?? _facilityRepository.GetFacility(reservation.FacilityId);
        if (facility == null)
            throw new NotFoundException("Facility", reservation.FacilityId.ToString());

        // 3. Verify permission - allow:
        //    - User who made the reservation
        //    - Agent who created the reservation
        //    - Facility owner
        //    - Authorized agent for the business
        var isReservationOwner = reservation.UserId == userId;
        var isCreator = reservation.CreatedById == userId;
        var isFacilityOwner = facility.UserId == userId;
        var isAuthorizedAgent = facility.BusinessProfileId.HasValue &&
            await _businessProfileAgentRepository.IsAgentActiveForBusinessAsync(userId, facility.BusinessProfileId.Value);

        if (!isReservationOwner && !isCreator && !isFacilityOwner && !isAuthorizedAgent)
            throw new UnauthorizedException("You do not have permission to cancel slots from this reservation");

        // 4. Validate slots belong to this reservation and are active
        var slotsToCancel = reservation.Slots
            .Where(s => slotIds.Contains(s.Id) && s.Status == "Active")
            .ToList();

        if (!slotsToCancel.Any())
            throw new BusinessRuleException("No active slots found to cancel");

        // 5. Validate timing - cancellation must be at least 48 hours before the reservation
        var earliestSlotToCancel = slotsToCancel.Select(s => s.TimeSlot).OrderBy(t => t).FirstOrDefault() ?? "00:00";
        var slotTime = TimeSpan.TryParse(earliestSlotToCancel, out var ts) ? ts : TimeSpan.Zero;
        var reservationDateTime = reservation.Date.Add(slotTime);
        var hoursUntilReservation = (reservationDateTime - DateTime.UtcNow).TotalHours;

        if (hoursUntilReservation < 48)
            throw new BusinessRuleException("Reservations can only be cancelled at least 48 hours before the scheduled time");

        // 6. Get refund settings
        var cancelledAmount = slotsToCancel.Sum(s => s.SlotPrice);
        var refundSettings = await _settingsService.GetRefundSettingsAsync();
        var daysUntilReservation = (reservation.Date - DateTime.UtcNow.Date).Days;

        decimal refundFeePercentage = 0;
        decimal refundFee = 0;
        decimal netRefund = 0;
        bool refundsEnabled = refundSettings.EnableRefunds;
        bool withinRefundTimeframe = daysUntilReservation <= refundSettings.MaxRefundDaysAdvance;

        // Calculate refund if refunds are enabled and within timeframe
        if (refundsEnabled && withinRefundTimeframe)
        {
            refundFeePercentage = refundSettings.RefundFeePercentage;
            refundFee = cancelledAmount * (refundFeePercentage / 100m);
            netRefund = cancelledAmount - refundFee;
        }

        // 7. Cancel the slots first (always allow cancellation)
        var cancellationReason = refundsEnabled && withinRefundTimeframe
            ? $"Anulowanie - Zwrot: {netRefund:F2} zł"
            : (isFacilityOwner || isAuthorizedAgent)
                ? "Anulowanie przez agenta/właściciela obiektu"
                : "Anulowanie - Brak zwrotu (zwroty wyłączone lub poza terminem)";

        await _reservationRepository.CancelReservationSlotsAsync(
            reservationId,
            slotIds,
            cancellationReason
        );

        // 8. Process refund if applicable
        if (refundsEnabled && withinRefundTimeframe && reservation.PaymentId.HasValue)
        {
            var payment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
            if (payment != null && payment.Status == "COMPLETED" && !payment.IsRefunded)
            {
                await _paymentService.RefundPaymentAsync(
                    reservation.PaymentId.Value,
                    netRefund,
                    reservation.FacilityId
                );
            }
        }

        // 9. Send notification email (only to customer, not for agent cancellations)
        if (isReservationOwner)
        {
            try
            {
                var user = reservation.User ?? _userRepository.GetUser(reservation.UserId ?? Guid.Empty);

                if (user != null && facility != null && !string.IsNullOrEmpty(user.Email))
                {
                    var businessProfile = _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);
                    if (businessProfile != null)
                    {
                        var userName = $"{user.FirstName} {user.LastName}".Trim();
                        var businessDto = MapBusinessToDto(businessProfile);

                        var cancelledSlotTimes = slotsToCancel.Select(s => s.TimeSlot).ToList();
                        var remainingSlotTimes = reservation.Slots
                            .Where(s => s.Status == "Active" && !slotIds.Contains(s.Id))
                            .Select(s => s.TimeSlot)
                            .ToList();

                        if (refundsEnabled && withinRefundTimeframe)
                        {
                            // Send email with refund information
                            await _emailService.SendPartialCancellationWithRefundEmailAsync(
                                reservation.Id,
                                facility.Name,
                                reservation.Date,
                                cancelledSlotTimes,
                                remainingSlotTimes,
                                cancelledAmount,
                                netRefund,
                                refundFee,
                                user.Email,
                                userName
                            );
                        }
                        else
                        {
                            // Send email without refund (with business contact info)
                            await _emailService.SendPartialCancellationNoRefundEmailAsync(
                                reservation.Id,
                                facility.Name,
                                reservation.Date,
                                cancelledSlotTimes,
                                remainingSlotTimes,
                                cancelledAmount,
                                user.Email,
                                userName,
                                businessDto,
                                !refundsEnabled ? "Zwroty są obecnie wyłączone" : $"Anulowania muszą być dokonane co najmniej {refundSettings.MaxRefundDaysAdvance} dni wcześniej"
                            );
                        }
                    }
                }
            }
            catch (Exception emailEx)
            {
                // Log email error but don't fail the cancellation
                System.Diagnostics.Debug.WriteLine($"Failed to send partial cancellation email: {emailEx.Message}");
            }
        }

        // 10. Get updated reservation to reflect new status
        var updatedReservation = await _reservationRepository.GetReservationWithSlotsAsync(reservationId);

        // 11. Return response
        return new PartialCancellationResponseDto
        {
            ReservationId = reservationId,
            CancelledSlots = slotsToCancel.Select(s => new SlotDto
            {
                Id = s.Id,
                TimeSlot = s.TimeSlot,
                Price = s.SlotPrice,
                Status = "Cancelled"
            }).ToList(),
            RemainingSlots = updatedReservation!.Slots
                .Where(s => s.Status == "Active")
                .Select(s => new SlotDto
                {
                    Id = s.Id,
                    TimeSlot = s.TimeSlot,
                    Price = s.SlotPrice,
                    Status = s.Status
                }).ToList(),
            OriginalTotal = updatedReservation.TotalPrice,
            RemainingTotal = updatedReservation.RemainingPrice,
            RefundAmount = netRefund,
            RefundFee = refundFee,
            RefundPercentage = 100m - refundFeePercentage, // Percentage refunded (not fee)
            NewStatus = updatedReservation.Status
        };
    }

    // NEW: Get reservation with slots detail
    public async Task<ReservationWithSlotsDto?> GetReservationWithSlotsAsync(Guid id)
    {
        var reservation = await _reservationRepository.GetReservationWithSlotsAsync(id);

        if (reservation == null)
            return null;

        // Get payment status if payment exists
        string? paymentStatus = null;
        if (reservation.PaymentId.HasValue)
        {
            try
            {
                var payment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
                paymentStatus = payment?.Status;
            }
            catch (Exception)
            {
                // Ignore payment lookup errors
                paymentStatus = "Unknown";
            }
        }

        return new ReservationWithSlotsDto
        {
            Id = reservation.Id,
            FacilityId = reservation.FacilityId,
            FacilityName = reservation.Facility?.Name,
            UserId = reservation.UserId,
            Date = reservation.Date,
            TotalPrice = reservation.TotalPrice,
            RemainingPrice = reservation.RemainingPrice,
            Status = reservation.Status,
            CreatedAt = reservation.CreatedAt,
            UpdatedAt = reservation.UpdatedAt,
            TrainerProfileId = reservation.TrainerProfileId,
            TrainerPrice = reservation.TrainerPrice,
            TrainerDisplayName = reservation.TrainerProfile?.DisplayName,
            
            // Additional reservation details
            CustomerName = reservation.GuestName,
            CustomerEmail = reservation.GuestEmail,
            CustomerPhone = reservation.GuestPhone,
            Notes = reservation.Notes,
            PaymentId = reservation.PaymentId,
            ProductPurchaseId = reservation.ProductPurchaseId,
            PaymentStatus = paymentStatus,
            GroupId = reservation.GroupId,
            CreatedById = reservation.CreatedById,
            CreatedByName = reservation.CreatedBy != null ? $"{reservation.CreatedBy.FirstName} {reservation.CreatedBy.LastName}" : null,

            // Multi-user booking info
            NumberOfUsers = reservation.NumberOfUsers,
            PaidForAllUsers = reservation.PaidForAllUsers,

            Slots = reservation.Slots.Select(s => new SlotDetailDto
            {
                Id = s.Id,
                TimeSlot = s.TimeSlot,
                SlotPrice = s.SlotPrice,
                Status = s.Status,
                CancelledAt = s.CancelledAt,
                CancellationReason = s.CancellationReason
            }).ToList()
        };
    }

    public async Task<ReservationDto> ToggleReservationPaymentAsync(Guid reservationId, Guid agentUserId, string paymentMethod = "OFFLINE")
    {
        try
        {
            _logger.LogInformation("Agent {AgentUserId} toggling payment status for reservation {ReservationId}", agentUserId, reservationId);

            // Get the reservation
            var reservation = _reservationRepository.GetReservation(reservationId);
            if (reservation == null)
            {
                throw new NotFoundException($"Reservation {reservationId} not found");
            }

            // Check if agent has permission for this facility
            var facility = _facilityRepository.GetFacility(reservation.FacilityId);
            if (facility == null)
            {
                throw new NotFoundException("Associated facility not found");
            }

            // Check if user is business owner or authorized agent
            var isBusinessOwner = facility.UserId == agentUserId;
            var isAuthorizedAgent = false;

            if (!isBusinessOwner && facility.BusinessProfileId.HasValue)
            {
                // Check if user is an authorized agent for this business
                var agentRelation = await _businessProfileAgentRepository.GetActiveAgentAssignmentAsync(facility.BusinessProfileId.Value, agentUserId);
                isAuthorizedAgent = agentRelation != null;
            }

            if (!isBusinessOwner && !isAuthorizedAgent)
            {
                throw new ForbiddenException("You are not authorized to mark payments for this facility");
            }

            // Check if reservation can have its payment status changed
            if (reservation.Status != "Active")
            {
                throw new BusinessRuleException($"Cannot change payment status for reservation. Current status: {reservation.Status}");
            }

            // Check current payment status and toggle accordingly
            bool isCurrentlyPaid = false;
            if (reservation.PaymentId.HasValue)
            {
                var existingPayment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
                isCurrentlyPaid = existingPayment != null && existingPayment.Status == "COMPLETED";
            }

            if (isCurrentlyPaid)
            {
                // Mark as unpaid - remove payment association
                reservation.PaymentId = null;
                await _reservationRepository.UpdateReservationAsync(reservation);
                
                _logger.LogInformation("Reservation {ReservationId} marked as unpaid by agent {AgentUserId}", reservationId, agentUserId);
                return MapToDto(reservation);
            }

            // Create an offline payment record
            // For agent-created reservations, use a placeholder UserId if none exists
            var offlinePaymentDto = new CreatePaymentDto
            {
                UserId = reservation.UserId ?? agentUserId, // Use agent's UserId as fallback for offline payments
                Amount = reservation.TotalPrice,
                Description = $"Offline payment for reservation {reservation.Id}",
                Breakdown = $"Facility: {facility.Name}, Date: {reservation.Date:yyyy-MM-dd}",
                CustomerEmail = "offline@payment.com", // Placeholder for offline payments
                CustomerName = "Offline Payment",
                CustomerPhone = "+48000000000", // Placeholder for offline payments
                ReturnUrl = "https://offline-payment/success", // Placeholder for offline payments
                ErrorUrl = "https://offline-payment/error", // Placeholder for offline payments
                MerchantId = null, // No TPay transaction for offline payments
                FacilityId = facility.Id
            };

            var offlinePayment = await _paymentService.ProcessPaymentAsync(offlinePaymentDto);

            // Mark the payment as completed offline
            await _paymentService.HandleTPayNotificationAsync(new TPayNotification
            {
                tr_crc = offlinePayment.Id.ToString(),
                tr_status = "CORRECT",
                tr_id = $"OFFLINE_{DateTime.UtcNow.Ticks}"
            });

            // Update reservation with payment ID
            reservation.PaymentId = offlinePayment.Id;
            await _reservationRepository.UpdateReservationAsync(reservation);

            _logger.LogInformation("Reservation {ReservationId} marked as paid by agent {AgentUserId}", reservationId, agentUserId);

            return MapToDto(reservation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking reservation {ReservationId} as paid by agent {AgentUserId}", reservationId, agentUserId);
            throw;
        }
    }

    public async Task<ApplyProductResultDto> ApplyProductToReservationAsync(Guid reservationId, ApplyProductToReservationDto dto, Guid agentUserId)
    {
        _logger.LogInformation("Agent {AgentUserId} applying product to reservation {ReservationId} with prefix {Prefix} and email {Email}",
            agentUserId, reservationId, dto.PurchaseIdPrefix, dto.UserEmail);

        // Validate prefix length (should be at least 8 characters)
        if (string.IsNullOrWhiteSpace(dto.PurchaseIdPrefix) || dto.PurchaseIdPrefix.Length < 8)
        {
            throw new ValidationException("Purchase ID prefix must be at least 8 characters");
        }

        // Get the reservation
        var reservation = _reservationRepository.GetReservation(reservationId);
        if (reservation == null)
        {
            throw new NotFoundException($"Reservation {reservationId} not found");
        }

        // Check if reservation already has a product or payment
        if (reservation.ProductPurchaseId.HasValue)
        {
            throw new BusinessRuleException("Reservation already has a product applied");
        }

        if (reservation.PaymentId.HasValue)
        {
            var existingPayment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
            if (existingPayment != null && existingPayment.Status == "COMPLETED")
            {
                throw new BusinessRuleException("Reservation is already paid via payment");
            }
        }

        // Check if agent has permission for this facility
        var facility = _facilityRepository.GetFacility(reservation.FacilityId);
        if (facility == null)
        {
            throw new NotFoundException("Associated facility not found");
        }

        var isBusinessOwner = facility.UserId == agentUserId;
        var isAuthorizedAgent = false;

        if (!isBusinessOwner && facility.BusinessProfileId.HasValue)
        {
            var agentRelation = await _businessProfileAgentRepository.GetActiveAgentAssignmentAsync(facility.BusinessProfileId.Value, agentUserId);
            isAuthorizedAgent = agentRelation != null;
        }

        if (!isBusinessOwner && !isAuthorizedAgent)
        {
            throw new ForbiddenException("You are not authorized to apply products to reservations for this facility");
        }

        // Find the product purchase by prefix and email
        var purchase = await _productPurchaseRepository.FindByPrefixAndEmailAsync(dto.PurchaseIdPrefix, dto.UserEmail);
        if (purchase == null)
        {
            throw new NotFoundException($"No product purchase found with ID starting with '{dto.PurchaseIdPrefix}' for email '{dto.UserEmail}'");
        }

        // Validate purchase is active
        if (purchase.Status != "active")
        {
            throw new BusinessRuleException($"Product purchase is {purchase.Status}");
        }

        // Validate purchase hasn't expired
        if (purchase.ExpiryDate < DateTime.UtcNow)
        {
            throw new BusinessRuleException("Product purchase has expired");
        }

        // Calculate usage needed (time slots × number of users)
        var numberOfUsers = reservation.NumberOfUsers > 0 ? reservation.NumberOfUsers : 1;
        var usageNeeded = reservation.TimeSlots.Count * numberOfUsers;

        // Validate remaining usage
        if (purchase.RemainingUsage < usageNeeded)
        {
            throw new BusinessRuleException($"Insufficient product usage. Need {usageNeeded} (slots: {reservation.TimeSlots.Count} × users: {numberOfUsers}), but only {purchase.RemainingUsage} remaining");
        }

        // Validate facility restrictions
        if (!purchase.AppliesToAllFacilities)
        {
            var allowedFacilityIds = new HashSet<Guid>();
            if (!string.IsNullOrEmpty(purchase.FacilityIds))
            {
                var facilityIdStrings = System.Text.Json.JsonSerializer.Deserialize<List<string>>(purchase.FacilityIds);
                if (facilityIdStrings != null)
                {
                    foreach (var idStr in facilityIdStrings)
                    {
                        if (Guid.TryParse(idStr, out var facilityGuid))
                            allowedFacilityIds.Add(facilityGuid);
                    }
                }
            }

            if (!allowedFacilityIds.Contains(reservation.FacilityId))
            {
                throw new BusinessRuleException("Product is not valid for this facility");
            }
        }

        // Validate business match (product should belong to same business as facility)
        if (facility.BusinessProfileId.HasValue && purchase.BusinessProfileId != facility.BusinessProfileId.Value)
        {
            throw new BusinessRuleException("Product does not belong to this business");
        }

        // Apply product to reservation
        reservation.ProductPurchaseId = purchase.Id;
        reservation.RemainingPrice = 0; // Mark as paid
        reservation.UpdatedAt = DateTime.UtcNow;

        // Decrement usage (time slots × number of users)
        purchase.RemainingUsage -= usageNeeded;
        purchase.UpdatedAt = DateTime.UtcNow;

        if (purchase.RemainingUsage == 0)
        {
            purchase.Status = "depleted";
        }

        // Save changes
        await _reservationRepository.UpdateReservationAsync(reservation);
        await _productPurchaseRepository.UpdateAsync(purchase);

        _logger.LogInformation("Product {PurchaseId} applied to reservation {ReservationId}. Used {UsageNeeded} (slots: {Slots} × users: {Users}). Remaining usage: {RemainingUsage}",
            purchase.Id, reservationId, usageNeeded, reservation.TimeSlots.Count, numberOfUsers, purchase.RemainingUsage);

        return new ApplyProductResultDto
        {
            ReservationId = reservationId,
            ProductPurchaseId = purchase.Id,
            ProductTitle = purchase.ProductTitle,
            CustomerEmail = purchase.User?.Email ?? dto.UserEmail,
            CustomerName = purchase.User != null ? $"{purchase.User.FirstName} {purchase.User.LastName}" : "",
            RemainingUsage = purchase.RemainingUsage,
            TotalUsage = purchase.TotalUsage,
            PurchaseStatus = purchase.Status,
            ExpiryDate = purchase.ExpiryDate,
            Message = $"Product '{purchase.ProductTitle}' successfully applied. {purchase.RemainingUsage} uses remaining."
        };
    }

    public async Task<RescheduleResultDto> RescheduleReservationAsync(Guid reservationId, RescheduleReservationDto dto, Guid agentUserId)
    {
        _logger.LogInformation("Agent {AgentUserId} rescheduling reservation {ReservationId}", agentUserId, reservationId);

        // Get the reservation
        var reservation = _reservationRepository.GetReservation(reservationId);
        if (reservation == null)
        {
            throw new NotFoundException($"Reservation {reservationId} not found");
        }

        // Check reservation status
        if (reservation.Status != "Active" && reservation.Status != "Partial")
        {
            throw new BusinessRuleException($"Cannot reschedule reservation with status '{reservation.Status}'");
        }

        // Get original facility
        var originalFacility = _facilityRepository.GetFacility(reservation.FacilityId);
        if (originalFacility == null)
        {
            throw new NotFoundException("Original facility not found");
        }

        // Determine target facility
        var targetFacilityId = dto.NewFacilityId ?? reservation.FacilityId;
        var targetFacility = targetFacilityId == reservation.FacilityId
            ? originalFacility
            : _facilityRepository.GetFacility(targetFacilityId);

        if (targetFacility == null)
        {
            throw new NotFoundException($"Target facility {targetFacilityId} not found");
        }

        // Validate same business if changing facility
        if (dto.NewFacilityId.HasValue && dto.NewFacilityId.Value != reservation.FacilityId)
        {
            if (originalFacility.BusinessProfileId != targetFacility.BusinessProfileId)
            {
                throw new BusinessRuleException("Cannot reschedule to a facility from a different business");
            }
        }

        // Check if agent has permission for this facility
        var isBusinessOwner = originalFacility.UserId == agentUserId;
        var isAuthorizedAgent = false;

        if (!isBusinessOwner && originalFacility.BusinessProfileId.HasValue)
        {
            var agentRelation = await _businessProfileAgentRepository.GetActiveAgentAssignmentAsync(originalFacility.BusinessProfileId.Value, agentUserId);
            isAuthorizedAgent = agentRelation != null;
        }

        if (!isBusinessOwner && !isAuthorizedAgent)
        {
            throw new ForbiddenException("You are not authorized to reschedule reservations for this facility");
        }

        // Validate new time slots are provided
        if (dto.NewTimeSlots == null || !dto.NewTimeSlots.Any())
        {
            throw new ValidationException("New time slots are required");
        }

        // Check if new time slots are available (exclude this reservation's current slots)
        var isAvailable = await _reservationRepository.IsTimeSlotAvailableAsync(
            dto.NewTimeSlots,
            targetFacilityId,
            dto.NewDate,
            null); // Don't exclude any user - we want to see all bookings

        if (!isAvailable)
        {
            throw new ConflictException("One or more of the new time slots are not available");
        }

        // Validate trainer availability if reservation has a trainer
        if (reservation.TrainerProfileId.HasValue)
        {
            var isTrainerAvailable = await IsTrainerAvailableAsync(
                reservation.TrainerProfileId.Value,
                dto.NewTimeSlots,
                dto.NewDate);

            if (!isTrainerAvailable)
            {
                throw new ConflictException("Trainer is not available for the new time slots");
            }
        }

        // Store old values for response
        var oldDate = reservation.Date;
        var oldTimeSlots = reservation.TimeSlots.ToList();
        var oldFacilityId = reservation.FacilityId;
        var oldFacilityName = originalFacility.Name;

        // Update reservation - ensure date is UTC for PostgreSQL
        reservation.Date = DateTime.SpecifyKind(dto.NewDate.Date, DateTimeKind.Utc);
        reservation.TimeSlots = dto.NewTimeSlots;
        reservation.FacilityId = targetFacilityId;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Append reschedule note if provided
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            var rescheduleNote = $"[Reschedule {DateTime.UtcNow:yyyy-MM-dd HH:mm}] {dto.Notes}";
            reservation.Notes = string.IsNullOrEmpty(reservation.Notes)
                ? rescheduleNote
                : $"{reservation.Notes}\n{rescheduleNote}";
        }

        // Recalculate prices (use gross price)
        var slotPrice = targetFacility.GrossPricePerHour ?? targetFacility.PricePerHour;

        // TotalPrice = full cost for all declared users
        var fullSlotPrice = targetFacility.PricePerUser
            ? slotPrice * reservation.NumberOfUsers
            : slotPrice;
        reservation.TotalPrice = fullSlotPrice * dto.NewTimeSlots.Count;
        if (reservation.TrainerPrice.HasValue && reservation.TrainerProfileId.HasValue)
        {
            reservation.TotalPrice += reservation.TrainerPrice.Value * dto.NewTimeSlots.Count;
        }

        // paidPrice = what the booking user already paid (their share)
        var paidSlotPrice = targetFacility.PricePerUser && reservation.PaidForAllUsers
            ? slotPrice * reservation.NumberOfUsers
            : slotPrice;
        var paidPrice = paidSlotPrice * dto.NewTimeSlots.Count;
        if (reservation.TrainerPrice.HasValue && reservation.TrainerProfileId.HasValue)
        {
            paidPrice += reservation.TrainerPrice.Value * dto.NewTimeSlots.Count;
        }

        reservation.RemainingPrice = reservation.ProductPurchaseId.HasValue || reservation.PaymentId.HasValue
            ? reservation.TotalPrice - paidPrice
            : reservation.TotalPrice;

        // Save reservation changes first (without touching slots)
        await _reservationRepository.UpdateReservationAsync(reservation);

        // Replace slots separately to avoid EF tracking issues
        await _reservationRepository.ReplaceReservationSlotsAsync(reservation.Id, dto.NewTimeSlots, slotPrice);

        _logger.LogInformation(
            "Reservation {ReservationId} rescheduled from {OldDate} {OldSlots} to {NewDate} {NewSlots}",
            reservationId, oldDate.ToString("yyyy-MM-dd"), string.Join(",", oldTimeSlots),
            dto.NewDate.ToString("yyyy-MM-dd"), string.Join(",", dto.NewTimeSlots));

        return new RescheduleResultDto
        {
            ReservationId = reservationId,
            OldDate = oldDate,
            NewDate = dto.NewDate,
            OldTimeSlots = oldTimeSlots,
            NewTimeSlots = dto.NewTimeSlots,
            OldFacilityId = oldFacilityId,
            NewFacilityId = targetFacilityId,
            OldFacilityName = oldFacilityName,
            NewFacilityName = targetFacility.Name,
            Status = reservation.Status,
            Message = $"Reservation successfully rescheduled from {oldDate:yyyy-MM-dd} to {dto.NewDate:yyyy-MM-dd}"
        };
    }

    public List<ReservationDto> GetUnpaidReservationsForUser(Guid userId)
    {
        // Get user's email to find guest reservations
        var user = _userRepository.GetUser(userId);
        var userEmail = user?.Email;

        // Get reservations where UserId matches
        var userReservations = _reservationRepository.GetUserReservations(userId);

        // Get guest reservations where email matches (if user has email)
        var guestReservations = new List<Reservation>();
        if (!string.IsNullOrEmpty(userEmail))
        {
            guestReservations = _reservationRepository.GetGuestReservationsByEmail(userEmail);
        }

        // Combine and filter to only unpaid, active reservations that are in the future
        var allReservations = userReservations
            .Concat(guestReservations)
            .DistinctBy(r => r.Id)  // Remove duplicates
            .Where(r => !r.PaymentId.HasValue &&
                       !r.ProductPurchaseId.HasValue &&
                       r.Status == "Active" &&
                       r.Date >= DateTime.UtcNow.Date)
            .ToList();

        return allReservations.Select(MapToDto).ToList();
    }

    public async Task<ReservationDto> UpdateReservationNotesAsync(Guid reservationId, UpdateReservationNotesDto dto, Guid userId)
    {
        _logger.LogInformation("User {UserId} updating notes for reservation {ReservationId}", userId, reservationId);

        // Get the reservation
        var reservation = _reservationRepository.GetReservation(reservationId);
        if (reservation == null)
        {
            throw new NotFoundException($"Reservation {reservationId} not found");
        }

        // Get the facility
        var facility = _facilityRepository.GetFacility(reservation.FacilityId);
        if (facility == null)
        {
            throw new NotFoundException("Facility not found");
        }

        // Check if user is business owner or authorized agent
        var isBusinessOwner = facility.UserId == userId;
        var isAuthorizedAgent = false;

        if (!isBusinessOwner && facility.BusinessProfileId.HasValue)
        {
            var agentRelation = await _businessProfileAgentRepository.GetActiveAgentAssignmentAsync(facility.BusinessProfileId.Value, userId);
            isAuthorizedAgent = agentRelation != null;
        }

        if (!isBusinessOwner && !isAuthorizedAgent)
        {
            throw new ForbiddenException("You are not authorized to update notes for this reservation");
        }

        // Update notes
        reservation.Notes = dto.Notes;
        reservation.UpdatedAt = DateTime.UtcNow;

        await _reservationRepository.UpdateReservationAsync(reservation);

        _logger.LogInformation("Notes updated for reservation {ReservationId}", reservationId);

        return MapToDto(reservation);
    }

    public async Task<PayForReservationResponseDto> PayForReservationAsync(Guid reservationId, PayForReservationDto paymentDto, Guid userId)
    {
        // Get the reservation
        var reservation = _reservationRepository.GetReservation(reservationId);
        if (reservation == null)
            throw new NotFoundException("Reservation", reservationId.ToString());

        // Get user to check email for guest reservations
        var user = _userRepository.GetUser(userId);
        if (user == null)
            throw new NotFoundException("User", userId.ToString());

        // Verify the reservation belongs to the requesting user
        // Allow if: UserId matches OR user's email matches GuestEmail
        var isOwner = reservation.UserId == userId;
        var isGuestWithMatchingEmail = !string.IsNullOrEmpty(reservation.GuestEmail) &&
                                       !string.IsNullOrEmpty(user.Email) &&
                                       reservation.GuestEmail.Equals(user.Email, StringComparison.OrdinalIgnoreCase);

        if (!isOwner && !isGuestWithMatchingEmail)
            throw new ForbiddenException("You can only pay for your own reservations");

        // If this is a guest reservation matching user's email, link the user to it
        if (!reservation.UserId.HasValue && isGuestWithMatchingEmail)
        {
            reservation.UserId = userId;
            _logger.LogInformation("Linking guest reservation {ReservationId} to user {UserId} based on email match", reservationId, userId);
        }

        // Check if reservation is already paid
        if (reservation.PaymentId.HasValue)
            throw new BusinessRuleException("This reservation has already been paid");

        // Check if reservation uses a product purchase
        if (reservation.ProductPurchaseId.HasValue)
            throw new BusinessRuleException("This reservation is covered by a product purchase");

        // Check if reservation is active
        if (reservation.Status != "Active")
            throw new BusinessRuleException($"Cannot pay for reservation with status {reservation.Status}");

        // Check if reservation is in the future
        if (reservation.Date < DateTime.UtcNow.Date)
            throw new BusinessRuleException("Cannot pay for reservations that have already occurred");

        // Get facility for merchant ID
        var facility = _facilityRepository.GetFacility(reservation.FacilityId);
        if (facility == null)
            throw new NotFoundException("Facility", reservation.FacilityId.ToString());

        // Create payment request
        var createPaymentDto = new CreatePaymentDto
        {
            UserId = userId,
            Amount = reservation.RemainingPrice,
            Description = $"Płatność za rezerwację - {facility.Name} - {reservation.Date:yyyy-MM-dd}",
            FacilityId = reservation.FacilityId,
            CustomerEmail = paymentDto.CustomerEmail,
            CustomerName = paymentDto.CustomerName,
            CustomerPhone = paymentDto.CustomerPhone,
            ReturnUrl = paymentDto.ReturnUrl,
            ErrorUrl = paymentDto.ErrorUrl,
            PushToken = paymentDto.PushToken
        };

        // Process payment through payment service
        var payment = await _paymentService.ProcessPaymentAsync(createPaymentDto);

        if (payment.Id == null)
            throw new BusinessRuleException("Failed to create payment");

        // Link payment to reservation
        reservation.PaymentId = payment.Id;
        await _reservationRepository.UpdateReservationAsync(reservation);

        _logger.LogInformation("Payment {PaymentId} created for reservation {ReservationId} by user {UserId}",
            payment.Id, reservationId, userId);

        return new PayForReservationResponseDto
        {
            PaymentId = payment.Id.Value,
            ReservationId = reservationId,
            Amount = payment.Amount,
            PaymentUrl = payment.TPayPaymentUrl ?? string.Empty,
            TransactionId = payment.TPayTransactionId
        };
    }

    /// <summary>
    /// Sends reservation notifications to child business and parent business (if associated)
    /// </summary>
    private async Task SendBusinessReservationNotificationsAsync(Reservation reservation)
    {
        try
        {
            var facility = reservation.Facility ?? _facilityRepository.GetFacility(reservation.FacilityId);
            if (facility == null) return;

            var businessProfile = _businessProfileRepository.GetBusinessProfileByUserId(facility.UserId);
            if (businessProfile == null) return;

            var reservationDto = MapToDto(reservation);
            var businessDto = MapBusinessToDto(businessProfile);

            // Send notification to child business (the one where reservation was made)
            if (!string.IsNullOrEmpty(businessProfile.Email))
            {
                await _emailService.SendNewReservationNotificationEmailAsync(reservationDto, businessDto);
            }

            // Send notification to parent business if there's a confirmed association
            var parentAssociation = await _parentChildAssociationRepository.GetConfirmedAssociationForChildAsync(businessProfile.Id);
            if (parentAssociation != null)
            {
                var parentProfile = parentAssociation.ParentBusinessProfile ??
                    _businessProfileRepository.GetBusinessProfileById(parentAssociation.ParentBusinessProfileId);

                if (parentProfile != null && !string.IsNullOrEmpty(parentProfile.Email))
                {
                    var parentBusinessDto = MapBusinessToDto(parentProfile);
                    // Use parent-specific email that includes child business info
                    await _emailService.SendNewReservationNotificationToParentEmailAsync(reservationDto, parentBusinessDto, businessDto);
                }
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - email notifications are not critical
            _logger.LogWarning(ex, "Failed to send business reservation notification for reservation {ReservationId}", reservation.Id);
        }
    }
}