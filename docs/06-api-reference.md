# 📱 API Reference

## 🔧 Base Configuration

### Base URL
- **Development**: `https://localhost:7125`
- **Production**: `https://your-domain.com`

### Authentication
All protected endpoints require JWT Bearer token in the Authorization header:
```
Authorization: Bearer <jwt-token>
```

### Content Type
All request bodies should be `application/json` unless specified otherwise.

### Response Format
All responses follow a consistent structure:
```json
{
  "data": {...},
  "success": true,
  "message": "Operation completed successfully",
  "errors": []
}
```

## 🔐 Authentication & Authorization

### POST /api/auth/signup
Register a new user account.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890"
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request`: Invalid input data
- `409 Conflict`: Email already exists

---

### POST /api/auth/signin
Authenticate user and receive JWT token.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2024-01-01T12:00:00Z",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }
}
```

**Error Responses:**
- `401 Unauthorized`: Invalid credentials
- `400 Bad Request`: Missing or invalid input

## 👤 User Management

### GET /api/user/{id}
🔒 **Protected** - Get user profile by ID.

**Parameters:**
- `id` (path): User GUID

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "phoneNumber": "+1234567890",
  "profilePicture": "https://example.com/profile.jpg",
  "createdAt": "2024-01-01T00:00:00Z",
  "updatedAt": "2024-01-01T00:00:00Z"
}
```

---

### PUT /api/user/{id}
🔒 **Protected** - Update user profile.

**Parameters:**
- `id` (path): User GUID

**Request Body:**
```json
{
  "firstName": "John",
  "lastName": "Smith",
  "phoneNumber": "+1234567890",
  "profilePicture": "base64-encoded-image"
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "firstName": "John",
  "lastName": "Smith",
  "phoneNumber": "+1234567890",
  "updatedAt": "2024-01-01T00:00:00Z"
}
```

## 🏢 Facility Management

### GET /api/facility
Get list of all facilities with optional filtering.

**Query Parameters:**
- `location` (string): Filter by city/area
- `sport` (string): Filter by sport type
- `page` (int): Page number (default: 1)
- `pageSize` (int): Items per page (default: 10)

**Response (200 OK):**
```json
{
  "facilities": [
    {
      "id": "facility-guid",
      "name": "Downtown Tennis Club",
      "address": "123 Main St, City",
      "description": "Premium tennis facility",
      "amenities": ["Parking", "Changing Rooms", "Pro Shop"],
      "rating": 4.5,
      "priceRange": "$$",
      "distance": 2.3,
      "availableCourts": 3,
      "images": ["image1.jpg", "image2.jpg"]
    }
  ],
  "totalCount": 25,
  "page": 1,
  "pageSize": 10
}
```

---

### POST /api/facility
🔒 **Protected** - Create new facility (Business owners only).

**Request Body:**
```json
{
  "name": "Sports Center",
  "address": "456 Oak Ave, City",
  "description": "Modern multi-sport facility",
  "amenities": ["Parking", "Changing Rooms", "Equipment Rental"],
  "operatingHours": {
    "monday": { "open": "06:00", "close": "22:00" },
    "tuesday": { "open": "06:00", "close": "22:00" }
  },
  "contactEmail": "info@sportscenter.com",
  "contactPhone": "+1234567890"
}
```

**Response (201 Created):**
```json
{
  "id": "facility-guid",
  "name": "Sports Center",
  "address": "456 Oak Ave, City",
  "businessProfileId": "business-guid",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

---

### GET /api/facility/{id}
Get detailed facility information.

**Parameters:**
- `id` (path): Facility GUID

**Response (200 OK):**
```json
{
  "id": "facility-guid",
  "name": "Downtown Tennis Club",
  "address": "123 Main St, City",
  "description": "Premium tennis facility with 4 courts",
  "amenities": ["Parking", "Changing Rooms", "Pro Shop"],
  "operatingHours": {
    "monday": { "open": "06:00", "close": "22:00" }
  },
  "courts": [
    {
      "id": "court-guid",
      "name": "Court 1",
      "sport": "Tennis",
      "surface": "Hard Court",
      "hourlyRate": 25.00
    }
  ],
  "reviews": [
    {
      "id": "review-guid",
      "userId": "user-guid",
      "rating": 5,
      "comment": "Great facility!",
      "createdAt": "2024-01-01T00:00:00Z"
    }
  ],
  "images": ["facility1.jpg", "facility2.jpg"],
  "rating": 4.5,
  "totalReviews": 127
}
```

## 📅 Booking & Reservations

### GET /api/timeslot
Get available time slots for booking.

**Query Parameters:**
- `facilityId` (guid): Facility ID
- `courtId` (guid): Specific court ID
- `date` (date): Date for availability (YYYY-MM-DD)
- `sport` (string): Sport type filter

**Response (200 OK):**
```json
{
  "timeSlots": [
    {
      "id": "slot-guid",
      "facilityId": "facility-guid",
      "courtId": "court-guid",
      "startTime": "2024-01-01T10:00:00Z",
      "endTime": "2024-01-01T11:00:00Z",
      "price": 25.00,
      "isAvailable": true,
      "court": {
        "name": "Court 1",
        "sport": "Tennis"
      }
    }
  ]
}
```

---

### POST /api/reservation
🔒 **Protected** - Create a new booking reservation.

**Request Body:**
```json
{
  "timeSlotId": "slot-guid",
  "notes": "Weekly tennis game",
  "participants": [
    {
      "userId": "user-guid",
      "role": "Player"
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "id": "reservation-guid",
  "timeSlotId": "slot-guid",
  "userId": "user-guid",
  "status": "Confirmed",
  "totalAmount": 25.00,
  "notes": "Weekly tennis game",
  "createdAt": "2024-01-01T00:00:00Z",
  "timeSlot": {
    "startTime": "2024-01-01T10:00:00Z",
    "endTime": "2024-01-01T11:00:00Z",
    "facility": {
      "name": "Downtown Tennis Club",
      "address": "123 Main St"
    }
  }
}
```

---

### GET /api/reservation/user/{userId}
🔒 **Protected** - Get user's reservations.

**Parameters:**
- `userId` (path): User GUID

**Query Parameters:**
- `status` (string): Filter by status (Confirmed, Cancelled, Completed)
- `fromDate` (date): Start date filter
- `toDate` (date): End date filter

**Response (200 OK):**
```json
{
  "reservations": [
    {
      "id": "reservation-guid",
      "status": "Confirmed",
      "totalAmount": 25.00,
      "timeSlot": {
        "startTime": "2024-01-01T10:00:00Z",
        "endTime": "2024-01-01T11:00:00Z",
        "facility": {
          "name": "Tennis Club",
          "address": "123 Main St"
        },
        "court": {
          "name": "Court 1"
        }
      },
      "createdAt": "2024-01-01T00:00:00Z"
    }
  ]
}
```

---

### PUT /api/reservation/{id}
🔒 **Protected** - Update or cancel reservation.

**Parameters:**
- `id` (path): Reservation GUID

**Request Body:**
```json
{
  "status": "Cancelled",
  "notes": "Unable to attend due to weather"
}
```

**Response (200 OK):**
```json
{
  "id": "reservation-guid",
  "status": "Cancelled",
  "refundAmount": 25.00,
  "updatedAt": "2024-01-01T00:00:00Z"
}
```

## 💰 Payment Processing

### POST /api/payment
🔒 **Protected** - Process payment for reservation.

**Request Body:**
```json
{
  "reservationId": "reservation-guid",
  "amount": 25.00,
  "paymentMethod": "CreditCard",
  "cardToken": "stripe-payment-token",
  "savePaymentMethod": true
}
```

**Response (200 OK):**
```json
{
  "id": "payment-guid",
  "reservationId": "reservation-guid",
  "amount": 25.00,
  "status": "Completed",
  "transactionId": "txn_12345",
  "paymentMethod": "CreditCard",
  "processedAt": "2024-01-01T00:00:00Z"
}
```

---

### GET /api/payment/user/{userId}
🔒 **Protected** - Get user's payment history.

**Parameters:**
- `userId` (path): User GUID

**Response (200 OK):**
```json
{
  "payments": [
    {
      "id": "payment-guid",
      "amount": 25.00,
      "status": "Completed",
      "description": "Court booking - Tennis Club",
      "processedAt": "2024-01-01T00:00:00Z",
      "reservation": {
        "id": "reservation-guid",
        "facility": "Tennis Club"
      }
    }
  ]
}
```

## 🏃‍♂️ Training Services

### POST /api/trainerprofile
🔒 **Protected** - Create trainer profile.

**Request Body:**
```json
{
  "specializations": ["Tennis", "Fitness Training"],
  "experience": "5 years professional coaching",
  "certifications": ["USPTA Certified", "CPR Certified"],
  "hourlyRate": 75.00,
  "bio": "Professional tennis coach with tournament experience",
  "availability": [
    {
      "dayOfWeek": "Monday",
      "startTime": "09:00",
      "endTime": "17:00"
    }
  ]
}
```

**Response (201 Created):**
```json
{
  "id": "trainer-guid",
  "userId": "user-guid",
  "specializations": ["Tennis", "Fitness Training"],
  "hourlyRate": 75.00,
  "rating": 0,
  "totalSessions": 0,
  "createdAt": "2024-01-01T00:00:00Z"
}
```

---

### GET /api/search/trainers
Search for trainers by location and specialization.

**Query Parameters:**
- `location` (string): City or area
- `sport` (string): Sport specialization
- `minRating` (number): Minimum rating filter
- `maxRate` (number): Maximum hourly rate
- `date` (date): Availability date

**Response (200 OK):**
```json
{
  "trainers": [
    {
      "id": "trainer-guid",
      "user": {
        "firstName": "Jane",
        "lastName": "Smith",
        "profilePicture": "trainer.jpg"
      },
      "specializations": ["Tennis"],
      "hourlyRate": 75.00,
      "rating": 4.8,
      "totalSessions": 150,
      "bio": "Professional tennis coach",
      "distance": 3.2,
      "nextAvailable": "2024-01-01T10:00:00Z"
    }
  ]
}
```

---

### POST /api/training
🔒 **Protected** - Book training session.

**Request Body:**
```json
{
  "trainerId": "trainer-guid",
  "dateTime": "2024-01-01T10:00:00Z",
  "duration": 60,
  "sessionType": "Individual",
  "notes": "Focus on serve technique",
  "facilityId": "facility-guid"
}
```

**Response (201 Created):**
```json
{
  "id": "training-guid",
  "trainerId": "trainer-guid",
  "userId": "user-guid",
  "dateTime": "2024-01-01T10:00:00Z",
  "duration": 60,
  "totalAmount": 75.00,
  "status": "Scheduled",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

## 🔍 Search & Discovery

### GET /api/search
Global search for facilities, trainers, and services.

**Query Parameters:**
- `query` (string): Search term
- `type` (string): Filter by type (facilities, trainers, all)
- `location` (string): Location filter
- `latitude` (number): GPS latitude
- `longitude` (number): GPS longitude
- `radius` (number): Search radius in km

**Response (200 OK):**
```json
{
  "results": {
    "facilities": [
      {
        "id": "facility-guid",
        "name": "Tennis Club",
        "type": "facility",
        "relevanceScore": 0.95,
        "distance": 2.1
      }
    ],
    "trainers": [
      {
        "id": "trainer-guid",
        "name": "Jane Smith",
        "type": "trainer",
        "specializations": ["Tennis"],
        "relevanceScore": 0.87
      }
    ]
  },
  "totalResults": 15
}
```

## 👥 Social Features

### GET /api/socialwallpost
Get social wall posts (community feed).

**Query Parameters:**
- `page` (int): Page number
- `pageSize` (int): Posts per page
- `userId` (guid): Filter by specific user

**Response (200 OK):**
```json
{
  "posts": [
    {
      "id": "post-guid",
      "userId": "user-guid",
      "content": "Great tennis session today!",
      "images": ["post1.jpg"],
      "createdAt": "2024-01-01T00:00:00Z",
      "user": {
        "firstName": "John",
        "lastName": "Doe",
        "profilePicture": "profile.jpg"
      },
      "likes": 12,
      "comments": 3,
      "isLikedByCurrentUser": false
    }
  ]
}
```

---

### POST /api/socialwallpost
🔒 **Protected** - Create new social post.

**Request Body:**
```json
{
  "content": "Amazing workout session today! #tennis #fitness",
  "images": ["base64-image1", "base64-image2"],
  "facilityId": "facility-guid",
  "activityType": "Tennis"
}
```

**Response (201 Created):**
```json
{
  "id": "post-guid",
  "userId": "user-guid",
  "content": "Amazing workout session today! #tennis #fitness",
  "images": ["post1.jpg", "post2.jpg"],
  "createdAt": "2024-01-01T00:00:00Z"
}
```

## 🏥 Health & Monitoring

### GET /api/health
System health check endpoint.

**Response (200 OK):**
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "external_services": "Healthy"
  },
  "totalDuration": "00:00:00.1234567"
}
```

## 📊 Error Responses

### Standard Error Format
```json
{
  "success": false,
  "message": "Error description",
  "errors": [
    {
      "field": "email",
      "message": "Email is required"
    }
  ],
  "statusCode": 400
}
```

### HTTP Status Codes
| Code | Description | Usage |
|------|-------------|-------|
| **200** | OK | Successful GET, PUT requests |
| **201** | Created | Successful POST requests |
| **400** | Bad Request | Invalid input data |
| **401** | Unauthorized | Missing or invalid authentication |
| **403** | Forbidden | Insufficient permissions |
| **404** | Not Found | Resource doesn't exist |
| **409** | Conflict | Business rule violation |
| **422** | Unprocessable Entity | Validation errors |
| **500** | Internal Server Error | Server-side errors |

---

> **For Frontend Developers**: Use this reference for API integration
> **For Mobile Developers**: All endpoints support JSON format
> **For Testers**: Use example requests for API testing
> **For Integration Partners**: Authentication and error handling guidelines included