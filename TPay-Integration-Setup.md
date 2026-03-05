# TPay Integration Setup Guide

## Configuration

### 1. Add TPay Configuration to appsettings.json

```json
{
  "TPayConfiguration": {
    "MerchantId": "your-merchant-id",
    "ApiKey": "your-api-key", 
    "ApiPassword": "your-api-password",
    "BaseUrl": "https://openapi.sandbox.tpay.com",
    "NotificationUrl": "https://your-domain.com/api/payment/tpay-notification",
    "ReturnUrl": "https://your-domain.com/payment/success",
    "IsSandbox": true
  }
}
```

### 2. Register Services in Program.cs

```csharp
// Add TPay configuration
builder.Services.Configure<TPayConfiguration>(
    builder.Configuration.GetSection("TPayConfiguration"));

// Register HTTP client for TPay
builder.Services.AddHttpClient<ITPayService, TPayService>();

// Register services
builder.Services.AddScoped<ITPayService, TPayService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
```

## Usage Flow

### 1. Create Payment Request

```http
POST /api/payment/create
{
  "userId": "user-guid",
  "amount": 100.00,
  "description": "Training session payment",
  "customerEmail": "user@example.com",
  "customerName": "John Doe",
  "customerPhone": "+48123456789",
  "returnUrl": "https://your-app.com/payment/success",
  "errorUrl": "https://your-app.com/payment/error"
}
```

### 2. Response with Payment URL

```json
{
  "id": "payment-guid",
  "tPayTransactionId": "tr12345",
  "tPayPaymentUrl": "https://secure.sandbox.tpay.com/pay?token=abc123",
  "status": "PENDING"
}
```

### 3. Redirect User to TPay

Redirect user to `tPayPaymentUrl` from the response.

### 4. Handle Notification

TPay will send POST notification to `/api/payment/tpay-notification` when payment is completed.

## Database Migration

You'll need to add the new TPay fields to your Payment table:

```sql
ALTER TABLE Payments ADD COLUMN TPayTransactionId NVARCHAR(255);
ALTER TABLE Payments ADD COLUMN TPayPaymentUrl NVARCHAR(500);
ALTER TABLE Payments ADD COLUMN TPayStatus NVARCHAR(50);
ALTER TABLE Payments ADD COLUMN TPayCompletedAt DATETIME2;
ALTER TABLE Payments ADD COLUMN TPayErrorMessage NVARCHAR(500);
ALTER TABLE Payments ADD COLUMN PaymentMethod NVARCHAR(50);
```

## Security Notes

1. Store your TPay credentials securely (use Azure Key Vault in production)
2. Always validate webhook notifications using MD5 hash
3. Use HTTPS for all webhook endpoints
4. Implement proper logging for payment events
5. Consider rate limiting on payment endpoints

## Testing

1. Register for TPay Sandbox account
2. Use sandbox credentials in development
3. Test different payment scenarios (success, failure, pending)
4. Verify webhook notifications are received correctly

## Next Steps

1. Implement PaymentRepository async methods
2. Add database migration for new Payment fields
3. Configure TPay credentials
4. Test integration in sandbox environment
5. Add proper error handling and logging
6. Consider implementing payment status polling as backup