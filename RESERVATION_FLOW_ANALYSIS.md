# Complete Reservation Flow Analysis - Hybrid Slot System

## Overview

The reservation system now uses a **hybrid approach** where each reservation creates individual slot records, enabling flexible partial cancellations while maintaining a parent-child relationship.

---

## Flow 1: Creating a Reservation (Happy Path)

### Step 1: User Selects Time Slots (Frontend)

**User Action:**
- User browses available slots for a facility
- Selects date: `2025-12-05`
- Selects time slots: `["08:00-09:00", "09:00-10:00", "10:00-11:00"]` (3 hours)

### Step 2: Create Pending Reservation (Optional but Recommended)

**API Call:**
```http
POST /api/pending-reservation
Authorization: Bearer {token}

{
  "facilityId": "facility-123",
  "date": "2025-12-05",
  "timeSlots": ["08:00-09:00", "09:00-10:00", "10:00-11:00"]
}
```

**What Happens:**
- Creates `PendingTimeSlotReservation` record
- Holds slots for 10-15 minutes (configurable)
- Prevents other users from booking during checkout
- **Location:** `PendingTimeSlotReservationService.cs`

**Database Changes:**
```sql
INSERT INTO PendingTimeSlotReservations (Id, UserId, FacilityId, Date, TimeSlots, ExpiresAt)
VALUES (...);
```

### Step 3: User Proceeds to Payment

**API Call:**
```http
POST /api/payment
Authorization: Bearer {token}

{
  "facilityId": "facility-123",
  "timeSlots": ["08:00-09:00", "09:00-10:00", "10:00-11:00"],
  "amount": 90.00,
  "trainerId": null
}
```

**What Happens:**
- Creates Payment record with status "PENDING"
- Initiates payment gateway transaction (TPay)
- Returns payment URL for user

**Database Changes:**
```sql
INSERT INTO Payments (Id, UserId, Amount, Status, TransactionId, ...)
VALUES ('payment-123', 'user-1', 90.00, 'PENDING', 'tpay-trans-456', ...);
```

### Step 4: Payment Completes Successfully

**TPay Webhook:**
```http
POST /api/payment/tpay-notification

{
  "transactionId": "tpay-trans-456",
  "status": "COMPLETED",
  ...
}
```

**What Happens:**
- Payment status updated to "COMPLETED"
- Payment marked as ready to be consumed

**Database Changes:**
```sql
UPDATE Payments
SET Status = 'COMPLETED', UpdatedAt = NOW()
WHERE TransactionId = 'tpay-trans-456';
```

### Step 5: Create Reservation with Completed Payment

**API Call:**
```http
POST /api/reservation
Authorization: Bearer {token}

{
  "facilityId": "facility-123",
  "date": "2025-12-05",
  "timeSlots": ["08:00-09:00", "09:00-10:00", "10:00-11:00"],
  "trainerProfileId": null,
  "paymentId": "payment-123"
}
```

**Service Layer:** `ReservationService.CreateReservationAsync()`

**Validations Performed:**

1. **Payment Validation** (lines 44-60):
```csharp
var payment = await _paymentService.GetPaymentByIdAsync(reservationDto.PaymentId);
if (payment == null)
    throw new NotFoundException("Payment", reservationDto.PaymentId.ToString());

if (payment.Status != "COMPLETED")
    throw new BusinessRuleException("Payment is not completed");

if (payment.IsConsumed)
    throw new BusinessRuleException("Payment has already been used");

// Prevent duplicate payment usage
var existingReservation = _reservationRepository.GetReservationByPaymentId(reservationDto.PaymentId);
if (existingReservation != null && existingReservation.GroupId == null)
    throw new ConflictException("A reservation already exists for this payment");
```

2. **Amount Validation:**
```csharp
await ValidatePaymentAmount(payment, reservationDto);
```

**Repository Layer:** `ReservationRepository.CreateReservation()`

**Database Operations:**

```csharp
// 1. Calculate pricing
var pricePerSlot = facility.PricePerHour; // e.g., 30.00
if (trainer != null)
    pricePerSlot += trainer.HourlyRate;

var totalPrice = pricePerSlot * timeSlots.Count; // 30 * 3 = 90.00

// 2. Create parent Reservation
var reservation = new Reservation
{
    Id = Guid.NewGuid(),
    FacilityId = facilityId,
    UserId = userId,
    Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
    TimeSlots = timeSlots, // Kept for backward compatibility
    TotalPrice = 90.00,
    RemainingPrice = 90.00, // NEW: Initially equals total
    TrainerProfileId = trainerId,
    TrainerPrice = null,
    PaymentId = paymentId,
    Status = "Active",
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

_context.Reservations.Add(reservation);

// 3. Create child ReservationSlot records (NEW!)
foreach (var timeSlot in timeSlots)
{
    var slot = new ReservationSlot
    {
        Id = Guid.NewGuid(),
        ReservationId = reservation.Id,
        TimeSlot = timeSlot,         // "08:00-09:00"
        SlotPrice = 30.00,            // Price per slot
        Status = "Active",
        CreatedAt = DateTime.UtcNow
    };

    _context.ReservationSlots.Add(slot);
}

_context.SaveChanges();
```

