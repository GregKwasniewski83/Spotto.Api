# Push Notifications Setup Guide

This guide explains how to configure Expo push notifications for the PlaySpace API.

## Backend Implementation

The backend now supports:
- **Auto-reservation creation** when payments complete
- **Real-time push notifications** eliminating the need for polling
- **Fallback mechanisms** for error handling

## Expo Push Notifications Setup

### 1. Expo App Configuration

Your mobile app must be configured to use Expo push notifications and generate Expo push tokens in the format: `ExponentPushToken[...]`

### 2. FCM Server Key (Required for Android)

⚠️ **Important**: For Android push notifications to work, you must configure an FCM server key in your Expo app configuration.

1. Go to [Firebase Console](https://console.firebase.google.com/)
2. Create a new project or use existing
3. Go to Project Settings → Cloud Messaging
4. Copy the **Server key** (legacy)
5. In your Expo app configuration (`app.json` or `app.config.js`), add:

```json
{
  "expo": {
    "android": {
      "googleServicesFile": "./google-services.json"
    },
    "plugins": [
      [
        "expo-notifications",
        {
          "icon": "./assets/notification-icon.png",
          "color": "#ffffff"
        }
      ]
    ]
  }
}
```

### 3. Backend Configuration

No additional backend configuration is required. The service uses Expo's push notification API directly.

## Database Migration

Add these fields to your Payment table:

```sql
ALTER TABLE payments ADD COLUMN push_token VARCHAR(255);
ALTER TABLE payments ADD COLUMN notification_id VARCHAR(255);
ALTER TABLE payments ADD COLUMN reservation_details TEXT;
```

## New API Flow

### Enhanced Payment Creation

```http
POST /api/payment/create
Content-Type: application/json

{
  "amount": 100.00,
  "userDetails": {
    "customerEmail": "user@example.com",
    "customerName": "John Doe",
    "customerPhone": "+48123456789"
  },
  "facilityReservation": {
    "facilityId": "facility-guid",
    "date": "2024-12-05",
    "timeSlots": ["14:00", "15:00"],
    "trainerProfileId": "trainer-guid"
  },
  "pushToken": "ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]",
  "returnUrl": "https://app.playspace.pl/payment/success",
  "errorUrl": "https://app.playspace.pl/payment/error",
  "description": "Sports Facility Booking"
}
```

**Response:**
```json
{
  "paymentId": "payment-guid",
  "tPayPaymentUrl": "https://secure.tpay.com/payment/...",
  "notificationId": "notification-guid"
}
```

### Automatic Processing

When TPay webhook confirms payment:
1. **Payment status** → Updated to "COMPLETED"
2. **Auto-reservation** → Created automatically if reservation details provided
3. **Push notification** → Sent to user with deep link to reservation
4. **No polling needed** → User gets instant notification

## Push Notification Types

### Payment Completed (with auto-reservation)
```json
{
  "title": "Payment Successful! 💳",
  "body": "Your reservation has been confirmed! 🎉",
  "data": {
    "type": "PAYMENT_COMPLETED",
    "paymentId": "payment-guid",
    "reservationId": "reservation-guid",
    "deepLink": "playspace://reservation/reservation-guid"
  }
}
```

### Payment Completed (manual reservation)
```json
{
  "title": "Payment Successful! 💳", 
  "body": "Payment successful! You can now create your reservation.",
  "data": {
    "type": "PAYMENT_COMPLETED",
    "paymentId": "payment-guid",
    "deepLink": "playspace://payment/payment-guid"
  }
}
```

### Auto-reservation Failed
```json
{
  "title": "Reservation Creation Failed",
  "body": "Please create your reservation manually in the app.",
  "data": {
    "type": "AUTO_RESERVATION_FAILED",
    "paymentId": "payment-guid"
  }
}
```

## Benefits

✅ **99% reduction** in API calls (no more polling)  
✅ **Instant feedback** when payment completes  
✅ **Better UX** with automatic reservation creation  
✅ **Battery friendly** for mobile devices  
✅ **Works offline** - push delivered when back online  
✅ **Deep linking** - direct navigation to confirmation  

## Error Handling

- **Push delivery failed** → App can poll once when returning to foreground
- **Auto-reservation failed** → Different push notification to retry manually
- **No push token** → Falls back to current polling approach
- **Webhook missed** → Scheduled job checks pending payments every 5 minutes

## Testing

### Test Push Notification

Use the custom notification endpoint:

```http
POST /api/payment/test-push
Content-Type: application/json

{
  "pushToken": "ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]",
  "title": "Test Notification",
  "body": "Testing push notification system"
}
```

### Monitor Logs

Check Seq logs for push notification delivery status:
- `Expo notification sent successfully. ID: xxx`
- `Expo notification failed. Status: error, Message: Unable to retrieve FCM server key...`
- `Failed to send push notification` (with error details)

### Common Error: "Unable to retrieve the FCM server key"

If you see this error:
```
Unable to retrieve the FCM server key for the recipient's app. Make sure you have provided a server key as directed by the Expo FCM documentation.
```

**Solution:**
1. Your Expo app needs an FCM server key configured
2. Follow the [Expo FCM setup guide](https://docs.expo.dev/push-notifications/using-fcm/)
3. Add the FCM server key to your Expo project settings
4. Rebuild your mobile app with the FCM configuration

## Security Notes

- FCM server keys should be secured in Expo project settings, not in your app code
- Expo push tokens should be validated before sending notifications  
- Rate limiting should be implemented for push notification endpoints
- Personal data should not be included in notification payloads

## Mobile App Integration

The mobile app needs to:
1. **Get Expo push token** on app startup using `Notifications.getExpoPushTokenAsync()`
2. **Include token** in payment creation requests  
3. **Configure FCM server key** in Expo project settings for Android notifications
4. **Handle push notifications** both foreground and background
5. **Implement deep linking** for navigation
6. **Remove polling logic** from payment flow