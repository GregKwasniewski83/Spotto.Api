# 🧪 Testing Documentation

## 🎯 Testing Strategy

PlaySpace.Api testing ensures quality across all user interactions and business scenarios. Our testing approach covers API functionality, business logic, data integrity, and user experience validation.

## 🛠️ Test Environment Setup

### Prerequisites
```bash
# Required tools
- .NET 8 SDK
- PostgreSQL (local or Docker)
- Postman or similar API testing tool
- IDE (Visual Studio, VS Code, or Rider)
```

### Environment Configuration

#### Local Testing Environment
```bash
# Clone repository
git clone [repository-url]
cd PlaySpace.Api

# Restore packages
dotnet restore

# Setup test database
dotnet ef database update --project PlaySpace.Repositories --startup-project PlaySpace.Api

# Run application
dotnet run --project PlaySpace.Api
```

#### Test Database Setup
```json
// appsettings.Development.json
{
  "DatabaseOptions": {
    "ConnectionString": "Host=localhost;Database=playspace_test;Username=test_user;Password=test_password"
  }
}
```

### Test Data Seeding

The application includes automatic test data seeding via `DatabaseSeeder` service:

| Entity | Test Records | Purpose |
|--------|--------------|---------|
| **Users** | 10+ test users | Authentication & profile testing |
| **Facilities** | 5+ sports facilities | Booking & search testing |
| **Courts** | 20+ courts | Availability & reservation testing |
| **TimeSlots** | 100+ time slots | Booking workflow testing |
| **Trainers** | 5+ trainer profiles | Training service testing |

## 📋 Test Scenarios by Feature

### 🔐 Authentication & User Management

#### Test Case: User Registration
```gherkin
Scenario: Successful user registration
  Given I have valid user registration data
  When I POST to /api/auth/signup
  Then I receive a 201 Created response
  And the user is created in the database
  And I receive user details without password

Test Data:
{
  "email": "test@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}

Expected Response: 201 Created
{
  "id": "user-guid",
  "email": "test@example.com",
  "firstName": "John",
  "lastName": "Doe"
}
```

#### Test Case: User Authentication
```gherkin
Scenario: Successful user sign-in
  Given I have a registered user account
  When I POST valid credentials to /api/auth/signin
  Then I receive a 200 OK response
  And I receive a valid JWT token
  And the token contains user claims

Test Data:
{
  "email": "test@example.com",
  "password": "SecurePass123!"
}

Expected Response: 200 OK
{
  "token": "jwt-token-string",
  "user": { user-object },
  "expiresAt": "2024-01-01T00:00:00Z"
}
```

#### Negative Test Cases
| Scenario | Input | Expected Result |
|----------|-------|-----------------|
| **Invalid Email** | malformed email | 400 Bad Request |
| **Weak Password** | "123" | 400 Bad Request |
| **Duplicate Email** | existing email | 409 Conflict |
| **Wrong Password** | incorrect password | 401 Unauthorized |

### 🏢 Facility Management

#### Test Case: Facility Creation
```gherkin
Scenario: Business owner creates a facility
  Given I am authenticated as a business owner
  When I POST facility data to /api/facility
  Then I receive a 201 Created response
  And the facility is saved with correct details
  And the facility is linked to my business profile

Test Data:
{
  "name": "Downtown Sports Center",
  "address": "123 Main St, City",
  "description": "Modern sports facility",
  "amenities": ["Parking", "Changing Rooms", "Equipment Rental"]
}

Expected Response: 201 Created
{
  "id": "facility-guid",
  "name": "Downtown Sports Center",
  "address": "123 Main St, City",
  "businessProfileId": "business-guid"
}
```

#### Test Case: Facility Search
```gherkin
Scenario: User searches for facilities
  Given there are facilities in the database
  When I GET /api/search with location parameters
  Then I receive a 200 OK response
  And I get a list of matching facilities
  And results are sorted by distance

Query Parameters:
?location=city&sport=tennis&date=2024-01-01

Expected Response: 200 OK
[
  {
    "id": "facility-1",
    "name": "Tennis Club",
    "distance": 2.5,
    "availableSlots": 5
  }
]
```

### 📅 Booking System

#### Test Case: Court Reservation
```gherkin
Scenario: User books a court successfully
  Given I am authenticated as a user
  And there are available time slots
  When I POST reservation data to /api/reservation
  Then I receive a 201 Created response
  And the reservation is confirmed
  And the time slot is marked as booked

Test Data:
{
  "timeSlotId": "slot-guid",
  "userId": "user-guid",
  "notes": "Weekly tennis game"
}

Expected Response: 201 Created
{
  "id": "reservation-guid",
  "timeSlotId": "slot-guid",
  "status": "Confirmed",
  "totalAmount": 50.00
}
```

#### Booking Edge Cases
| Scenario | Condition | Expected Behavior |
|----------|-----------|-------------------|
| **Double Booking** | Slot already booked | 409 Conflict |
| **Past Date** | Historical time slot | 400 Bad Request |
| **Insufficient Funds** | Payment failure | 402 Payment Required |
| **Facility Closed** | Outside operating hours | 400 Bad Request |

### 💰 Payment Processing