**Database Changes:**

```sql
-- 1. Create Reservation
INSERT INTO Reservations (
    Id, FacilityId, UserId, Date, TimeSlots,
    TotalPrice, RemainingPrice, Status, PaymentId, CreatedAt, UpdatedAt
)
VALUES (
    'res-123', 'facility-123', 'user-1', '2025-12-05', '08:00-09:00,09:00-10:00,10:00-11:00',
    90.00, 90.00, 'Active', 'payment-123', NOW(), NOW()
);

-- 2. Create ReservationSlots (3 records)
INSERT INTO ReservationSlots (Id, ReservationId, TimeSlot, SlotPrice, Status, CreatedAt)
VALUES
    ('slot-1', 'res-123', '08:00-09:00', 30.00, 'Active', NOW()),
    ('slot-2', 'res-123', '09:00-10:00', 30.00, 'Active', NOW()),
    ('slot-3', 'res-123', '10:00-11:00', 30.00, 'Active', NOW());
```

### Step 6: Clean Up Pending Reservation

**Service Layer:** `ReservationService.CreateReservationAsync()` (line 65)

```csharp
// Remove any pending reservation since reservation was created successfully
await _pendingReservationRepository.RemoveUserPendingReservationAsync(
    reservationDto.FacilityId,
    reservationDto.Date,
    userId
);
```

**Database Changes:**
```sql
DELETE FROM PendingTimeSlotReservations
WHERE UserId = 'user-1'
  AND FacilityId = 'facility-123'
  AND Date = '2025-12-05';
```

### Step 7: Return Response to User

**Response:**
```json
{
  "id": "res-123",
  "facilityId": "facility-123",
  "userId": "user-1",
  "date": "2025-12-05",
  "timeSlots": ["08:00-09:00", "09:00-10:00", "10:00-11:00"],
  "totalPrice": 90.00,
  "status": "Active",
  "createdAt": "2025-12-01T10:30:00Z",
  "facilityName": "Tennis Court 1",
  "paymentId": "payment-123"
}
```

---

## Flow 2: Viewing Reservation with Slot Details

### API Call:
```http
GET /api/reservation/res-123/slots
```

### Service Layer: `ReservationService.GetReservationWithSlotsAsync()`

### Repository Layer: `ReservationRepository.GetReservationWithSlotsAsync()`

```csharp
return await _context.Reservations
    .Include(r => r.Slots.OrderBy(s => s.TimeSlot))
    .Include(r => r.Facility)
    .Include(r => r.User)
    .Include(r => r.TrainerProfile)
    .Include(r => r.Payment)
    .Include(r => r.CreatedBy)
    .FirstOrDefaultAsync(r => r.Id == id);
```

### Response:
```json
{
  "id": "res-123",
  "facilityId": "facility-123",
  "facilityName": "Tennis Court 1",
  "userId": "user-1",
  "date": "2025-12-05",
  "totalPrice": 90.00,
  "remainingPrice": 90.00,
  "status": "Active",
  "slots": [
    {
      "id": "slot-1",
      "timeSlot": "08:00-09:00",
      "slotPrice": 30.00,
      "status": "Active",
      "cancelledAt": null,
      "cancellationReason": null
    },
    {
      "id": "slot-2",
      "timeSlot": "09:00-10:00",
      "slotPrice": 30.00,
      "status": "Active",
      "cancelledAt": null,
      "cancellationReason": null
    },
    {
      "id": "slot-3",
      "timeSlot": "10:00-11:00",
      "slotPrice": 30.00,
      "status": "Active",
      "cancelledAt": null,
      "cancellationReason": null
    }
  ],
  "totalSlots": 3,
  "activeSlots": 3,
  "cancelledSlots": 0
}
```

---

## Flow 3: Partial Cancellation (NEW Feature!)

### Scenario: User wants to cancel middle hour only

**API Call:**
```http
POST /api/reservation/res-123/cancel-slots
Authorization: Bearer {token}

{
  "slotIds": ["slot-2"]
}
```

