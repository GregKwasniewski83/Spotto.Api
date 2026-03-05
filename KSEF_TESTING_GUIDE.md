# KSeF Integration Testing Guide

## Prerequisites Checklist

Before you start testing, ensure you have:

- [ ] PlaySpace.Api running (default port: 7125)
- [ ] PostgreSQL database running with migrations applied
- [ ] At least one Business Profile in the database with a valid NIP
- [ ] A registered user account with JWT token for API authentication
- [ ] KSeF test account credentials (we'll get this in Step 1)

---

## Step 1: Obtain KSeF Test Credentials

### Option A: Register for Real KSeF Test Account (Recommended)

1. **Go to KSeF Test Portal:**
   - URL: https://ksef-test.mf.gov.pl
   - This is the official Polish government test environment

2. **Register an Account:**
   - Click "Rejestracja" (Registration)
   - You'll need:
     - Test NIP (Polish tax ID) - You can use a test NIP like `1234567890`
     - Email address
     - Phone number

3. **Generate Test Token:**
   - After registration, log in to the portal
   - Go to Settings → API Access
   - Click "Generate Token"
   - Copy the token - you'll need this for configuration

### Option B: Use Mock Test Credentials (For Initial API Testing)

If you want to test the API endpoints first before getting real credentials:

```json
{
  "nip": "1234567890",
  "token": "test_token_12345",
  "environment": "Test"
}
```

**Note:** These won't work for actual invoice submission, but will let you test the API flow.

---

## Step 2: Verify API is Running

```bash
# Start the API if not already running
cd /mnt/c/Users/gkwas/source/repos/GrzegorzKwasniewski7/PlaySpace.Api
dotnet run --project PlaySpace.Api
```

The API should start on `http://localhost:7125` (or your configured port).

**Check the logs for:**
```
🚀 Starting Spotto API...
```

---

## Step 3: Get Your Business Profile ID

You need to know which business profile you'll use for testing.

### Query Database (Option 1):

```bash
# Connect to your PostgreSQL database
# Replace with your connection details
```

```sql
SELECT "Id", "CompanyName", "Nip", "KSeFEnabled", "KSeFToken", "KSeFEnvironment"
FROM "BusinessProfiles"
LIMIT 5;
```

### Via API (Option 2):

If you have an endpoint to list business profiles, use that. Otherwise, you should know the business profile ID from your user account.

**Example Business Profile ID to use:**
```
{businessProfileId} = <YOUR_BUSINESS_PROFILE_ID>
```

---

## Step 4: Get JWT Authentication Token

You need a valid JWT token to call the API endpoints.

**Sign in as a business owner:**

```bash
POST http://localhost:7125/api/auth/signin
Content-Type: application/json

{
  "email": "your_business_email@example.com",
  "password": "your_password"
}
```

**Save the token from response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": { ... }
}
```

**Set as environment variable (for easier testing):**
```bash
# Linux/Mac
export JWT_TOKEN="your_token_here"
export BUSINESS_ID="your_business_profile_id"

# Windows PowerShell
$env:JWT_TOKEN="your_token_here"
$env:BUSINESS_ID="your_business_profile_id"
```

---

## Step 5: Check Initial KSeF Configuration Status

**Request:**
```bash
GET http://localhost:7125/api/business-profile/{businessProfileId}/ksef/configuration
Authorization: Bearer {your_jwt_token}
```

**Using curl:**
```bash
curl -X GET "http://localhost:7125/api/business-profile/$BUSINESS_ID/ksef/configuration" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

**Expected Response (Not Configured):**
```json
{
  "isConfigured": false,
  "isEnabled": false,
  "environment": null,
  "registeredAt": null,
  "lastSyncAt": null,
  "statusMessage": "KSeF is not configured. Please configure your KSeF token first."
}
```

---

## Step 6: Configure KSeF Credentials

**Request:**
```bash
POST http://localhost:7125/api/business-profile/{businessProfileId}/ksef/configure
Authorization: Bearer {your_jwt_token}
Content-Type: application/json

{
  "token": "your_ksef_test_token_here",
  "environment": "Test"
}
```

**Using curl:**
```bash
curl -X POST "http://localhost:7125/api/business-profile/$BUSINESS_ID/ksef/configure" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "token": "your_ksef_test_token_here",
    "environment": "Test"
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "message": "KSeF credentials configured successfully"
}
```

---

## Step 7: Test KSeF Connection

This is the CRITICAL step - it verifies your credentials work with the KSeF API.

