# KSeF Integration Guide

## Overview

This guide explains the KSeF (Polish National e-Invoice System) integration in PlaySpace.Api and how to complete the implementation.

## Architecture

### ✅ What's Implemented

1. **Per-Business Credentials Architecture**
   - Each business stores their own KSeF credentials (NIP, Token, Environment)
   - Legally compliant - invoices submitted using business's own credentials
   - Seller NIP matches submitter credentials

2. **Database Schema**
   - `BusinessProfiles` table has KSeF fields
   - `KSeFInvoices` table stores all invoice data
   - Migration: `20260109120000_AddKSeFFieldsToBusinessProfile.cs`

3. **Service Layer Architecture**
   - `IKSeFApiService` - Wrapper for KSEFAPI.KSEFAPIClient library
   - `IFAXmlGeneratorService` - Generates FA (Faktura) XML format
   - `IKSeFInvoiceService` - Main invoice management service

4. **Invoice Flow**
   ```
   Payment Completed (TPay webhook)
     ↓
   Check BusinessProfile.KSeFEnabled
     ↓
   Check BusinessProfile.KSeFToken exists
     ↓
   Generate FA XML
     ↓
   Submit to KSeF using BUSINESS credentials
     ↓
   Store KSeF reference number
   ```

5. **API Endpoints**
   - `GET /api/business-profile/{id}/ksef/configuration` - Get KSeF status
   - `POST /api/business-profile/{id}/ksef/configure` - Configure credentials
   - `PUT /api/business-profile/{id}/ksef/status` - Enable/disable
   - `POST /api/business-profile/{id}/ksef/test-connection` - Test connection

### ⚠️ What Needs Implementation

1. **KSEFAPI.KSEFAPIClient Integration**
   - Complete the stub methods in `KSeFApiService.cs`
   - Implement actual API calls to KSeF endpoints

2. **FA(3) XML Schema Compliance**
   - Update `FAXmlGeneratorService.cs` with full FA(3) schema
   - Follow official Polish Ministry of Finance schema

3. **Testing**
   - Test with KSeF test environment
   - Validate XML against official schema

---

## How to Complete the Integration

### Step 1: Study the KSEFAPI.KSEFAPIClient Library

The NuGet package `KSEFAPI.KSEFAPIClient` v1.2.4 is already installed.

**Resources:**
- GitHub: https://github.com/ksefapi/ksefapi-cs-client
- Documentation: https://ksef-test.mf.gov.pl/docs/v2/index.html
- Integration Guide: Published by Polish Ministry of Finance (June 2025)

**Files to Update:**
- `/PlaySpace.Services/Services/KSeFApiService.cs`

### Step 2: Implement KSeF API Methods

Update these methods in `KSeFApiService.cs`:

#### A. `InitializeSessionAsync()`

```csharp
public async Task<KSeFSessionResult> InitializeSessionAsync(string nip, string token, string environment)
{
    var apiUrl = environment == "Production"
        ? _ksefOptions.ProductionApiUrl
        : _ksefOptions.TestApiUrl;

    // TODO: Use KSEFAPI library
    // Example (adjust based on actual library API):
    var ksefClient = new KSefClient(apiUrl);
    var sessionResponse = await ksefClient.AuthorizeAsync(nip, token);

    if (sessionResponse.Success)
    {
        return new KSeFSessionResult
        {
            Success = true,
            SessionToken = sessionResponse.SessionToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_ksefOptions.SessionExpirationMinutes)
        };
    }

    return new KSeFSessionResult
    {
        Success = false,
        ErrorMessage = sessionResponse.ErrorMessage
    };
}
```

#### B. `SendInvoiceAsync()`