### Service Layer: `ReservationService.CancelSpecificSlotsAsync()`

**Step 1: Get Reservation with Slots**
```csharp
var reservation = await _reservationRepository.GetReservationWithSlotsAsync(reservationId);
```

**Step 2: Verify Ownership**
```csharp
if (reservation.UserId != userId && reservation.CreatedById != userId)
    throw new UnauthorizedException("You do not have permission to cancel this reservation");
```

**Step 3: Validate Slots**
```csharp
var slotsToCancel = reservation.Slots
    .Where(s => slotIds.Contains(s.Id) && s.Status == "Active")
    .ToList();

if (!slotsToCancel.Any())
    throw new BusinessRuleException("No active slots found to cancel");
```

**Step 4: Validate Timing**
```csharp
if (reservation.Date.Date <= DateTime.UtcNow.Date)
    throw new BusinessRuleException("Cannot cancel same-day or past reservations");
```

**Step 5: Calculate Refund**
```csharp
var cancelledAmount = slotsToCancel.Sum(s => s.SlotPrice); // 30.00
var refundSettings = await _settingsService.GetRefundSettingsAsync();

// Check if refunds enabled
if (!refundSettings.EnableRefunds)
    throw new BusinessRuleException("Refunds are currently disabled");

// Check days until reservation
var daysUntilReservation = (reservation.Date - DateTime.UtcNow.Date).Days;
if (daysUntilReservation > refundSettings.MaxRefundDaysAdvance)
    throw new BusinessRuleException($"Cannot cancel more than {refundSettings.MaxRefundDaysAdvance} days in advance");

// Calculate refund with fee
var refundFeePercentage = refundSettings.RefundFeePercentage; // e.g., 10%
var refundFee = cancelledAmount * (refundFeePercentage / 100m); // 30 * 0.10 = 3.00
var netRefund = cancelledAmount - refundFee; // 30 - 3 = 27.00
```

**Step 6: Process Refund**
```csharp
if (reservation.PaymentId.HasValue)
{
    var payment = await _paymentService.GetPaymentByIdAsync(reservation.PaymentId.Value);
    if (payment != null)
    {
        await _paymentService.RefundPaymentAsync(
            reservation.PaymentId.Value,
            netRefund, // 27.00
            reservation.FacilityId
        );
    }
}
```

**Step 7: Cancel the Slots**

**Repository Layer:** `ReservationRepository.CancelReservationSlotsAsync()`

```csharp
using var transaction = await _context.Database.BeginTransactionAsync();

try
{
    var reservation = await _context.Reservations
        .Include(r => r.Slots)
        .FirstOrDefaultAsync(r => r.Id == reservationId);

    var slotsToCancel = reservation.Slots
        .Where(s => slotIds.Contains(s.Id) && s.Status == "Active")
        .ToList();

    // Cancel each slot
    foreach (var slot in slotsToCancel)
    {
        slot.Status = "Cancelled";
        slot.CancelledAt = DateTime.UtcNow;
        slot.CancellationReason = "User cancellation - Refund: $27.00";
    }

    // Update parent reservation
    var cancelledAmount = slotsToCancel.Sum(s => s.SlotPrice);
    reservation.RemainingPrice -= cancelledAmount; // 90 - 30 = 60
    reservation.UpdatedAt = DateTime.UtcNow;

    // Update parent status
    var activeSlots = reservation.Slots.Count(s => s.Status == "Active");
    if (activeSlots == 0)
        reservation.Status = "Cancelled";
    else if (activeSlots < reservation.Slots.Count)
        reservation.Status = "Partial"; // ✅ NEW STATUS

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

**Database Changes:**
```sql
-- 1. Cancel specific slot
UPDATE ReservationSlots
SET Status = 'Cancelled',
    CancelledAt = NOW(),
    CancellationReason = 'User cancellation - Refund: $27.00'
WHERE Id = 'slot-2';

-- 2. Update parent reservation
UPDATE Reservations
SET RemainingPrice = 60.00,  -- Was 90, now 60
    Status = 'Partial',      -- NEW STATUS!
    UpdatedAt = NOW()
WHERE Id = 'res-123';

