# Reservation Refactoring Analysis - PlaySpace.Api Actual Implementation

## Executive Summary

**Current Design:** One reservation record with `List<string> TimeSlots` stored as comma-separated values in database

**Proposed Design:** Hybrid approach with parent Reservation + child ReservationSlot records (one per hour)

**Verdict:** ✅ **FEASIBLE AND RECOMMENDED** - The current architecture is well-structured for this refactoring.

---

## Current Implementation Analysis

### Project Structure
```
PlaySpace.Api/
├── PlaySpace.Domain/          # Domain models and DTOs
│   ├── Models/
│   │   ├── Reservation.cs     # Existing model
│   │   ├── TimeSlot.cs        # Availability tracking
│   │   └── Payment.cs
│   └── DTOs/
│       └── ReservationDto.cs  # API contracts
├── PlaySpace.Repositories/    # Data access layer
│   ├── Data/
│   │   └── PlaySpaceDbContext.cs
│   └── Repositories/
│       └── ReservationRepository.cs
├── PlaySpace.Services/        # Business logic layer
│   └── Services/
│       └── ReservationService.cs
└── PlaySpace.Api/             # API controllers
```

### Current Reservation Model

**File:** `PlaySpace.Domain/Models/Reservation.cs`

```csharp
public class Reservation
{
    public Guid Id { get; set; }
    public Guid? GroupId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid? UserId { get; set; }          // Nullable for guest reservations
    public DateTime Date { get; set; }
    public List<string> TimeSlots { get; set; } = new();  // ["08:00-09:00", "09:00-10:00"]
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid? TrainerProfileId { get; set; }
    public decimal? TrainerPrice { get; set; }
    public Guid? PaymentId { get; set; }
    public string? Notes { get; set; }

    // Guest information
    public string? GuestName { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }

    // Agent tracking
    public Guid? CreatedById { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationNotes { get; set; }

    // Navigation properties
    public Facility? Facility { get; set; }
    public User? User { get; set; }
    public TrainerProfile? TrainerProfile { get; set; }
    public Payment? Payment { get; set; }
    public User? CreatedBy { get; set; }
}
```

### Database Storage

**File:** `PlaySpaceDbContext.cs` (line 357-361)

```csharp
entity.Property(e => e.TimeSlots)
    .HasConversion(
        v => string.Join(',', v),          // List → "08:00-09:00,09:00-10:00,10:00-11:00"
        v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
    );
```

**Database Column:** `TimeSlots NVARCHAR(MAX)` storing comma-separated values

### Key Characteristics

1. **One reservation = Multiple hours**
   - User books 3 hours → 1 Reservation record with `TimeSlots = ["08:00-09:00", "09:00-10:00", "10:00-11:00"]`

2. **Separate TimeSlot table for availability**
   - `PlaySpace.Domain/Models/TimeSlot.cs` tracks which slots are booked
   - When reservation created, TimeSlot records are marked `IsBooked = true`
   - Repository updates TimeSlots table separately (lines 59-72 in ReservationRepository.cs)

3. **Payment integration**
   - One Payment → One or more Reservations (for group bookings)
   - Payment validation in `ReservationService.cs` (lines 44-60)
   - Payment marked as consumed to prevent reuse

4. **Cancellation flow**
   - `CancelReservationWithRefundAsync()` in ReservationService.cs (line 97+)
   - Calculates refund based on global settings
   - Currently cancels ENTIRE reservation (all time slots)

---

## Limitations of Current Design

### ❌ Cannot Cancel Individual Hours

```
User books 08:00-11:00 (3 hours) for $90
Later wants to cancel just 09:00-10:00
❌ NOT POSSIBLE - must cancel entire 3-hour reservation
```

### ❌ No Per-Slot Pricing

```
Peak hour pricing not supported:
- 08:00-09:00: $25 (off-peak)
- 09:00-10:00: $35 (peak)
- 10:00-11:00: $30 (semi-peak)
Currently all slots must have same price
```

### ❌ Complex Partial Refund Logic

