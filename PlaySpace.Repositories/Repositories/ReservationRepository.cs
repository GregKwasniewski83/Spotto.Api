using PlaySpace.Domain.Models;
using PlaySpace.Domain.DTOs;
using PlaySpace.Repositories.Interfaces;
using PlaySpace.Repositories.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PlaySpace.Repositories.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly PlaySpaceDbContext _context;
    private readonly IPendingTimeSlotReservationRepository _pendingReservationRepository;
    private readonly ILogger<ReservationRepository> _logger;

    public ReservationRepository(
        PlaySpaceDbContext context,
        IPendingTimeSlotReservationRepository pendingReservationRepository,
        ILogger<ReservationRepository> logger)
    {
        _context = context;
        _pendingReservationRepository = pendingReservationRepository;
        _logger = logger;
    }

    public Reservation CreateReservation(CreateReservationDto reservationDto, Guid userId)
    {
        var facility = _context.Facilities.FirstOrDefault(f => f.Id == reservationDto.FacilityId);
        if (facility == null)
            throw new ArgumentException("Facility not found");

        // Check if any of the requested time slots are already reserved
        var utcDate = DateTime.SpecifyKind(reservationDto.Date.Date, DateTimeKind.Utc);
        var existingReservedSlots = GetReservedTimeSlots(reservationDto.FacilityId, utcDate);
        var conflictingSlots = reservationDto.TimeSlots.Where(ts => existingReservedSlots.Contains(ts)).ToList();
        if (conflictingSlots.Any())
        {
            throw new InvalidOperationException($"Time slots already reserved: {string.Join(", ", conflictingSlots)}");
        }

        // Calculate price per slot (use gross price)
        var basePricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;
        decimal? trainerPriceTotal = null;

        // Handle trainer if specified
        if (reservationDto.TrainerProfileId.HasValue)
        {
            var trainer = _context.TrainerProfiles.FirstOrDefault(t => t.Id == reservationDto.TrainerProfileId.Value);
            if (trainer == null)
                throw new ArgumentException("Trainer profile not found");

            // Check for business-specific pricing via trainer-business association
            decimal trainerRate = trainer.HourlyRate;
            if (facility.BusinessProfileId.HasValue)
            {
                var association = _context.TrainerBusinessAssociations
                    .FirstOrDefault(a => a.TrainerProfileId == reservationDto.TrainerProfileId.Value
                        && a.BusinessProfileId == facility.BusinessProfileId.Value
                        && a.Status == AssociationStatus.Confirmed);

                if (association?.GrossHourlyRate.HasValue == true)
                {
                    trainerRate = association.GrossHourlyRate.Value;
                }
            }

            basePricePerSlot += trainerRate;
            trainerPriceTotal = trainerRate * reservationDto.TimeSlots.Count;
        }

        // Calculate total price considering number of users if facility charges per user
        var numberOfUsers = reservationDto.NumberOfUsers > 0 ? reservationDto.NumberOfUsers : 1;
        var pricePerSlot = basePricePerSlot;

        if (facility.PricePerUser && reservationDto.PayForAllUsers)
        {
            pricePerSlot = basePricePerSlot * numberOfUsers;
        }

        var paidPrice = pricePerSlot * reservationDto.TimeSlots.Count;

        // TotalPrice reflects full cost for all declared users
        var totalPrice = facility.PricePerUser
            ? basePricePerSlot * numberOfUsers * reservationDto.TimeSlots.Count
            : paidPrice;

        // RemainingPrice tracks outstanding amount (total minus what booking user already paid)
        var remainingPrice = totalPrice - (reservationDto.PaymentId.HasValue ? paidPrice : 0);

        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            FacilityId = reservationDto.FacilityId,
            UserId = userId,
            Date = DateTime.SpecifyKind(reservationDto.Date.Date, DateTimeKind.Utc),
            TimeSlots = reservationDto.TimeSlots, // Keep for backward compatibility
            TotalPrice = totalPrice,
            RemainingPrice = remainingPrice,
            TrainerProfileId = reservationDto.TrainerProfileId,
            TrainerPrice = trainerPriceTotal,
            PaymentId = reservationDto.PaymentId,
            NumberOfUsers = numberOfUsers,
            PaidForAllUsers = reservationDto.PayForAllUsers,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Reservations.Add(reservation);

        // NEW: Create ReservationSlot records for each time slot
        foreach (var timeSlot in reservationDto.TimeSlots)
        {
            var slot = new ReservationSlot
            {
                Id = Guid.NewGuid(),
                ReservationId = reservation.Id,
                TimeSlot = timeSlot,
                SlotPrice = pricePerSlot,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };

            _context.ReservationSlots.Add(slot);
        }

        _context.SaveChanges();
        return reservation;
    }

    public Reservation? GetReservation(Guid id)
    {
        return _context.Reservations
            .Include(r => r.Facility)
                .ThenInclude(f => f.BusinessProfile)
            .Include(r => r.TrainerProfile)
            .Include(r => r.Payment)
            .Include(r => r.CreatedBy)
            .Include(r => r.Slots)
            .FirstOrDefault(r => r.Id == id);
    }

    public Reservation? GetReservationByPaymentId(Guid paymentId)
    {
        return _context.Reservations
            .FirstOrDefault(r => r.PaymentId == paymentId);
    }

    public List<Reservation> GetUserReservations(Guid userId)
    {
        // Get current UTC date/time for proper comparison
        var utcNow = DateTime.UtcNow;
        var todayUtc = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
        var currentTime = utcNow.TimeOfDay;

        // Get the user's email to check against guest reservations
        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        var userEmail = user?.Email;

        var reservations = _context.Reservations
            .Include(r => r.Facility)
                .ThenInclude(f => f.BusinessProfile)
            .Include(r => r.TrainerProfile)
            .Include(r => r.Payment)
            .Include(r => r.CreatedBy)
            .Include(r => r.Slots)
            .Where(r => r.Status == "Active" && r.Date >= todayUtc && (
                // Regular user reservations
                r.UserId == userId ||
                // Agent-created reservations where user's email was provided as guest email
                (r.CreatedById != null && !string.IsNullOrEmpty(userEmail) && r.GuestEmail == userEmail)
            ))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        // Filter out today's reservations where all time slots have already passed
        return reservations.Where(r =>
        {
            if (r.Date > todayUtc) return true; // Future dates always included

            // For today's reservations, check if the latest time slot is still upcoming
            var latestSlot = r.TimeSlots?.OrderByDescending(t => t).FirstOrDefault();
            if (latestSlot == null) return false;

            return TimeSpan.TryParse(latestSlot, out var slotTime) && slotTime >= currentTime;
        }).ToList();
    }

    public bool CancelReservation(Guid reservationId, Guid userId)
    {
        // Get the user's email to check against guest reservations
        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        var userEmail = user?.Email;

        var reservation = _context.Reservations
            .FirstOrDefault(r => r.Id == reservationId && (
                r.UserId == userId || 
                // Allow cancellation of agent-created reservations where user's email was provided
                (r.CreatedById != null && !string.IsNullOrEmpty(userEmail) && r.GuestEmail == userEmail)
            ));

        if (reservation == null || reservation.Status == "Cancelled")
            return false;

        reservation.Status = "Cancelled";
        reservation.UpdatedAt = DateTime.UtcNow;

        // Free up the time slots
        foreach (var timeSlot in reservation.TimeSlots)
        {
            var existingSlot = _context.TimeSlots
                .FirstOrDefault(ts => ts.FacilityId == reservation.FacilityId &&
                                    ts.Time == timeSlot &&
                                    ts.Date == reservation.Date &&
                                    ts.BookedByUserId == userId);

            if (existingSlot != null)
            {
                existingSlot.IsBooked = false;
                existingSlot.BookedByUserId = null;
                existingSlot.UpdatedAt = DateTime.UtcNow;
            }
        }

        _context.SaveChanges();
        return true;
    }

    public bool CancelReservationByAgent(Guid reservationId, Guid agentUserId, string? agentName, string? notes)
    {
        var reservation = _context.Reservations
            .FirstOrDefault(r => r.Id == reservationId);

        if (reservation == null || reservation.Status == "Cancelled")
            return false;

        reservation.Status = "Cancelled";
        reservation.UpdatedAt = DateTime.UtcNow;
        reservation.CancelledById = agentUserId;
        reservation.CancelledByName = agentName;
        reservation.CancelledAt = DateTime.UtcNow;
        reservation.CancellationNotes = notes;

        // Free up the time slots - need to find by reservation data, not by specific user
        foreach (var timeSlot in reservation.TimeSlots)
        {
            var existingSlot = _context.TimeSlots
                .FirstOrDefault(ts => ts.FacilityId == reservation.FacilityId &&
                                    ts.Time == timeSlot &&
                                    ts.Date == reservation.Date &&
                                    ts.BookedByUserId != null);

            if (existingSlot != null)
            {
                existingSlot.IsBooked = false;
                existingSlot.BookedByUserId = null;
                existingSlot.UpdatedAt = DateTime.UtcNow;
            }
        }

        _context.SaveChanges();
        return true;
    }

    public async Task<bool> IsTimeSlotAvailableAsync(List<string> timeSlots, Guid facilityId, DateTime date, Guid? excludeUserId = null)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        
        // Check regular time slot availability
        foreach (var timeSlot in timeSlots)
        {
            var existingSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(ts => ts.FacilityId == facilityId && 
                                         ts.Time == timeSlot && 
                                         ts.Date == utcDate &&
                                         (ts.IsBooked || !ts.IsAvailable));

            if (existingSlot != null)
                return false;
        }

        // Check for conflicting pending reservations, excluding the current user
        var hasConflictingPending = await _pendingReservationRepository.HasConflictingPendingReservationAsync(facilityId, date, timeSlots, excludeUserId);
        if (hasConflictingPending)
            return false;

        return true;
    }

    public List<string> GetReservedTimeSlots(Guid facilityId, DateTime date)
    {
        try
        {
            var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

            // Get Active reservations - all their slots are reserved
            var activeReservations = _context.Reservations
                .Where(r => r.FacilityId == facilityId &&
                           r.Date == utcDate &&
                           r.Status == "Active")
                .ToList();

            var reservedTimeSlots = activeReservations
                .SelectMany(r => r.TimeSlots)
                .ToList();

            // Get Partial reservations - only their Active slots are reserved
            var partialReservations = _context.Reservations
                .Include(r => r.Slots)
                .Where(r => r.FacilityId == facilityId &&
                           r.Date == utcDate &&
                           r.Status == "Partial")
                .ToList();

            var partialActiveSlots = partialReservations
                .SelectMany(r => r.Slots.Where(s => s.Status == "Active").Select(s => s.TimeSlot))
                .ToList();

            reservedTimeSlots.AddRange(partialActiveSlots);

            return reservedTimeSlots.Distinct().ToList();
        }
        catch(Exception ex)
        {
            throw ex;
        }
    }

    public Reservation? UpdateReservation(Guid reservationId, UpdateReservationDto updateDto, Guid userId)
    {
        // Get the user's email to check against guest reservations
        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        var userEmail = user?.Email;

        var reservation = _context.Reservations
            .Include(r => r.Facility)
            .FirstOrDefault(r => r.Id == reservationId && (
                r.UserId == userId || 
                // Allow updating agent-created reservations where user's email was provided
                (r.CreatedById != null && !string.IsNullOrEmpty(userEmail) && r.GuestEmail == userEmail)
            ));

        if (reservation == null || reservation.Status == "Cancelled")
            return null;

        // Use gross price for total price calculation
        var facilityGrossPrice = reservation.Facility!.GrossPricePerHour ?? reservation.Facility.PricePerHour;
        var originalTotalPrice = facilityGrossPrice * reservation.TimeSlots.Count;

        // Handle trainer update
        if (updateDto.TrainerProfileId.HasValue)
        {
            var trainer = _context.TrainerProfiles.FirstOrDefault(t => t.Id == updateDto.TrainerProfileId.Value);
            if (trainer == null)
                throw new ArgumentException("Trainer profile not found");

            var trainerPrice = trainer.HourlyRate * reservation.TimeSlots.Count;
            reservation.TrainerProfileId = updateDto.TrainerProfileId;
            reservation.TrainerPrice = trainerPrice;
            reservation.TotalPrice = originalTotalPrice + trainerPrice;
        }
        else
        {
            // Remove trainer if TrainerProfileId is null
            reservation.TrainerProfileId = null;
            reservation.TrainerPrice = null;
            reservation.TotalPrice = originalTotalPrice;
        }

        reservation.UpdatedAt = DateTime.UtcNow;
        _context.SaveChanges();

        return GetReservation(reservationId);
    }
    
    public int GetTotalCount()
    {
        return _context.Reservations.Count();
    }
    
    public List<Reservation> GetUserUpcomingReservations(Guid userId)
    {
        var currentDate = DateTime.UtcNow.Date;

        // Get the user's email to check against guest reservations
        var user = _context.Users.FirstOrDefault(u => u.Id == userId);
        var userEmail = user?.Email;

        return _context.Reservations
            .Include(r => r.Facility)
                .ThenInclude(f => f.BusinessProfile)
            .Include(r => r.TrainerProfile)
            .Include(r => r.CreatedBy)
            .Include(r => r.Slots)
            .Where(r => r.Date.Date >= currentDate && r.Status != "Cancelled" && (
                // Regular user reservations
                r.UserId == userId ||
                // Agent-created reservations where user's email was provided as guest email
                (r.CreatedById != null && !string.IsNullOrEmpty(userEmail) && r.GuestEmail == userEmail)
            ))
            .OrderBy(r => r.Date)
            .ToList();
    }

    public List<Reservation> CreateGroupReservations(List<CreateReservationDto> reservationDtos, Guid userId, Guid groupId)
    {
        var createdReservations = new List<Reservation>();

        // Log incoming time slots for diagnostics
        _logger.LogInformation("CreateGroupReservations: Processing {Count} reservation DTOs for user {UserId}, groupId {GroupId}",
            reservationDtos.Count, userId, groupId);

        foreach (var dtoForLog in reservationDtos)
        {
            _logger.LogInformation("CreateGroupReservations: FacilityId={FacilityId}, Date={Date}, TimeSlots=[{TimeSlots}], SlotCount={SlotCount}",
                dtoForLog.FacilityId, dtoForLog.Date, string.Join(", ", dtoForLog.TimeSlots ?? new List<string>()), dtoForLog.TimeSlots?.Count ?? 0);

            // Validate time slot lengths
            var longSlots = dtoForLog.TimeSlots?.Where(ts => ts?.Length > 50).ToList();
            if (longSlots?.Any() == true)
            {
                _logger.LogError("CreateGroupReservations: Found time slots exceeding 50 chars: [{LongSlots}]",
                    string.Join(", ", longSlots.Select(s => $"'{s}' (len={s?.Length})")));
            }
        }

        foreach (var dto in reservationDtos)
        {
            var facility = _context.Facilities.FirstOrDefault(f => f.Id == dto.FacilityId);
            if (facility == null)
                throw new ArgumentException($"Facility with ID {dto.FacilityId} not found");

            // Check if any of the requested time slots are already reserved
            var utcDate = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
            var existingReservedSlots = GetReservedTimeSlots(dto.FacilityId, utcDate);
            var conflictingSlots = dto.TimeSlots.Where(ts => existingReservedSlots.Contains(ts)).ToList();
            if (conflictingSlots.Any())
            {
                throw new InvalidOperationException($"Time slots already reserved: {string.Join(", ", conflictingSlots)}");
            }

            // Calculate price per slot (use gross price)
            var basePricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;
            decimal? trainerPriceTotal = null;

            // Add trainer price if trainer is specified
            if (dto.TrainerProfileId.HasValue)
            {
                var trainer = _context.TrainerProfiles.FirstOrDefault(t => t.Id == dto.TrainerProfileId.Value);
                if (trainer != null)
                {
                    // Check for business-specific pricing via trainer-business association
                    decimal trainerRate = trainer.HourlyRate;
                    if (facility.BusinessProfileId.HasValue)
                    {
                        var association = _context.TrainerBusinessAssociations
                            .FirstOrDefault(a => a.TrainerProfileId == dto.TrainerProfileId.Value
                                && a.BusinessProfileId == facility.BusinessProfileId.Value
                                && a.Status == AssociationStatus.Confirmed);

                        if (association?.GrossHourlyRate.HasValue == true)
                        {
                            trainerRate = association.GrossHourlyRate.Value;
                        }
                    }

                    basePricePerSlot += trainerRate;
                    trainerPriceTotal = trainerRate * dto.TimeSlots.Count;
                }
            }

            // Apply per-user pricing if applicable
            var numberOfUsers = dto.NumberOfUsers > 0 ? dto.NumberOfUsers : 1;
            var pricePerSlot = basePricePerSlot;

            if (facility.PricePerUser && dto.PayForAllUsers)
            {
                pricePerSlot = basePricePerSlot * numberOfUsers;
            }

            var paidPrice = pricePerSlot * dto.TimeSlots.Count;

            var totalPrice = facility.PricePerUser
                ? basePricePerSlot * numberOfUsers * dto.TimeSlots.Count
                : paidPrice;

            var remainingPrice = totalPrice - (dto.PaymentId.HasValue ? paidPrice : 0);

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                FacilityId = dto.FacilityId,
                UserId = userId,
                Date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc),
                TimeSlots = dto.TimeSlots,
                TotalPrice = totalPrice,
                RemainingPrice = remainingPrice,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                TrainerProfileId = dto.TrainerProfileId,
                TrainerPrice = trainerPriceTotal,
                PaymentId = dto.PaymentId,
                ProductPurchaseId = dto.PurchaseId,
                NumberOfUsers = numberOfUsers,
                PaidForAllUsers = dto.PayForAllUsers
            };

            _context.Reservations.Add(reservation);

            // Create ReservationSlot records for each time slot
            foreach (var timeSlot in dto.TimeSlots)
            {
                var slot = new ReservationSlot
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    TimeSlot = timeSlot,
                    SlotPrice = pricePerSlot,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };

                _context.ReservationSlots.Add(slot);
            }

            createdReservations.Add(reservation);
        }

        _context.SaveChanges();
        return createdReservations;
    }

    public List<Reservation> GetGroupReservations(Guid groupId)
    {
        return _context.Reservations
            .Include(r => r.Facility)
                .ThenInclude(f => f.BusinessProfile)
            .Include(r => r.TrainerProfile)
            .Include(r => r.CreatedBy)
            .Include(r => r.Slots)
            .Where(r => r.GroupId == groupId)
            .OrderBy(r => r.CreatedAt)
            .ToList();
    }

    public bool CancelGroupReservations(Guid groupId, Guid userId)
    {
        var reservations = _context.Reservations
            .Where(r => r.GroupId == groupId && r.UserId == userId && r.Status == "Active")
            .ToList();

        if (!reservations.Any())
            return false;

        foreach (var reservation in reservations)
        {
            reservation.Status = "Cancelled";
            reservation.UpdatedAt = DateTime.UtcNow;
        }

        _context.SaveChanges();
        return true;
    }

    public List<Reservation> GetReservationsForFacilityAndDate(Guid facilityId, DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        return _context.Reservations
            .Include(r => r.User)
            .Include(r => r.TrainerProfile)
            .Include(r => r.CreatedBy)
            .Include(r => r.Slots)
            .Include(r => r.Payment)
            .Include(r => r.ProductPurchase)
            .Where(r => r.FacilityId == facilityId &&
                       r.Date == utcDate &&
                       r.Status != "Cancelled")
            .OrderBy(r => r.CreatedAt)
            .ToList();
    }

    public Reservation CreateAdminReservation(AdminCreateReservationDto reservationDto, Guid createdById)
    {
        try
        {
            var facility = _context.Facilities.FirstOrDefault(f => f.Id == reservationDto.FacilityId);
            if (facility == null)
                throw new ArgumentException("Facility not found");

            // Calculate price based on CustomPrice or standard rates
            decimal totalPrice;
            decimal pricePerSlot;
            decimal? trainerPrice = null;

            var numberOfUsers = reservationDto.NumberOfUsers > 0 ? reservationDto.NumberOfUsers : 1;

            if (reservationDto.CustomPrice.HasValue)
            {
                totalPrice = reservationDto.CustomPrice.Value;
                pricePerSlot = totalPrice / reservationDto.TimeSlots.Count; // Divide custom price evenly
            }
            else
            {
                // Use gross price for standard pricing
                var basePricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;

                // Handle trainer if specified
                if (reservationDto.TrainerProfileId.HasValue)
                {
                    var trainer = _context.TrainerProfiles.FirstOrDefault(t => t.Id == reservationDto.TrainerProfileId.Value);
                    if (trainer == null)
                        throw new ArgumentException("Trainer profile not found");

                    // Check for business-specific pricing via trainer-business association
                    decimal trainerRate = trainer.HourlyRate;
                    if (facility.BusinessProfileId.HasValue)
                    {
                        var association = _context.TrainerBusinessAssociations
                            .FirstOrDefault(a => a.TrainerProfileId == reservationDto.TrainerProfileId.Value
                                && a.BusinessProfileId == facility.BusinessProfileId.Value
                                && a.Status == AssociationStatus.Confirmed);

                        if (association?.GrossHourlyRate.HasValue == true)
                        {
                            trainerRate = association.GrossHourlyRate.Value;
                        }
                    }

                    basePricePerSlot += trainerRate;
                    trainerPrice = trainerRate * reservationDto.TimeSlots.Count;
                }

                // Apply per-user pricing if applicable
                pricePerSlot = basePricePerSlot;
                if (facility.PricePerUser && reservationDto.PayForAllUsers)
                {
                    pricePerSlot = basePricePerSlot * numberOfUsers;
                }

                totalPrice = facility.PricePerUser
                    ? basePricePerSlot * numberOfUsers * reservationDto.TimeSlots.Count
                    : pricePerSlot * reservationDto.TimeSlots.Count;
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                FacilityId = reservationDto.FacilityId,
                UserId = reservationDto.UserId,
                Date = DateTime.SpecifyKind(reservationDto.Date.Date, DateTimeKind.Utc),
                TimeSlots = reservationDto.TimeSlots,
                TotalPrice = totalPrice,
                RemainingPrice = totalPrice, // Admin reservations have no upfront payment
                TrainerProfileId = reservationDto.TrainerProfileId,
                TrainerPrice = trainerPrice,
                PaymentId = null, // No payment for admin reservations
                Notes = reservationDto.Notes,
                NumberOfUsers = numberOfUsers,
                PaidForAllUsers = reservationDto.PayForAllUsers,
                GuestName = reservationDto.GuestName,
                GuestPhone = reservationDto.GuestPhone,
                GuestEmail = reservationDto.GuestEmail,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedById = createdById // Track who created this reservation
            };

            _context.Reservations.Add(reservation);

            // Create ReservationSlot records for each time slot
            foreach (var timeSlot in reservationDto.TimeSlots)
            {
                var slot = new ReservationSlot
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    TimeSlot = timeSlot,
                    SlotPrice = pricePerSlot,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };

                _context.ReservationSlots.Add(slot);
            }

            // Mark time slots as booked
            foreach (var timeSlot in reservationDto.TimeSlots)
            {
                var existingSlot = _context.TimeSlots
                    .FirstOrDefault(ts => ts.FacilityId == reservationDto.FacilityId &&
                                        ts.Time == timeSlot &&
                                        ts.Date == reservation.Date);

                if (existingSlot != null)
                {
                    existingSlot.IsBooked = true;
                    existingSlot.BookedByUserId = reservationDto.UserId;
                    existingSlot.UpdatedAt = DateTime.UtcNow;
                }
            }

            _context.SaveChanges();
            return reservation;
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public List<Reservation> CreateAdminGroupReservations(CreateAdminGroupReservationDto groupDto, Guid createdById, Guid groupId)
    {
        var createdReservations = new List<Reservation>();

        foreach (var dto in groupDto.FacilityReservations)
        {
            var facility = _context.Facilities.FirstOrDefault(f => f.Id == dto.FacilityId);
            if (facility == null)
                throw new ArgumentException($"Facility with ID {dto.FacilityId} not found");

            // Check if any of the requested time slots are already reserved
            var utcDate = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
            var existingReservedSlots = GetReservedTimeSlots(dto.FacilityId, utcDate);
            var conflictingSlots = dto.TimeSlots.Where(ts => existingReservedSlots.Contains(ts)).ToList();
            if (conflictingSlots.Any())
            {
                throw new InvalidOperationException($"Time slots already reserved for facility {dto.FacilityId}: {string.Join(", ", conflictingSlots)}");
            }

            // Calculate price
            decimal totalPrice;
            decimal pricePerSlot;
            decimal? trainerPrice = null;

            var numberOfUsers = dto.NumberOfUsers > 0 ? dto.NumberOfUsers : 1;

            if (dto.CustomPrice.HasValue)
            {
                totalPrice = dto.CustomPrice.Value;
                pricePerSlot = totalPrice / dto.TimeSlots.Count;
            }
            else
            {
                var basePricePerSlot = facility.GrossPricePerHour ?? facility.PricePerHour;

                // Handle trainer if specified
                if (dto.TrainerProfileId.HasValue)
                {
                    var trainer = _context.TrainerProfiles.FirstOrDefault(t => t.Id == dto.TrainerProfileId.Value);
                    if (trainer != null)
                    {
                        decimal trainerRate = trainer.HourlyRate;
                        if (facility.BusinessProfileId.HasValue)
                        {
                            var association = _context.TrainerBusinessAssociations
                                .FirstOrDefault(a => a.TrainerProfileId == dto.TrainerProfileId.Value
                                    && a.BusinessProfileId == facility.BusinessProfileId.Value
                                    && a.Status == AssociationStatus.Confirmed);

                            if (association?.GrossHourlyRate.HasValue == true)
                            {
                                trainerRate = association.GrossHourlyRate.Value;
                            }
                        }

                        basePricePerSlot += trainerRate;
                        trainerPrice = trainerRate * dto.TimeSlots.Count;
                    }
                }

                pricePerSlot = basePricePerSlot;
                if (facility.PricePerUser && dto.PayForAllUsers)
                {
                    pricePerSlot = basePricePerSlot * numberOfUsers;
                }

                totalPrice = facility.PricePerUser
                    ? basePricePerSlot * numberOfUsers * dto.TimeSlots.Count
                    : pricePerSlot * dto.TimeSlots.Count;
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                FacilityId = dto.FacilityId,
                UserId = groupDto.UserId,
                Date = utcDate,
                TimeSlots = dto.TimeSlots,
                TotalPrice = totalPrice,
                RemainingPrice = totalPrice, // Admin reservations have no upfront payment
                TrainerProfileId = dto.TrainerProfileId,
                TrainerPrice = trainerPrice,
                PaymentId = null,
                Notes = dto.Notes ?? groupDto.Notes,
                NumberOfUsers = numberOfUsers,
                PaidForAllUsers = dto.PayForAllUsers,
                GuestName = groupDto.GuestName,
                GuestPhone = groupDto.GuestPhone,
                GuestEmail = groupDto.GuestEmail,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedById = createdById
            };

            _context.Reservations.Add(reservation);

            // Create ReservationSlot records
            foreach (var timeSlot in dto.TimeSlots)
            {
                var slot = new ReservationSlot
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    TimeSlot = timeSlot,
                    SlotPrice = pricePerSlot,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                _context.ReservationSlots.Add(slot);
            }

            // Mark time slots as booked
            foreach (var timeSlot in dto.TimeSlots)
            {
                var existingSlot = _context.TimeSlots
                    .FirstOrDefault(ts => ts.FacilityId == dto.FacilityId &&
                                        ts.Time == timeSlot &&
                                        ts.Date == utcDate);

                if (existingSlot != null)
                {
                    existingSlot.IsBooked = true;
                    existingSlot.BookedByUserId = groupDto.UserId;
                    existingSlot.UpdatedAt = DateTime.UtcNow;
                }
            }

            createdReservations.Add(reservation);
        }

        _context.SaveChanges();
        return createdReservations;
    }

    // NEW: Cancel specific slots from a reservation
    public async Task<bool> CancelReservationSlotsAsync(Guid reservationId, List<Guid> slotIds, string? cancellationReason = null)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var reservation = await _context.Reservations
                .Include(r => r.Slots)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
                return false;

            var slotsToCancel = reservation.Slots
                .Where(s => slotIds.Contains(s.Id) && s.Status == "Active")
                .ToList();

            if (!slotsToCancel.Any())
                return false;

            // Cancel each slot
            foreach (var slot in slotsToCancel)
            {
                slot.Status = "Cancelled";
                slot.CancelledAt = DateTime.UtcNow;
                slot.CancellationReason = cancellationReason;
            }

            // Update parent reservation
            var cancelledAmount = slotsToCancel.Sum(s => s.SlotPrice);
            reservation.RemainingPrice -= cancelledAmount;
            reservation.UpdatedAt = DateTime.UtcNow;

            // Update parent status based on remaining active slots
            var activeSlots = reservation.Slots.Count(s => s.Status == "Active");
            if (activeSlots == 0)
                reservation.Status = "Cancelled";
            else if (activeSlots < reservation.Slots.Count)
                reservation.Status = "Partial";

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // NEW: Get reservation with slots loaded
    public async Task<Reservation?> GetReservationWithSlotsAsync(Guid id)
    {
        return await _context.Reservations
            .Include(r => r.Slots.OrderBy(s => s.TimeSlot))
            .Include(r => r.Facility)
                .ThenInclude(f => f.BusinessProfile)
            .Include(r => r.User)
            .Include(r => r.TrainerProfile)
            .Include(r => r.Payment)
            .Include(r => r.CreatedBy)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Reservation> UpdateReservationAsync(Reservation reservation)
    {
        reservation.UpdatedAt = DateTime.UtcNow;

        // Check if entity is already tracked - if so, don't call Update
        var entry = _context.Entry(reservation);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        {
            _context.Reservations.Update(reservation);
        }
        // If already tracked, changes will be saved automatically

        await _context.SaveChangesAsync();
        return reservation;
    }

    public async Task ReplaceReservationSlotsAsync(Guid reservationId, List<string> newTimeSlots, decimal slotPrice)
    {
        // Delete existing slots directly via DbContext to avoid tracking issues
        var existingSlots = await _context.ReservationSlots
            .Where(s => s.ReservationId == reservationId)
            .ToListAsync();

        if (existingSlots.Any())
        {
            _context.ReservationSlots.RemoveRange(existingSlots);
        }

        // Add new slots
        foreach (var timeSlot in newTimeSlots)
        {
            var slot = new ReservationSlot
            {
                Id = Guid.NewGuid(),
                ReservationId = reservationId,
                TimeSlot = timeSlot,
                SlotPrice = slotPrice,
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            _context.ReservationSlots.Add(slot);
        }

        await _context.SaveChangesAsync();
    }

    public List<Reservation> GetGuestReservationsByEmail(string email)
    {
        return _context.Reservations
            .Include(r => r.Facility)
                .ThenInclude(f => f.BusinessProfile)
            .Include(r => r.TrainerProfile)
            .Include(r => r.Slots)
            .Include(r => r.CreatedBy)
            .Where(r => r.GuestEmail != null &&
                       r.GuestEmail.ToLower() == email.ToLower() &&
                       r.UserId == null)  // Only get guest reservations (not linked to a user yet)
            .OrderByDescending(r => r.Date)
            .ToList();
    }

    public async Task<List<Reservation>> GetMonthlyReservationsForBusinessAsync(Guid businessProfileId, int year, int month)
    {
        return await _context.Reservations
            .Include(r => r.Facility)
            .Where(r => r.Facility.BusinessProfileId == businessProfileId
                     && r.Date.Year == year
                     && r.Date.Month == month)
            .ToListAsync();
    }
}