-- 3. Payment refund
INSERT INTO Refunds (Id, PaymentId, Amount, Status, ...)
VALUES ('refund-1', 'payment-123', 27.00, 'COMPLETED', ...);
```

### Response:
```json
{
  "reservationId": "res-123",
  "cancelledSlots": [
    {
      "id": "slot-2",
      "timeSlot": "09:00-10:00",
      "price": 30.00,
      "status": "Cancelled"
    }
  ],
  "remainingSlots": [
    {
      "id": "slot-1",
      "timeSlot": "08:00-09:00",
      "price": 30.00,
      "status": "Active"
    },
    {
      "id": "slot-3",
      "timeSlot": "10:00-11:00",
      "price": 30.00,
      "status": "Active"
    }
  ],
  "originalTotal": 90.00,
  "remainingTotal": 60.00,
  "refundAmount": 27.00,
  "refundFee": 3.00,
  "refundPercentage": 90.0,
  "newStatus": "Partial"
}
```

### Final Database State:

**Reservations Table:**
```
Id: res-123
TotalPrice: 90.00
RemainingPrice: 60.00    ← Updated!
Status: Partial          ← Updated!
```

**ReservationSlots Table:**
```
slot-1: Active   ✅
slot-2: Cancelled ❌
slot-3: Active   ✅
```

---

## Flow 4: Full Cancellation (All Slots)

### API Call:
```http
POST /api/reservation/res-123/cancel-slots
Authorization: Bearer {token}

{
  "slotIds": ["slot-1", "slot-3"]  // Cancel remaining slots
}
```

### What Happens:
- Same process as partial cancellation
- Refund calculated for remaining slots: $54.00 (60 - 10% fee)
- All slots marked as "Cancelled"
- Reservation status updated to "Cancelled"

### Database State After Full Cancellation:

**Reservations Table:**
```
Id: res-123
TotalPrice: 90.00
RemainingPrice: 0.00     ← All cancelled
Status: Cancelled        ← All slots cancelled
```

**ReservationSlots Table:**
```
slot-1: Cancelled ❌
slot-2: Cancelled ❌
slot-3: Cancelled ❌
```

---

## Flow 5: Admin/Agent Reservation (Without Payment)

### API Call:
```http
POST /api/reservation/admin
Authorization: Bearer {token}
Roles: Business

{
  "facilityId": "facility-123",
  "userId": "user-1",
  "date": "2025-12-05",
  "timeSlots": ["14:00-15:00", "15:00-16:00"],
  "customPrice": 50.00,
  "notes": "VIP customer discount",
  "guestName": null,
  "guestPhone": null,
  "guestEmail": null
}
```

### What Happens:
- **No payment required** (admin override)
- Creates Reservation + ReservationSlots directly
- Can specify custom pricing
- Can create for guests (non-registered users)
- Tracks who created it (CreatedById)

### Database Changes:
```sql
-- Reservation
INSERT INTO Reservations (
    Id, FacilityId, UserId, Date, TimeSlots,
    TotalPrice, RemainingPrice, Status,
    PaymentId, CreatedById, Notes, CreatedAt, UpdatedAt
)
VALUES (
    'res-456', 'facility-123', 'user-1', '2025-12-05', '14:00-15:00,15:00-16:00',
    50.00, 50.00, 'Active',
    NULL, 'admin-user-1', 'VIP customer discount', NOW(), NOW()
);

-- ReservationSlots (2 records with custom pricing)
INSERT INTO ReservationSlots (Id, ReservationId, TimeSlot, SlotPrice, Status, CreatedAt)
VALUES
    ('slot-4', 'res-456', '14:00-15:00', 25.00, 'Active', NOW()),
    ('slot-5', 'res-456', '15:00-16:00', 25.00, 'Active', NOW());
```

---

## Flow 6: Group Reservation (Multiple Facilities)

### Scenario: User books 2 different courts for same time

**API Call:**
```http
POST /api/reservation/group
Authorization: Bearer {token}

{
  "facilityReservations": [
    {
      "facilityId": "court-1",
      "date": "2025-12-05",
      "timeSlots": ["10:00-11:00", "11:00-12:00"],
      "trainerProfileId": null
    },
    {
      "facilityId": "court-2",
      "date": "2025-12-05",
      "timeSlots": ["10:00-11:00", "11:00-12:00"],
      "trainerProfileId": null
    }
  ],
  "paymentId": "payment-789"
}
```

### What Happens:
- Creates multiple reservations with same `GroupId`
- Each reservation gets its own ReservationSlots
- Single payment for entire group
- Cancel group cancels all

### Database Changes:
```sql
-- Reservation 1 (Court 1)
INSERT INTO Reservations (..., GroupId) VALUES (..., 'group-1');
INSERT INTO ReservationSlots VALUES ('slot-6', 'res-701', '10:00-11:00', 30, 'Active', NOW());
INSERT INTO ReservationSlots VALUES ('slot-7', 'res-701', '11:00-12:00', 30, 'Active', NOW());