**Request:**
```bash
POST http://localhost:7125/api/business-profile/{businessProfileId}/ksef/test-connection
Authorization: Bearer {your_jwt_token}
```

**Using curl:**
```bash
curl -X POST "http://localhost:7125/api/business-profile/$BUSINESS_ID/ksef/test-connection" \
  -H "Authorization: Bearer $JWT_TOKEN"
```

**Expected Response (Success):**
```json
{
  "isSuccessful": true,
  "message": "Successfully connected to KSeF Test environment",
  "testedAt": "2026-01-10T10:30:00Z"
}
```

**Expected Response (Failure - Invalid Credentials):**
```json
{
  "isSuccessful": false,
  "message": "Failed to initialize session: Invalid token",
  "testedAt": "2026-01-10T10:30:00Z"
}
```

### What This Test Does:

1. Calls `POST /online/Session/AuthorisationChallenge` to KSeF
2. Signs the challenge with your token
3. Calls `POST /online/Session/InitToken` to get session token
4. Calls `GET /online/Session/Terminate` to close session
5. Returns success/failure

**Check the logs** (in your API console):
```
Initializing KSeF session for NIP: ...
KSeF session initialized successfully for NIP: ...
KSeF session closed successfully
```

---

## Step 8: Enable KSeF Auto-Invoicing

Once the connection test passes, enable auto-invoicing:

**Request:**
```bash
PUT http://localhost:7125/api/business-profile/{businessProfileId}/ksef/status
Authorization: Bearer {your_jwt_token}
Content-Type: application/json

{
  "enabled": true
}
```

**Using curl:**
```bash
curl -X PUT "http://localhost:7125/api/business-profile/$BUSINESS_ID/ksef/status" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"enabled": true}'
```

**Expected Response:**
```json
{
  "success": true,
  "message": "KSeF status updated successfully"
}
```

---

## Step 9: Verify Configuration is Complete

**Request:**
```bash
GET http://localhost:7125/api/business-profile/{businessProfileId}/ksef/configuration
Authorization: Bearer {your_jwt_token}
```

**Expected Response (Fully Configured):**
```json
{
  "isConfigured": true,
  "isEnabled": true,
  "environment": "Test",
  "registeredAt": "2026-01-10T10:25:00Z",
  "lastSyncAt": null,
  "statusMessage": "KSeF is configured and enabled for Test environment"
}
```

---

## Step 10: Create Test Payment to Trigger Invoice

Now the exciting part - create a test payment to see if invoices are generated automatically!

### Option A: Complete a Real Reservation Payment

1. Create a reservation via your normal workflow
2. Complete payment via TPay (or mark as completed)
3. Watch the logs for invoice generation

### Option B: Manually Trigger Invoice Creation (For Testing)

If you have access to the database, you can manually create a test payment:

```sql
-- Insert a test payment (adjust IDs to match your data)
INSERT INTO "Payments" (
  "Id", "UserId", "Amount", "Currency", "Status",
  "PaymentMethod", "CreatedAt", "UpdatedAt"
)
VALUES (
  gen_random_uuid(),
  'YOUR_USER_ID'::uuid,
  100.00,
  'PLN',
  'COMPLETED',
  'TPay',
  NOW(),
  NOW()
);

-- Then call the invoice creation service via API or directly
```

### Option C: Use TPay Test Webhook (Recommended)

Simulate a TPay payment completion webhook to trigger the full flow.

---

## Step 11: Monitor Invoice Generation

**Watch the API logs for:**

```
Creating invoice from payment...
Initializing KSeF session for NIP: {nip}
KSeF session initialized successfully
Generating FA XML...
Sending invoice to KSeF. XML length: {length}
Invoice sent to KSeF successfully. Reference: {reference_number}
KSeF session closed successfully
```

---

## Step 12: Check Invoice in Database

Query the database to see the generated invoice:

```sql
SELECT
  "Id",
  "InvoiceNumber",
  "Status",
  "KSeFStatus",
  "KSeFReferenceNumber",
  "KSeFSentAt",
  "KSeFErrorMessage",
  "GrossAmount"
FROM "KSeFInvoices"
ORDER BY "CreatedAt" DESC
LIMIT 5;
```

**Expected Results:**

| Status | KSeFReferenceNumber | KSeFStatus |
|--------|-------------------|-----------|
| Sent | KSEF-12345-... | Accepted/Pending |

**Possible Statuses:**
- `Pending` - Invoice created, not sent yet
- `Sent` - Sent to KSeF successfully
- `Accepted` - KSeF accepted the invoice
- `Error` - Something went wrong (check KSeFErrorMessage)
- `PendingBusinessKSeFSetup` - Business hasn't configured KSeF