```
If we wanted to add partial cancellations:
- Parse comma-separated string
- Calculate which slots to cancel
- Update string with remaining slots
- Messy and error-prone
```

### ❌ Poor Query Performance for Slot-Specific Data

```sql
-- Finding all reservations with specific timeslot requires string search
SELECT * FROM Reservations
WHERE TimeSlots LIKE '%09:00-10:00%'  -- Slow, can't use index properly
```

---

## Proposed Hybrid Architecture

### New Database Structure

```csharp
// Parent - Overall reservation
public class Reservation
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime Date { get; set; }

    public decimal TotalPrice { get; set; }          // Original total
    public decimal RemainingPrice { get; set; }      // After partial cancellations
    public string Status { get; set; }               // "Active", "Partial", "Cancelled"

    public Guid? GroupId { get; set; }               // Multi-facility bookings
    public Guid? TrainerProfileId { get; set; }
    public decimal? TrainerPrice { get; set; }
    public Guid? PaymentId { get; set; }

    // Existing guest and agent tracking fields...

    // NEW: Navigation to slots
    public ICollection<ReservationSlot> Slots { get; set; } = new List<ReservationSlot>();
}

// NEW: Child - Individual time slot
public class ReservationSlot
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }          // FK to parent

    public string TimeSlot { get; set; }             // "08:00-09:00"
    public decimal SlotPrice { get; set; }           // Price for THIS slot
    public string Status { get; set; }               // "Active", "Cancelled"

    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public Reservation? Reservation { get; set; }
}
```

### Database Tables

**Reservations Table** (existing, with modifications):
```sql
Reservations
├─ Id (PK)
├─ FacilityId
├─ UserId
├─ Date
├─ TotalPrice        -- Original total
├─ RemainingPrice    -- NEW: Tracks current value after cancellations
├─ Status            -- "Active", "Partial", "Cancelled"
├─ GroupId
├─ TrainerProfileId
├─ TrainerPrice
├─ PaymentId
└─ ... (all other existing fields)
```

**ReservationSlots Table** (NEW):
```sql
ReservationSlots
├─ Id (PK)
├─ ReservationId (FK)
├─ TimeSlot           -- "08:00-09:00"
├─ SlotPrice
├─ Status             -- "Active", "Cancelled"
├─ CancelledAt
├─ CancellationReason
└─ CreatedAt
```

---

## Implementation Impact Analysis

### ✅ Files Requiring Changes

| File | Impact Level | Changes Required |
|------|--------------|------------------|
| `PlaySpace.Domain/Models/Reservation.cs` | 🟡 MEDIUM | Add `RemainingPrice`, `Slots` navigation property |
| `PlaySpace.Domain/Models/ReservationSlot.cs` | 🟢 NEW FILE | Create new model |
| `PlaySpace.Domain/DTOs/ReservationDto.cs` | 🟡 MEDIUM | Add slot details, new status values |
| `PlaySpace.Domain/DTOs/*` | 🟢 NEW FILES | Create partial cancellation DTOs |
| `PlaySpaceDbContext.cs` | 🔴 HIGH | Add ReservationSlots DbSet, configure relationships |
| `ReservationRepository.cs` | 🔴 HIGH | Rewrite CreateReservation, add slot cancellation methods |
| `ReservationService.cs` | 🔴 HIGH | Add partial cancellation logic |
| Controllers | 🟡 MEDIUM | Add new endpoints for partial cancellation |
| Database Migration | 🔴 HIGH | Create migration, data migration script |

### Required Changes in Detail

#### 1. ReservationRepository.cs Changes

**Current CreateReservation (lines 20-76):**
```csharp
public Reservation CreateReservation(CreateReservationDto reservationDto, Guid userId)
{
    // ... validation ...

    var reservation = new Reservation
    {
        TimeSlots = reservationDto.TimeSlots,  // Store as list
        TotalPrice = totalPrice
    };

    _context.Reservations.Add(reservation);

    // Mark TimeSlot records as booked
    foreach (var timeSlot in reservationDto.TimeSlots) { ... }

    _context.SaveChanges();
}
```