-- Reservation 2 (Court 2)
INSERT INTO Reservations (..., GroupId) VALUES (..., 'group-1');
INSERT INTO ReservationSlots VALUES ('slot-8', 'res-702', '10:00-11:00', 30, 'Active', NOW());
INSERT INTO ReservationSlots VALUES ('slot-9', 'res-702', '11:00-12:00', 30, 'Active', NOW());
```

---

## Key Database Tables Relationship

```
Reservations (Parent)
├─ Id: res-123
├─ TotalPrice: 90.00
├─ RemainingPrice: 60.00
├─ Status: Partial
├─ PaymentId: payment-123
└─ Slots ──────────┐
                   │
                   ├─ ReservationSlots (Children)
                   │  ├─ slot-1: 08:00-09:00, $30, Active
                   │  ├─ slot-2: 09:00-10:00, $30, Cancelled
                   │  └─ slot-3: 10:00-11:00, $30, Active
                   │
Payment            │
├─ Id: payment-123 │
├─ Amount: 90.00   │
└─ Status: COMPLETED
    │
    └─ Refunds
       └─ refund-1: $27.00
```

---

## Status Transitions

### Reservation Status:
```
Active  ──cancel some slots──> Partial ──cancel remaining──> Cancelled
  │                                                              ▲
  └──────────────cancel all slots directly──────────────────────┘
```

### ReservationSlot Status:
```
Active ──cancel──> Cancelled
```

### Payment Status:
```
PENDING ──webhook──> COMPLETED ──refund──> [COMPLETED + Refund record]
```

---

## Validation Rules

### Creating Reservation:
1. ✅ Payment must exist
2. ✅ Payment status must be "COMPLETED"
3. ✅ Payment not already consumed (except group reservations)
4. ✅ Payment amount must match reservation cost
5. ✅ Time slots must be available

### Partial Cancellation:
1. ✅ User must own reservation or be creator
2. ✅ Slots must exist and be "Active"
3. ✅ Cannot cancel same-day or past reservations
4. ✅ Refunds must be enabled (global setting)
5. ✅ Cannot cancel beyond max days advance (global setting)

---

## Performance Considerations

### Indexes Created:

**ReservationSlots:**
- `IX_ReservationSlots_ReservationId` - Fast lookup of slots by reservation
- `IX_ReservationSlots_TimeSlot` - Fast search by time slot
- `IX_ReservationSlots_Status` - Fast filtering by status

**Reservations:**
- `IX_Reservations_UserId` - Fast user reservation lookup
- `IX_Reservations_FacilityId` - Fast facility booking lookup
- `IX_Reservations_Date` - Fast date range queries
- `IX_Reservations_Status` - Fast status filtering

### Query Patterns:

**Good:**
```csharp
// Uses index on ReservationId
var slots = _context.ReservationSlots
    .Where(s => s.ReservationId == id)
    .ToListAsync();
```

**Good:**
```csharp
// Uses indexes on FacilityId and Date
var reservations = _context.Reservations
    .Where(r => r.FacilityId == facilityId && r.Date == date)
    .Include(r => r.Slots)
    .ToListAsync();
```

---

## Edge Cases Handled

### 1. Payment Already Used
```csharp
var existingReservation = _reservationRepository.GetReservationByPaymentId(paymentId);
if (existingReservation != null && existingReservation.GroupId == null)
    throw new ConflictException("A reservation already exists for this payment");
```

### 2. Partial Cancellation with No Active Slots
```csharp
var slotsToCancel = reservation.Slots
    .Where(s => slotIds.Contains(s.Id) && s.Status == "Active")
    .ToList();

if (!slotsToCancel.Any())
    throw new BusinessRuleException("No active slots found to cancel");
```

### 3. Transaction Rollback on Error
```csharp
using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    // Operations...
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 4. Same-Day Cancellation Prevention
```csharp
if (reservation.Date.Date <= DateTime.UtcNow.Date)
    throw new BusinessRuleException("Cannot cancel same-day or past reservations");
```

---

## Summary

The hybrid reservation system provides:

✅ **Flexible Bookings** - Book multiple hours in one reservation
✅ **Partial Cancellations** - Cancel individual hours with automatic refunds
✅ **Granular Tracking** - Each hour has its own status and audit trail
✅ **Transaction Safety** - Database transactions ensure data integrity
✅ **Payment Integration** - Full payment and refund workflow
✅ **Admin Controls** - Business owners can create reservations without payment
✅ **Group Bookings** - Book multiple facilities in one transaction
✅ **Status Management** - Clear status progression (Active → Partial → Cancelled)

The system is production-ready with proper validation, error handling, and performance optimization! 🚀
