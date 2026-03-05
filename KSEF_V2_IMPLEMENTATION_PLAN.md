# KSeF 2.0 Implementation Plan

## Analysis Complete

Based on the official KSeF 2.0 documentation from https://github.com/CIRFMF/ksef-docs, I've identified that our current implementation needs a complete rewrite to support KSeF 2.0.

## Key Differences: KSeF 1.0 vs 2.0

| Feature | KSeF 1.0 | KSeF 2.0 |
|---------|----------|----------|
| Base URL (Test) | `web2te-ksef.mf.gov.pl/api` | `ksef-test.mf.gov.pl/api/v2` |
| Challenge Endpoint | `/online/Session/AuthorisationChallenge` | `/auth/challenge` |
| Auth Method | Simple SHA-256 hash | RSA-OAEP encryption with public key |
| Init Session | `/online/Session/InitToken` | `/auth/ksef-token` |
| Token Format | Direct token | `{token}|{timestampMs}` encrypted |
| Response | SessionToken header | authenticationToken → accessToken |
| Encryption | None | RSA-OAEP with SHA-256 (MGF1) |

## Required Changes

### 1. **Download KSeF Public Key**
- Endpoint: `GET /api/v2/security/public-key-certificates`
- Cache the public key for encryption
- Parse PEM format RSA public key

### 2. **Update Authentication Flow**

**Current (Incorrect):**
```
1. POST /online/Session/AuthorisationChallenge
2. Sign with SHA-256(challenge + token)
3. POST /online/Session/InitToken
4. Get SessionToken from header
```

**Required (KSeF 2.0):**
```
1. POST /auth/challenge
   → Returns: { challenge, timestamp }

2. Download public key (if not cached)
   → GET /security/public-key-certificates

3. Prepare token string:
   → tokenString = "{ksefToken}|{timestampMs}"

4. Encrypt with RSA-OAEP:
   → encryptedToken = RSA-OAEP-SHA256(tokenString, publicKey)
   → encodedToken = Base64Encode(encryptedToken)

5. POST /auth/ksef-token
   Request:
   {
     "challenge": "{challenge}",
     "contextIdentifier": {
       "type": "onip",
       "identifier": "{nip}"
     },
     "encryptedToken": "{encodedToken}"
   }
   → Returns: { authenticationToken, referenceNumber }

6. Poll status (optional)
   → GET /auth/{referenceNumber}

7. Retrieve access token
   → POST /auth/token/redeem
   → Returns: { accessToken, refreshToken }

8. Use accessToken in Authorization header
   → Authorization: Bearer {accessToken}
```

### 3. **Update All Endpoints**

| Operation | Old Endpoint | New Endpoint (v2) |
|-----------|-------------|-------------------|
| Send Invoice | `/online/Invoice/Send` | `/invoices/send` |
| Check Status | `/online/Invoice/Status/{ref}` | `/invoices/{ref}/status` |
| Terminate Session | `/online/Session/Terminate` (GET) | `/auth/sessions/current` (DELETE) |

### 4. **Update Authorization Header**

**Old:**
```
SessionToken: {token}
```

**New:**
```
Authorization: Bearer {accessToken}
```

## Implementation Complexity

This is a **major rewrite** requiring:

✅ Configuration changes (DONE)
❌ RSA-OAEP encryption implementation
❌ Public key download and parsing
❌ Complete authentication flow rewrite
❌ Token management (access + refresh tokens)
❌ All endpoint updates
❌ Authorization header changes

## Estimated Effort

- **Complexity:** High
- **Files to Modify:** 3-4 files
- **New Dependencies:** Possibly System.Security.Cryptography enhancements
- **Testing Required:** Extensive

## Alternative: Use Existing .NET SDK

The Polish Ministry of Finance provides an **official .NET SDK** for KSeF 2.0 that handles all this complexity.

### Option A: Implement from Scratch
- **Pros:** Full control, no dependencies
- **Cons:** Complex, time-consuming, error-prone
- **Time:** 4-6 hours

### Option B: Use Official SDK
- **Pros:** Tested, maintained, handles all complexity
- **Cons:** External dependency
- **Time:** 1-2 hours

## Recommendation

Given the complexity of RSA-OAEP encryption and the multi-step authentication flow, I **strongly recommend using the official .NET SDK** if available, or looking for a community-maintained NuGet package that implements KSeF 2.0.

**Known Packages:**
- `n1ebieski/ksef-php-client` (PHP, but similar exists for .NET)
- Official Ministry of Finance .NET SDK (check ksef.podatki.gov.pl)

## Next Steps

**Decision Point:** How would you like to proceed?

1. **Quick Solution:** Find and integrate an existing .NET KSeF 2.0 client library
2. **Custom Implementation:** Implement the full RSA-OAEP authentication flow from scratch
3. **Hybrid:** Use a library for authentication, keep our invoice generation logic

Please advise which approach you prefer, and I'll proceed accordingly.

---

## Sources:
- [KSeF GitHub Documentation - Authentication](https://github.com/CIRFMF/ksef-docs/blob/main/uwierzytelnianie.md)
- [KSeF 2.0 API Documentation](https://ksef-test.mf.gov.pl/docs/v2/index.html)
- [Poland Releases KSeF 2.0 API](https://www.comarch.com/trade-and-services/data-management/legal-regulation-changes/poland-releases-final-version-of-fa3-schema-and-ksef-20-api-documentation/)
