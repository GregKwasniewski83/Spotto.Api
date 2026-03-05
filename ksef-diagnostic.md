# KSeF Connection Diagnostic Steps

## Quick Diagnostic Checklist

### 1. Check Your API Logs

Look for these specific log entries when you call test-connection:

```
✅ Good log entry (connection working):
Initializing KSeF session for NIP: 1234567890
KSeF session initialized successfully for NIP: 1234567890
KSeF session closed successfully

❌ Bad log entry (connection failed):
Initializing KSeF session for NIP: 1234567890
Failed to generate authorization challenge. Status: XXX, Error: ...
HTTP error initializing KSeF session for NIP: ...
```

### 2. Verify Database Configuration

Run this SQL query:

```sql
SELECT
  "Id",
  "Nip",
  "KSeFToken" IS NOT NULL as "TokenSet",
  "KSeFEnvironment",
  "KSeFEnabled"
FROM "BusinessProfiles"
WHERE "KSeFToken" IS NOT NULL;
```

**Expected:**
- Nip: 10 digits (matching your KSeF portal login)
- TokenSet: true
- KSeFEnvironment: "Test"
- KSeFEnabled: Can be true or false (doesn't matter for connection test)

### 3. Common Error Messages and Solutions

#### Error: "503 Service Unavailable"
**Meaning:** KSeF API is temporarily down or under maintenance
**Solution:**
- Wait 10-15 minutes and try again
- Check KSeF status at https://ksef-test.mf.gov.pl
- Try during business hours (7 AM - 7 PM Warsaw time)

#### Error: "401 Unauthorized" or "Invalid token"
**Meaning:** The token is incorrect or expired
**Solution:**
- Log in to https://web2te-ksef.mf.gov.pl/web/login
- Generate a new token
- Update your configuration with the new token

#### Error: "400 Bad Request" with "Invalid NIP"
**Meaning:** The NIP doesn't match the token
**Solution:**
- Verify the NIP in your database matches the NIP you used to generate the token
- NIP should be exactly 10 digits, no spaces, no "PL" prefix

#### Error: "Unable to connect" or "Network error"
**Meaning:** Cannot reach KSeF servers
**Solution:**
- Check your internet connection
- Check if firewall is blocking outbound HTTPS requests
- Try accessing https://ksef-test.mf.gov.pl in a browser from the same server

#### Error: "SSL/TLS error"
**Meaning:** Certificate validation issues
**Solution:**
- Ensure your .NET runtime trusts Polish government certificates
- May need to update certificate store

### 4. Manual HTTP Test

You can test the KSeF API manually using curl or Postman:

#### Step 1: Get Authorization Challenge

```bash
curl -X POST "https://ksef-test.mf.gov.pl/api/online/Session/AuthorisationChallenge" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{
    "contextIdentifier": {
      "type": "onip",
      "identifier": "YOUR_10_DIGIT_NIP"
    }
  }'
```

**Expected Response (Success):**
```json
{
  "timestamp": "2026-01-10T12:30:00Z",
  "challenge": "abc123def456..."
}
```

**If this fails, the problem is with the KSeF API itself, not your code.**

### 5. Test from Different Network

If you're running the API in Docker or on a server:
- Try running the API locally on your development machine
- This will help determine if it's a network/firewall issue

### 6. Check appsettings.json

Verify these exact values:

```json
"KSeFConfiguration": {
  "TestApiUrl": "https://ksef-test.mf.gov.pl/api",
  "ProductionApiUrl": "https://ksef.mf.gov.pl/api"
}
```

**Common mistakes:**
- ❌ "https://ksef-test.mf.gov.pl" (missing /api)
- ❌ "https://ksef-test.mf.gov.pl/api/" (extra slash)
- ❌ "https://web2te-ksef.mf.gov.pl/api" (wrong subdomain)

### 7. Quick Fix: Try Demo Environment

If test environment keeps failing, try the demo environment instead:

Update appsettings.json:
```json
"KSeFConfiguration": {
  "TestApiUrl": "https://ksef-demo.mf.gov.pl/api",
  ...
}
```

Then test again.

---

## What to Share for Help

If still having issues, please share:

1. **Exact error message from logs** (copy/paste the error lines)
2. **HTTP status code** (503, 401, 400, etc.)
3. **Your NIP** (first 4 digits only for privacy: "1234******")
4. **Environment** you're using ("Test" or "Demo")
5. **Time of day** (KSeF might have maintenance windows)

---

## Sources:
- [KSeF API Documentation](https://ksef.dev/api/)
- [Sample Requests on GitHub](https://github.com/ksef4dev/sample-requests/blob/main/session.http)
- [KSeF 2.0 API Documentation](https://www.comarch.com/trade-and-services/data-management/legal-regulation-changes/poland-releases-final-version-of-fa3-schema-and-ksef-20-api-documentation/)