**Proposed CreateReservation (Hybrid):**
```csharp
public Reservation CreateReservation(CreateReservationDto reservationDto, Guid userId)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        var facility = _context.Facilities.FirstOrDefault(f => f.Id == reservationDto.FacilityId);
        var pricePerSlot = facility.PricePerHour;
        var totalPrice = pricePerSlot * reservationDto.TimeSlots.Count;

        // Handle trainer pricing
        if (reservationDto.TrainerProfileId.HasValue)
        {
            var trainer = _context.TrainerProfiles.FirstOrDefault(...);
            var trainerPricePerSlot = trainer.HourlyRate;
            pricePerSlot += trainerPricePerSlot;
            totalPrice += trainerPricePerSlot * reservationDto.TimeSlots.Count;
        }

        // Create parent reservation
        var reservation = new Reservation
        {
            Id = Guid.NewGuid(),
            FacilityId = reservationDto.FacilityId,
            UserId = userId,
            Date = DateTime.SpecifyKind(reservationDto.Date.Date, DateTimeKind.Utc),
            TotalPrice = totalPrice,
            RemainingPrice = totalPrice,  // NEW
            Status = "Active",
            // ... other fields ...
        };

        _context.Reservations.Add(reservation);

        // NEW: Create child slot records
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

            // Mark TimeSlot as booked (existing logic)
            var existingSlot = _context.TimeSlots
                .FirstOrDefault(ts => ts.FacilityId == reservationDto.FacilityId
                    && ts.Time == timeSlot
                    && ts.Date == reservation.Date);

            if (existingSlot != null)
            {
                existingSlot.IsBooked = true;
                existingSlot.BookedByUserId = userId;
            }
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return reservation;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

#### 2. New Repository Methods

```csharp
// Cancel specific slots
public async Task<bool> CancelReservationSlotsAsync(
    Guid reservationId,
    List<Guid> slotIds,
    string cancellationReason = null)
{
    using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        var reservation = await _context.Reservations
            .Include(r => r.Slots)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null) return false;

        var slotsToCancel = reservation.Slots
            .Where(s => slotIds.Contains(s.Id) && s.Status == "Active")
            .ToList();

        if (!slotsToCancel.Any()) return false;

        // Cancel each slot
        foreach (var slot in slotsToCancel)
        {
            slot.Status = "Cancelled";
            slot.CancelledAt = DateTime.UtcNow;
            slot.CancellationReason = cancellationReason;

            // Mark TimeSlot as available
            var timeSlot = await _context.TimeSlots
                .FirstOrDefaultAsync(ts =>
                    ts.FacilityId == reservation.FacilityId &&
                    ts.Date == reservation.Date &&
                    ts.Time == slot.TimeSlot);

            if (timeSlot != null)
            {
                timeSlot.IsBooked = false;
                timeSlot.BookedByUserId = null;
            }
        }

        // Update parent reservation
        var cancelledAmount = slotsToCancel.Sum(s => s.SlotPrice);
        reservation.RemainingPrice -= cancelledAmount;
        reservation.UpdatedAt = DateTime.UtcNow;

        // Update parent status
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