```csharp
public async Task<KSeFInvoiceSubmissionResult> SendInvoiceAsync(string sessionToken, string invoiceXml)
{
    // TODO: Submit invoice to KSeF
    var ksefClient = new KSefClient();
    ksefClient.SetSessionToken(sessionToken);

    var submitResponse = await ksefClient.SubmitInvoiceAsync(invoiceXml);

    if (submitResponse.Success)
    {
        return new KSeFInvoiceSubmissionResult
        {
            Success = true,
            KSeFReferenceNumber = submitResponse.ReferenceNumber,
            Status = "Sent",
            SubmittedAt = DateTime.UtcNow
        };
    }

    return new KSeFInvoiceSubmissionResult
    {
        Success = false,
        ErrorMessage = submitResponse.ErrorMessage,
        ErrorCode = submitResponse.ErrorCode,
        SubmittedAt = DateTime.UtcNow
    };
}
```

#### C. `CheckInvoiceStatusAsync()`

```csharp
public async Task<KSeFInvoiceStatusResult> CheckInvoiceStatusAsync(string sessionToken, string ksefReferenceNumber)
{
    // TODO: Check invoice status in KSeF
    var ksefClient = new KSefClient();
    ksefClient.SetSessionToken(sessionToken);

    var statusResponse = await ksefClient.GetInvoiceStatusAsync(ksefReferenceNumber);

    if (statusResponse.Success)
    {
        return new KSeFInvoiceStatusResult
        {
            Success = true,
            Status = statusResponse.Status, // "Accepted", "Rejected", "Pending"
            UPO = statusResponse.UPO, // Official receipt
            ProcessedAt = statusResponse.ProcessedAt
        };
    }

    return new KSeFInvoiceStatusResult
    {
        Success = false,
        Status = "Unknown",
        ErrorMessage = statusResponse.ErrorMessage
    };
}
```

### Step 3: Update FA XML Generator

The current FA XML generator in `FAXmlGeneratorService.cs` creates a basic structure. You need to:

1. Download the official FA(3) schema from:
   - https://www.gov.pl/web/kas/schemat-faktury-ustrukturyzowanej

2. Update the XML generation to match FA(3) requirements:
   - Correct namespace URI
   - All required elements per schema
   - Proper data types and formats
   - VAT calculation rules
   - Payment terms
   - Currency handling

**Files to Update:**
- `/PlaySpace.Services/Services/FAXmlGeneratorService.cs`

### Step 4: Test with KSeF Test Environment

1. **Register for Test Account:**
   - Go to: https://ksef-test.mf.gov.pl
   - Create test account with test NIP
   - Generate test token

2. **Configure Test Business:**
   ```http
   POST /api/business-profile/{businessProfileId}/ksef/configure
   {
     "token": "test_token_here",
     "environment": "Test"
   }
   ```

3. **Enable KSeF:**
   ```http
   PUT /api/business-profile/{businessProfileId}/ksef/status
   {
     "enabled": true
   }
   ```

4. **Test Connection:**
   ```http
   POST /api/business-profile/{businessProfileId}/ksef/test-connection
   ```

5. **Create Test Payment:**
   - Complete a test reservation payment
   - Invoice should be auto-generated
   - Check invoice status in database

### Step 5: Validate XML

Use official KSeF validators:
- Test Environment: https://ksef-test.mf.gov.pl/validator
- Validate FA XML against FA(3) schema
- Check for errors and warnings

### Step 6: Production Deployment

1. **Update Configuration:**
   ```json
   "KSeFConfiguration": {
     "TestApiUrl": "https://ksef-test.mf.gov.pl/api",
     "ProductionApiUrl": "https://ksef.mf.gov.pl/api",
     "EnableAutoInvoicing": true,
     "InvoicePrefix": "FA",
     "DefaultVATRate": 23
   }
   ```

2. **Business Onboarding:**
   - Each business must register for KSeF account
   - Obtain production token from KSeF portal
   - Configure via API endpoints

3. **Monitoring:**
   - Monitor invoice submission success rates
   - Set up alerts for failures
   - Log all KSeF API interactions

---

## API Documentation

### For Businesses

#### Configure KSeF Credentials