---

## Step 13: Check Invoice Status in KSeF (Optional)

You can query the invoice status from KSeF:

**Via API:** Call your `CheckInvoiceStatusAsync` endpoint (if you expose it)

**Via Database:** The status is automatically updated when you query it

**Via KSeF Portal:**
1. Log in to https://ksef-test.mf.gov.pl
2. Go to "Moje faktury" (My invoices)
3. Find your invoice by reference number
4. Check status and download UPO (official receipt)

---

## Troubleshooting

### Issue: "Failed to initialize session: Invalid token"

**Cause:** KSeF credentials are incorrect

**Solution:**
1. Verify you're using the correct token from KSeF portal
2. Ensure the NIP in BusinessProfile matches the NIP used to generate the token
3. Check that you're using "Test" environment, not "Production"

---

### Issue: "Business has not configured KSeF token"

**Cause:** Step 6 (Configure Credentials) was skipped or failed

**Solution:**
1. Run Step 6 again
2. Check database to verify `KSeFToken` is stored:
```sql
SELECT "KSeFToken", "KSeFEnvironment"
FROM "BusinessProfiles"
WHERE "Id" = 'your_business_id';
```

---

### Issue: Invoice Status = "Error"

**Cause:** Various - check `KSeFErrorMessage` field

**Solution:**
1. Query the invoice to see error message:
```sql
SELECT "KSeFErrorMessage" FROM "KSeFInvoices" WHERE "Id" = 'invoice_id';
```
2. Common errors:
   - Invalid FA XML format → Check FAXmlGeneratorService
   - Network timeout → Check internet connection
   - Invalid credentials → Re-run connection test

---

### Issue: "FA XML validation failed"

**Cause:** Generated XML doesn't match FA(3) schema

**Solution:**
1. Check the invoice XML:
```sql
SELECT "InvoiceXML" FROM "KSeFInvoices" WHERE "Id" = 'invoice_id';
```
2. Validate against FA(3) schema
3. May need to update FAXmlGeneratorService.cs

---

## Testing Checklist

- [ ] API is running
- [ ] Have Business Profile ID
- [ ] Have JWT token
- [ ] KSeF test account registered
- [ ] Configuration endpoint works
- [ ] Credentials configured successfully
- [ ] Connection test PASSES
- [ ] Auto-invoicing enabled
- [ ] Test payment created
- [ ] Invoice generated in database
- [ ] Invoice status = "Sent" or "Accepted"
- [ ] KSeFReferenceNumber is populated
- [ ] Can see invoice in KSeF test portal

---

## Next Steps After Successful Testing

1. **Test Error Scenarios:**
   - Invalid credentials
   - Network failures
   - Invalid XML

2. **Test Different Invoice Types:**
   - Facility reservations
   - Product purchases
   - B2B (with buyer NIP)
   - B2C (without buyer NIP)

3. **Monitor Production Readiness:**
   - Ensure FA XML matches FA(3) schema completely
   - Review token signing algorithm (see TODO in KSeFApiService.cs)
   - Test with multiple businesses
   - Set up monitoring/alerting

4. **Before February 1, 2026:**
   - Switch from Test to Production environment
   - Update all businesses to use production tokens
   - Monitor first live invoices carefully

---

## Useful Commands for Testing

### Reset KSeF Configuration
```bash
# Disable KSeF
curl -X PUT "http://localhost:7125/api/business-profile/$BUSINESS_ID/ksef/status" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"enabled": false}'

# Reconfigure with new credentials
curl -X POST "http://localhost:7125/api/business-profile/$BUSINESS_ID/ksef/configure" \
  -H "Authorization: Bearer $JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "token": "new_token",
    "environment": "Test"
  }'
```

### View Recent Invoices
```sql
SELECT
  "InvoiceNumber",
  "Status",
  "KSeFReferenceNumber",
  "GrossAmount",
  "CreatedAt"
FROM "KSeFInvoices"
ORDER BY "CreatedAt" DESC
LIMIT 10;
```

---

## Support

If you encounter issues:

1. **Check API logs** - All KSeF operations are logged
2. **Check database** - Query KSeFInvoices for error messages
3. **Review documentation** - See KSEF_IMPLEMENTATION_COMPLETE.md
4. **KSeF Support:**
   - Test Portal: https://ksef-test.mf.gov.pl
   - Email: kontakt@ksef.mf.gov.pl
   - Phone: +48 22 330 00 00

Good luck with testing! 🚀