// Get reservation with slots
public async Task<Reservation?> GetReservationWithSlotsAsync(Guid id)
{
    return await _context.Reservations
        .Include(r => r.Slots.OrderBy(s => s.TimeSlot))
        .Include(r => r.Facility)
        .Include(r => r.TrainerProfile)
        .Include(r => r.Payment)
        .Include(r => r.CreatedBy)
        .FirstOrDefaultAsync(r => r.Id == id);
}
```

#### 3. Service Layer Changes

```csharp
// ReservationService.cs - NEW METHOD
public async Task<PartialCancellationResponseDto> CancelSpecificSlotsAsync(
    Guid reservationId,
    List<Guid> slotIds,
    Guid userId)
{
    // 1. Get reservation with slots
    var reservation = await _reservationRepository.GetReservationWithSlotsAsync(reservationId);

    if (reservation == null)
        throw new NotFoundException("Reservation", reservationId.ToString());

    // 2. Verify ownership
    if (reservation.UserId != userId && reservation.CreatedById != userId)
        throw new UnauthorizedException("Not your reservation");

    // 3. Validate slots
    var slotsToCancel = reservation.Slots
        .Where(s => slotIds.Contains(s.Id) && s.Status == "Active")
        .ToList();

    if (!slotsToCancel.Any())
        throw new BusinessRuleException("No active slots found to cancel");

    // 4. Validate timing (cannot cancel same-day or past)
    if (reservation.Date.Date <= DateTime.UtcNow.Date)
        throw new BusinessRuleException("Cannot cancel same-day or past reservations");

    // 5. Calculate refund
    var cancelledAmount = slotsToCancel.Sum(s => s.SlotPrice);
    var settings = await _settingsService.GetSettingsAsync();
    var daysUntilReservation = (reservation.Date - DateTime.UtcNow.Date).Days;
    var refundPercentage = CalculateRefundPercentage(daysUntilReservation, settings);
    var refundFee = cancelledAmount * (1 - refundPercentage / 100m);
    var netRefund = cancelledAmount - refundFee;

    // 6. Process refund
    var payment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
    await _paymentService.ProcessRefundAsync(
        payment.Id,
        netRefund,
        $"Partial cancellation: {slotsToCancel.Count} slot(s)"
    );

    // 7. Cancel the slots
    await _reservationRepository.CancelReservationSlotsAsync(
        reservationId,
        slotIds,
        $"User cancellation - Refund: ${netRefund:F2}"
    );

    // 8. Return response
    return new PartialCancellationResponseDto
    {
        ReservationId = reservationId,
        CancelledSlots = slotsToCancel.Select(s => new SlotDto
        {
            Id = s.Id,
            TimeSlot = s.TimeSlot,
            Price = s.SlotPrice
        }).ToList(),
        RemainingSlots = reservation.Slots
            .Where(s => s.Status == "Active" && !slotIds.Contains(s.Id))
            .Select(s => new SlotDto
            {
                Id = s.Id,
                TimeSlot = s.TimeSlot,
                Price = s.SlotPrice
            }).ToList(),
        RefundAmount = netRefund,
        RefundFee = refundFee,
        NewStatus = reservation.Status
    };
}
```

#### 4. New DTOs

**File:** `PlaySpace.Domain/DTOs/PartialCancellationDto.cs` (NEW)

```csharp
namespace PlaySpace.Domain.DTOs;

public class CancelSlotsDto
{
    public List<Guid> SlotIds { get; set; } = new();
}

public class PartialCancellationResponseDto
{
    public Guid ReservationId { get; set; }
    public List<SlotDto> CancelledSlots { get; set; } = new();
    public List<SlotDto> RemainingSlots { get; set; } = new();
    public decimal RefundAmount { get; set; }
    public decimal RefundFee { get; set; }
    public decimal RefundPercentage { get; set; }
    public string NewStatus { get; set; } = string.Empty;
}