#### Test Case: Payment Processing
```gherkin
Scenario: Successful payment for reservation
  Given I have a confirmed reservation
  When I POST payment data to /api/payment
  Then I receive a 200 OK response
  And the payment is processed
  And the reservation status is updated to "Paid"

Test Data:
{
  "reservationId": "reservation-guid",
  "amount": 50.00,
  "paymentMethod": "CreditCard",
  "cardToken": "stripe-token"
}

Expected Response: 200 OK
{
  "id": "payment-guid",
  "status": "Completed",
  "transactionId": "txn-12345"
}
```

### 🏃‍♂️ Training Services

#### Test Case: Trainer Profile Creation
```gherkin
Scenario: User creates trainer profile
  Given I am authenticated as a user
  When I POST trainer profile data to /api/trainerprofile
  Then I receive a 201 Created response
  And the trainer profile is created
  And I can offer training services

Test Data:
{
  "specializations": ["Tennis", "Fitness"],
  "experience": "5 years professional coaching",
  "hourlyRate": 75.00,
  "certifications": ["USPTA Certified"]
}
```

## 🔍 API Testing Checklist

### Pre-Test Setup
- [ ] Test database is clean and seeded
- [ ] Application is running on test environment
- [ ] Test user accounts are available
- [ ] Payment gateway is in test mode

### Authentication Tests
- [ ] User registration with valid data
- [ ] User registration with invalid data
- [ ] User sign-in with correct credentials
- [ ] User sign-in with incorrect credentials
- [ ] JWT token validation
- [ ] Token expiration handling

### Authorization Tests
- [ ] Protected endpoints require authentication
- [ ] Role-based access control
- [ ] User can only access own data
- [ ] Business owner can manage own facilities
- [ ] Admin access to all resources

### Data Validation Tests
- [ ] Required fields validation
- [ ] Data type validation
- [ ] String length limits
- [ ] Email format validation
- [ ] Date range validation

### Business Logic Tests
- [ ] Booking availability calculation
- [ ] Payment amount calculation
- [ ] Facility capacity limits
- [ ] Trainer schedule conflicts
- [ ] Search result accuracy

### Error Handling Tests
- [ ] 400 Bad Request for invalid input
- [ ] 401 Unauthorized for missing auth
- [ ] 403 Forbidden for insufficient permissions
- [ ] 404 Not Found for missing resources
- [ ] 409 Conflict for business rule violations
- [ ] 500 Internal Server Error handling

## 📊 Test Data Management

### Test User Accounts
| Role | Email | Password | Purpose |
|------|-------|----------|---------|
| **Regular User** | user@test.com | TestPass123! | Basic booking scenarios |
| **Business Owner** | business@test.com | TestPass123! | Facility management |
| **Trainer** | trainer@test.com | TestPass123! | Training services |
| **Admin** | admin@test.com | TestPass123! | Administrative functions |

### Test Facilities
| Name | Location | Courts | Purpose |
|------|----------|--------|---------|
| **City Tennis Club** | Downtown | 4 tennis courts | Tennis booking tests |
| **Sports Complex** | Uptown | 6 multi-purpose | Multi-sport scenarios |
| **Fitness Center** | Suburbs | 2 studios | Training session tests |

## 🚀 Automated Testing

### Unit Tests
```bash
# Run unit tests
dotnet test PlaySpace.Tests.Unit

# Test categories
- Service layer tests
- Repository tests
- Validation tests
- Utility function tests
```

### Integration Tests
```bash
# Run integration tests
dotnet test PlaySpace.Tests.Integration

# Test scenarios
- API endpoint tests
- Database operations
- Authentication flow
- Payment processing
```

### Performance Tests
| Scenario | Target | Measurement |
|----------|--------|-------------|
| **API Response Time** | <200ms | Average response time |
| **Database Queries** | <100ms | Query execution time |
| **Concurrent Users** | 100+ | Simultaneous requests |
| **Search Performance** | <500ms | Complex search queries |

## 📝 Test Reporting

### Test Execution Report Template
```
Test Execution Summary
=====================

Environment: [Test/Staging/Production]
Date: [Execution Date]
Tester: [Tester Name]

Results Summary:
- Total Tests: [Number]
- Passed: [Number]
- Failed: [Number]
- Skipped: [Number]

Failed Tests:
[List of failed test cases with details]

Issues Found:
[Bugs discovered during testing]

Notes:
[Additional observations]
```

### Bug Report Template
```
Bug Report
==========

Title: [Brief description]
Priority: [High/Medium/Low]
Environment: [Test environment]

Steps to Reproduce:
1. [Step 1]
2. [Step 2]
3. [Step 3]

Expected Result:
[What should happen]

Actual Result:
[What actually happened]

API Details:
- Endpoint: [URL]
- Method: [GET/POST/PUT/DELETE]
- Request: [JSON payload]
- Response: [Response received]

Screenshots/Logs:
[Attach relevant files]
```

---

> **For QA Teams**: Use this as your primary testing guide
> **For Developers**: Reference test scenarios during implementation
> **For Business Analysts**: Validate business rules through test cases
> **For Product Managers**: Review test coverage for feature acceptance