```http
POST /api/business-profile/{businessProfileId}/ksef/configure
Authorization: Bearer {token}
Content-Type: application/json

{
  "token": "business_ksef_token",
  "environment": "Test"  // or "Production"
}
```

#### Enable KSeF Integration

```http
PUT /api/business-profile/{businessProfileId}/ksef/status
Authorization: Bearer {token}
Content-Type: application/json

{
  "enabled": true
}
```

#### Test Connection

```http
POST /api/business-profile/{businessProfileId}/ksef/test-connection
Authorization: Bearer {token}
```

#### Check Configuration

```http
GET /api/business-profile/{businessProfileId}/ksef/configuration
Authorization: Bearer {token}
```

---

## Important Notes

### Legal Compliance

✅ **Correct Implementation:**
- Each business uses their own NIP and token
- Seller NIP on invoice matches submitter credentials
- Compliant with Polish KSeF regulations

❌ **Wrong Implementation (Avoid):**
- Platform issuing invoices for all businesses
- Centralized credentials
- Mismatched seller/submitter NIPs

### Security

- KSeF tokens stored securely per business
- Never expose tokens in API responses
- Only business owner can access credentials
- Session tokens expire after configured time

### Mandatory Date

E-invoicing via KSeF becomes **mandatory in Poland on February 1, 2026** for all B2B transactions.

---

## Troubleshooting

### Invoice Status "PendingBusinessKSeFSetup"

**Cause:** Business has not configured KSeF credentials

**Solution:**
1. Business must register for KSeF account
2. Obtain KSeF token
3. Configure via API endpoint

### Invoice Status "Error"

**Cause:** Various - check `KSeFErrorMessage` field

**Common Issues:**
- Invalid FA XML format
- Authentication failure
- Network/API timeout
- Invalid business credentials

### Invoice Status "PendingKSeFImplementation"

**Cause:** KSeF API integration not yet complete

**Solution:** Complete Step 2 above (Implement KSeF API Methods)

---

## Support

**Official KSeF Support:**
- Email: kontakt@ksef.mf.gov.pl
- Phone: +48 22 330 00 00 (business days 7:00-19:00)
- Portal: https://ksef.podatki.gov.pl
- Form: https://ksef.podatki.gov.pl/formularz

**Technical Documentation:**
- API v2: https://ksef-test.mf.gov.pl/docs/v2/index.html
- FA(3) Schema: https://www.gov.pl/web/kas/schemat-faktury-ustrukturyzowanej
- Integration Guide: Available from Ministry of Finance

---

## Related Files

### Core Implementation
- `/PlaySpace.Services/Services/KSeFApiService.cs` - KSeF API wrapper (NEEDS IMPLEMENTATION)
- `/PlaySpace.Services/Services/FAXmlGeneratorService.cs` - FA XML generator (NEEDS UPDATE)
- `/PlaySpace.Services/Services/KSeFInvoiceService.cs` - Invoice management (COMPLETE)
- `/PlaySpace.Api/Controllers/BusinessProfileController.cs` - API endpoints (COMPLETE)

### Database
- `/PlaySpace.Domain/Models/BusinessProfile.cs` - Business credentials
- `/PlaySpace.Domain/Models/KSeFInvoice.cs` - Invoice entity
- `/PlaySpace.Repositories/Migrations/20260109120000_AddKSeFFieldsToBusinessProfile.cs` - Migration

### Configuration
- `/PlaySpace.Api/appsettings.json` - KSeF configuration
- `/PlaySpace.Domain/Configuration/KSeFOptions.cs` - Options class

---

## Next Steps

1. ✅ Architecture complete
2. ✅ Database schema ready
3. ✅ API endpoints implemented
4. ⚠️ **TODO:** Implement KSEFAPI.KSEFAPIClient integration
5. ⚠️ **TODO:** Update FA XML to FA(3) schema
6. ⚠️ **TODO:** Test with KSeF test environment
7. ⚠️ **TODO:** Production deployment

Good luck with the integration! 🚀