public class SlotDto
{
    public Guid Id { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ReservationWithSlotsDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public DateTime Date { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal RemainingPrice { get; set; }
    public string Status { get; set; } = string.Empty;

    public List<SlotDetailDto> Slots { get; set; } = new();

    public int TotalSlots => Slots.Count;
    public int ActiveSlots => Slots.Count(s => s.Status == "Active");
    public int CancelledSlots => Slots.Count(s => s.Status == "Cancelled");
}

public class SlotDetailDto
{
    public Guid Id { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public decimal SlotPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
```

#### 5. Controller Changes

**File:** `PlaySpace.Api/Controllers/ReservationController.cs`

```csharp
// NEW ENDPOINT
[HttpPost("{id}/cancel-slots")]
[Authorize]
public async Task<ActionResult<PartialCancellationResponseDto>> CancelSpecificSlots(
    Guid id,
    [FromBody] CancelSlotsDto dto)
{
    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

    var result = await _reservationService.CancelSpecificSlotsAsync(
        id,
        dto.SlotIds,
        userId
    );

    return Ok(result);
}

// NEW ENDPOINT
[HttpGet("{id}/slots")]
public async Task<ActionResult<ReservationWithSlotsDto>> GetReservationWithSlots(Guid id)
{
    var result = await _reservationService.GetReservationWithSlotsAsync(id);

    if (result == null)
        return NotFound();

    return Ok(result);
}
```

#### 6. Database Migration

```csharp
// Add to PlaySpace.Repositories/Migrations/

public partial class AddReservationSlots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add RemainingPrice to Reservations
        migrationBuilder.AddColumn<decimal>(
            name: "RemainingPrice",
            table: "Reservations",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);

        // Create ReservationSlots table
        migrationBuilder.CreateTable(
            name: "ReservationSlots",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                ReservationId = table.Column<Guid>(nullable: false),
                TimeSlot = table.Column<string>(maxLength: 20, nullable: false),
                SlotPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                Status = table.Column<string>(maxLength: 50, nullable: false, defaultValue: "Active"),
                CancelledAt = table.Column<DateTime>(nullable: true),
                CancellationReason = table.Column<string>(maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReservationSlots", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReservationSlots_Reservations_ReservationId",
                    column: x => x.ReservationId,
                    principalTable: "Reservations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_ReservationSlots_ReservationId",
            table: "ReservationSlots",
            column: "ReservationId");

        migrationBuilder.CreateIndex(
            name: "IX_ReservationSlots_TimeSlot",
            table: "ReservationSlots",
            column: "TimeSlot");

        migrationBuilder.CreateIndex(
            name: "IX_ReservationSlots_Status",
            table: "ReservationSlots",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReservationSlots");
        migrationBuilder.DropColumn(name: "RemainingPrice", table: "Reservations");
    }
}
```

#### 7. Data Migration Script

```csharp
// Migrate existing reservations to create slot records
public async Task MigrateExistingReservations(PlaySpaceDbContext context)
{
    var reservations = await context.Reservations
        .Where(r => r.TimeSlots != null && r.TimeSlots.Any())
        .ToListAsync();

    foreach (var reservation in reservations)
    {
        var pricePerSlot = reservation.TotalPrice / reservation.TimeSlots.Count;

        // Set RemainingPrice
        reservation.RemainingPrice = reservation.Status == "Cancelled"
            ? 0
            : reservation.TotalPrice;

        // Create slots
        foreach (var timeSlot in reservation.TimeSlots)
        {
            var slot = new ReservationSlot
            {
                Id = Guid.NewGuid(),
                ReservationId = reservation.Id,
                TimeSlot = timeSlot,
                SlotPrice = pricePerSlot,
                Status = reservation.Status == "Cancelled" ? "Cancelled" : "Active",
                CancelledAt = reservation.Status == "Cancelled" ? reservation.CancelledAt : null,
                CreatedAt = reservation.CreatedAt
            };

            context.ReservationSlots.Add(slot);
        }
    }

    await context.SaveChangesAsync();
}
```

---

## Benefits of Hybrid Approach

### ✅ Enables Partial Cancellations

```
User books 08:00-11:00 (3 hours) for $90
Later cancels 09:00-10:00 only
✅ Keeps 08:00-09:00 and 10:00-11:00
✅ Gets refund for $30 (minus fees)
✅ Cancelled slot immediately available for others
```

### ✅ Flexible Pricing

```
Can implement dynamic pricing per slot:
- 08:00-09:00: $25 (off-peak)
- 09:00-10:00: $35 (peak hour)
- 10:00-11:00: $30 (semi-peak)
Total: $90
```

### ✅ Better Data Integrity

```
No more comma-separated strings
Each slot is a proper database record
Easier to query and report
```

### ✅ Granular Status Tracking

```
Reservation Status:
- "Active": All slots active
- "Partial": Some slots cancelled
- "Cancelled": All slots cancelled

Each slot has its own status and cancellation tracking
```

### ✅ Improved Analytics

```sql
-- Find all reservations for specific time slot
SELECT r.* FROM Reservations r
INNER JOIN ReservationSlots s ON r.Id = s.ReservationId
WHERE s.TimeSlot = '09:00-10:00'
AND s.Status = 'Active'
-- Fast, uses index

-- Revenue by time slot
SELECT TimeSlot, SUM(SlotPrice) as Revenue
FROM ReservationSlots
WHERE Status = 'Active'
GROUP BY TimeSlot
```

---

## Migration Strategy

### Phase 1: Add New Structure (Non-Breaking)

1. Create `ReservationSlot` model
2. Add `RemainingPrice` to `Reservation`
3. Update `PlaySpaceDbContext` configuration
4. Create and run migration
5. Update `CreateReservation` to create both parent and child records
6. Keep `TimeSlots` property for backward compatibility

**Status:** System works with both old and new data

### Phase 2: Add New Features

1. Add partial cancellation repository methods
2. Add partial cancellation service methods
3. Add partial cancellation controller endpoints
4. Update DTOs

**Status:** New features available, old data still works

### Phase 3: Data Migration

1. Run data migration script to populate `ReservationSlots` for existing reservations
2. Set `RemainingPrice` for all existing reservations
3. Verify data integrity

**Status:** All data in new format

### Phase 4: Cleanup (Optional)

1. Remove `TimeSlots` property from `Reservation` model
2. Remove database column
3. Update any remaining code references

**Status:** Full migration complete

---

## Risks and Mitigation

### Risk 1: Data Migration Complexity
**Mitigation:**
- Run migration in stages
- Keep old data intact during transition
- Extensive testing on staging environment
- Rollback plan ready

### Risk 2: Increased Database Size
**Impact:** ~3-5x more rows in reservation-related tables
**Mitigation:**
- Proper indexing on `ReservationId`, `TimeSlot`, `Status`
- Archive old cancelled reservations
- Monitor database performance

### Risk 3: Transaction Complexity
**Mitigation:**
- Use database transactions for all multi-record operations
- Comprehensive error handling
- Retry logic for transient failures

### Risk 4: Payment Refund Edge Cases
**Mitigation:**
- Thorough testing of refund calculations
- Idempotent refund operations
- Detailed logging and audit trail

---

## Timeline Estimate

| Phase | Duration | Tasks |
|-------|----------|-------|
| **Phase 1: Models & DB** | 1 week | Create models, migration, update DbContext |
| **Phase 2: Repository** | 1-2 weeks | Update CreateReservation, add slot methods |
| **Phase 3: Service Layer** | 2 weeks | Partial cancellation logic, refund calculations |
| **Phase 4: API Layer** | 1 week | New endpoints, update DTOs |
| **Phase 5: Testing** | 2 weeks | Unit tests, integration tests, E2E tests |
| **Phase 6: Data Migration** | 1 week | Migration script, validation |
| **Total** | **8-9 weeks** | **~2 months** |

---

## Recommendation

### ✅ PROCEED with Hybrid Approach

**Reasons:**
1. **Well-architected foundation** - Clean separation of Domain, Repositories, Services
2. **Moderate effort** - ~2 months vs. 3-4 months for full refactor
3. **Low risk** - Can be done incrementally without breaking existing functionality
4. **High value** - Enables flexible cancellations, dynamic pricing, better analytics
5. **Future-proof** - Sets foundation for advanced features

**Next Steps:**
1. Get stakeholder approval
2. Create detailed technical specification
3. Set up feature branch
4. Begin Phase 1 implementation
5. Comprehensive testing at each phase
6. Staged rollout to production

---

## Conclusion

The hybrid approach is **feasible, recommended, and ready to implement**. The current codebase is well-structured for this refactoring, with clear separation of concerns and good architectural patterns already in place.

The ability to cancel individual time slots will significantly improve user experience and provide flexibility for dynamic pricing and advanced scheduling features.
