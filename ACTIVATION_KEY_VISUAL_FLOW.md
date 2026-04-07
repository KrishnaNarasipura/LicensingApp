# 🔐 Activation Key System - Visual Flow Diagrams

## 📊 Complete System Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         ACTIVATION KEY SYSTEM                           │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────────────────┐              ┌──────────────────────┐         │
│  │   KEY GENERATION     │              │   KEY VALIDATION     │         │
│  │    (Server-Side)     │              │  (Server API Call)   │         │
│  └──────────────────────┘              └──────────────────────┘         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🎯 PART 1: ACTIVATION KEY GENERATION

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CLIENT REQUEST                                       │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         │ HTTP POST /api/License/generate
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    LicenseController.cs                                 │
│                    GenerateLicense()                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  INPUT: LicenseRequest                                                 │
│  {                                                                      │
│    "customerId": "CUSTOMER-001",                                       │
│    "licenseType": "METERED",          ← METERED or PERMANENT           │
│    "licenseId": "LIC-12345",                                           │
│    "issuedAt": "2024-01-15T10:00:00Z",                                │
│    "expiresAt": "2025-01-15T10:00:00Z"  ← null for PERMANENT          │
│  }                                                                      │
│                                                                         │
│  VALIDATION:                                                            │
│  ├─ If METERED: ExpiresAt must be provided and > IssuedAt             │
│  └─ If PERMANENT: ExpiresAt should be null (ignored if provided)      │
│                                                                         │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│              ActivationKeyService.GenerateActivationKey()               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  STEP 1: Pack License Data into 5 bytes (40 bits)                     │
│  ┌───────────────────────────────────────────────────────────────┐    │
│  │ PackLicenseData(customerId, licenseType, issuedAt, expiresAt) │    │
│  └───────────────────────────────────────────────────────────────┘    │
│                         │                                              │
│                         ▼                                              │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │  Bit Packing (40 bits total):                                   │  │
│  │                                                                  │  │
│  │  ┌──────────┬───┬────────────┬────────────┐                    │  │
│  │  │ Customer │ T │  Issued    │  Expiry    │                    │  │
│  │  │  Hash    │ y │  Days      │  Days      │                    │  │
│  │  │ (15 bits)│(1)│  (12 bits) │  (12 bits) │                    │  │
│  │  └──────────┴───┴────────────┴────────────┘                    │  │
│  │                                                                  │  │
│  │  Details:                                                        │  │
│  │  • Customer Hash: customerId.GetHashCode() & 0x7FFF             │  │
│  │  • License Type: 0 = METERED, 1 = PERMANENT                     │  │
│  │  • Issued Days: Days since 2024-01-01 (epoch)                   │  │
│  │  • Expiry Days: Days since 2024-01-01 OR 0xFFF for PERMANENT    │  │
│  │                                                                  │  │
│  │  Example for "CUSTOMER-001" issued 2024-01-15, expires 2025-01-15:│
│  │                                                                  │  │
│  │  data[0] = 0x39  ← Customer hash low byte                       │  │
│  │  data[1] = 0x30  ← Customer hash high (15 bits) + Type bit      │  │
│  │  data[2] = 0x0E  ← Issued days low byte (14 days)               │  │
│  │  data[3] = 0x7E  ← Issued high 4 bits + Expiry low 4 bits       │  │
│  │  data[4] = 0x17  ← Expiry days high byte                        │  │
│  │                                                                  │  │
│  │  Result: [39 30 0E 7E 17] (5 bytes)                             │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 2: Generate HMAC Checksum (40 bits)                             │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ GenerateHMAC(data)                                               │  │
│  │                                                                  │  │
│  │ Uses: HMAC-SHA256                                                │  │
│  │ Secret: "L!ve$ys2026" (from appsettings.json)                   │  │
│  │ Input: [39 30 0E 7E 17]                                          │  │
│  │ Output: 32-byte SHA256 hash                                      │  │
│  │ Truncate: Take first 5 bytes                                     │  │
│  │                                                                  │  │
│  │ Example: [A3 B4 C5 D6 E7] (first 5 bytes of HMAC)               │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 3: Combine Data + HMAC (10 bytes total)                         │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ combined = new byte[10]                                          │  │
│  │ Array.Copy(data, 0, combined, 0, 5)    ← Data bytes             │  │
│  │ Array.Copy(hmac, 0, combined, 5, 5)    ← HMAC bytes             │  │
│  │                                                                  │  │
│  │ Result: [39 30 0E 7E 17 A3 B4 C5 D6 E7]                         │  │
│  │         └──── Data ─────┘ └──── HMAC ────┘                      │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 4: Base32 Encode (RFC 4648)                                     │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ ToBase32(combined)                                               │  │
│  │                                                                  │  │
│  │ Alphabet: "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"                    │  │
│  │                                                                  │  │
│  │ Process:                                                         │  │
│  │ • Take 10 bytes (80 bits)                                        │  │
│  │ • Convert to Base32 (5 bits per character)                       │  │
│  │ • Result: 16 characters (80 bits ÷ 5 = 16)                      │  │
│  │                                                                  │  │
│  │ Example: "HGAQ3YL2UPGEUKBH"                                      │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 5: Format with Dashes                                           │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ FormatActivationKey(base32)                                      │  │
│  │                                                                  │  │
│  │ Insert dash every 4 characters:                                  │  │
│  │ HGAQ-3YL2-UPGE-UKBH                                             │  │
│  │                                                                  │  │
│  │ ✅ FINAL ACTIVATION KEY                                          │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    LicenseController.cs                                 │
│                    Return Response                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  OUTPUT: LicenseResponse                                               │
│  {                                                                      │
│    "activationKey": "HGAQ-3YL2-UPGE-UKBH",                            │
│    "customerId": "CUSTOMER-001",                                       │
│    "licenseType": "METERED",                                           │
│    "issuedAt": "2024-01-15T10:00:00Z",                                │
│    "expiresAt": "2025-01-15T10:00:00Z"                                │
│  }                                                                      │
│                                                                         │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    CLIENT RECEIVES KEY                                  │
│                    User can now activate with: HGAQ-3YL2-UPGE-UKBH    │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🔍 PART 2: ACTIVATION KEY VALIDATION

### Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    CLIENT / LicenseValidator                            │
│                    User enters: HGAQ-3YL2-UPGE-UKBH                     │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         │ HTTP POST /api/License/validate-activation-key
                         │ X-API-Key: dev-api-key-12345
                         │
                         │ Body: { "activationKey": "HGAQ-3YL2-UPGE-UKBH" }
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    ApiKeyAuthenticationMiddleware                       │
│                    Validates API Key                                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Checks: X-API-Key header == "dev-api-key-12345"                        │
│  ✅ Valid → Continue                                                    │
│  ❌ Invalid → 401 Unauthorized                                          │
│                                                                         │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    LicenseController.cs                                 │
│                    ValidateActivationKey()                              │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  INPUT: ActivationKeyValidationRequest                                  │
│  {                                                                      │
│    "activationKey": "HGAQ-3YL2-UPGE-UKBH"                               │
│  }                                                                      │
│                                                                         │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│              ActivationKeyService.ValidateActivationKey()               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  STEP 1: Normalize Input                                                │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │ key = activationKey.Replace("-", "").ToUpper()                  │    │
│  │                                                                 │  │
│  │ Input:  "HGAQ-3YL2-UPGE-UKBH"                                   │  │
│  │ Output: "HGAQ3YL2UPGEUKBH"                                      │  │
│  │                                                                  │  │
│  │ Validate length: Must be exactly 16 characters                  │  │
│  │ ❌ Not 16 → return null (invalid)                               │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 2: Base32 Decode                                                 │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ FromBase32(key)                                                  │  │
│  │                                                                  │  │
│  │ Input:  "HGAQ3YL2UPGEUKBH" (16 Base32 chars)                    │  │
│  │ Output: [39 30 0E 7E 17 A3 B4 C5 D6 E7] (10 bytes)             │  │
│  │                                                                  │  │
│  │ Process:                                                         │  │
│  │ • Each Base32 char = 5 bits                                      │  │
│  │ • 16 chars × 5 bits = 80 bits = 10 bytes                        │  │
│  │                                                                  │  │
│  │ Validate length: Must be exactly 10 bytes                       │  │
│  │ ❌ Not 10 → return null (invalid)                               │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 3: Split Data and HMAC                                           │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ data         = bytes[0..4]  = [39 30 0E 7E 17]                  │  │
│  │ receivedHmac = bytes[5..9]  = [A3 B4 C5 D6 E7]                  │  │
│  │                                                                  │  │
│  │ ┌────────────────┬────────────────┐                             │  │
│  │ │  Data (5 bytes)│ HMAC (5 bytes) │                             │  │
│  │ └────────────────┴────────────────┘                             │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 4: Verify HMAC                                                   │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ expectedHmac = GenerateHMAC(data)                                │  │
│  │                                                                  │  │
│  │ Uses SAME secret as generation: "L!ve$ys2026"                   │  │
│  │ Input: [39 30 0E 7E 17]                                          │  │
│  │ Output: 32-byte SHA256, take first 5 bytes                       │  │
│  │                                                                  │  │
│  │ expectedHmac = [A3 B4 C5 D6 E7]                                  │  │
│  │                                                                  │  │
│  │ Compare byte by byte:                                            │  │
│  │ for (int i = 0; i < 5; i++)                                      │  │
│  │     if (receivedHmac[i] != expectedHmac[i])                      │  │
│  │         return null  // ❌ INVALID CHECKSUM                      │  │
│  │                                                                  │  │
│  │ ✅ All 5 bytes match → HMAC VALID                                │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 5: Unpack License Data                                           │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ UnpackLicenseData(data)                                          │  │
│  │                                                                  │  │
│  │ Input: [39 30 0E 7E 17]                                          │  │
│  │                                                                  │  │
│  │ Extract:                                                         │  │
│  │                                                                  │  │
│  │ 1. Customer Hash (15 bits):                                      │  │
│  │    data[0] | ((data[1] & 0x7F) << 8)                            │  │
│  │    0x39 | ((0x30 & 0x7F) << 8) = 0x3039 = 12345 decimal         │  │
│  │                                                                  │  │
│  │ 2. License Type (1 bit):                                         │  │
│  │    (data[1] >> 7) & 0x01                                         │  │
│  │    (0x30 >> 7) & 0x01 = 0                                        │  │
│  │    0 = METERED, 1 = PERMANENT                                    │  │
│  │                                                                  │  │
│  │ 3. Issued Days (12 bits):                                        │  │
│  │    data[2] | ((data[3] & 0x0F) << 8)                            │  │
│  │    0x0E | ((0x7E & 0x0F) << 8) = 0x00E = 14 days                │  │
│  │    2024-01-01 + 14 days = 2024-01-15                            │  │
│  │                                                                  │  │
│  │ 4. Expiry Days (12 bits):                                        │  │
│  │    ((data[3] >> 4) & 0x0F) | (data[4] << 4)                     │  │
│  │    ((0x7E >> 4) & 0x0F) | (0x17 << 4) = 0x17E = 382 days        │  │
│  │    2024-01-01 + 382 days = 2025-01-18                           │  │
│  │    (If 0xFFF → PERMANENT, expiresAt = null)                     │  │
│  │                                                                  │  │
│  │ Result: ActivationKeyData                                        │  │
│  │ {                                                                │  │
│  │   customerIdHash: 12345,                                         │  │
│  │   licenseType: METERED,                                          │  │
│  │   issuedAt: 2024-01-15,                                          │  │
│  │   expiresAt: 2025-01-18                                          │  │
│  │ }                                                                │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                                                                         │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    LicenseController.cs                                 │
│                    Check Expiration & Build Response                    │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  STEP 6: Check if Expired                                              │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ If METERED:                                                      │  │
│  │   isExpired = DateTime.UtcNow > expiresAt                        │  │
│  │                                                                  │  │
│  │ If PERMANENT:                                                    │  │
│  │   isExpired = false (never expires)                              │  │
│  │                                                                  │  │
│  │ Example (current date: 2024-06-15):                              │  │
│  │   expiresAt = 2025-01-18                                         │  │
│  │   2024-06-15 > 2025-01-18 → false (NOT expired)                 │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  STEP 7: Build Response Message                                        │
│  ┌─────────────────────────────────────────────────────────────────┐  │
│  │ If PERMANENT:                                                    │  │
│  │   message = "Activation key is valid (PERMANENT license)"        │  │
│  │                                                                  │  │
│  │ Else if isExpired:                                               │  │
│  │   message = "Activation key has expired"                         │  │
│  │                                                                  │  │
│  │ Else:                                                            │  │
│  │   message = "Activation key is valid"                            │  │
│  └─────────────────────────────────────────────────────────────────┘  │
│                         │                                              │
│                         ▼                                              │
│  OUTPUT: ActivationKeyValidationResponse                               │
│  {                                                                      │
│    "isValid": true,                                                    │
│    "isExpired": false,                                                 │
│    "licenseType": "METERED",                                           │
│    "issuedAt": "2024-01-15T00:00:00Z",                                │
│    "expiresAt": "2025-01-18T00:00:00Z",                               │
│    "customerIdHash": 12345,                                            │
│    "message": "Activation key is valid"                                │
│  }                                                                      │
│                                                                         │
└────────────────────────┬────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                    LicenseValidator (Client)                            │
│                    Display Results                                      │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  Console Output:                                                        │
│  ═══════════════════════════════════════                               │
│       ACTIVATION KEY INFORMATION                                        │
│  ═══════════════════════════════════════                               │
│                                                                         │
│  Activation Key:   HGAQ-3YL2-UPGE-UKBH                                │
│  License Type:     METERED                                             │
│  Customer Hash:    12345                                               │
│  Issued At:        2024-01-15 00:00:00 UTC                             │
│  Expires At:       2025-01-18 00:00:00 UTC                             │
│                                                                         │
│  License Duration: 368 days                                            │
│  Time Remaining:   217 days, 8 hours                                   │
│                                                                         │
│  ═══════════════════════════════════════                               │
│       VALIDATION RESULT                                                 │
│  ═══════════════════════════════════════                               │
│                                                                         │
│  API Validation:   ✓ VALID                                             │
│  Status:           ✓ ACTIVE                                            │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 🔐 SECURITY FLOW

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    SECRET KEY STORAGE & USAGE                           │
└─────────────────────────────────────────────────────────────────────────┘

SERVER SIDE (Secure):
┌──────────────────────────────────┐
│  appsettings.json                │
│  {                               │
│    "Licensing": {                │
│      "ActivationKeySecret":      │
│        "L!ve$ys2026"             │  ← Secret stays here!
│    }                             │
│  }                               │
└────────────┬─────────────────────┘
             │
             ▼
┌──────────────────────────────────┐
│  ActivationKeyService            │
│  _hmacSecretKey = "L!ve$ys2026"  │
│                                  │
│  Used for:                       │
│  • Generating HMAC checksum      │
│  • Validating HMAC checksum      │
└──────────────────────────────────┘

CLIENT SIDE (No Secret):
┌──────────────────────────────────┐
│  LicenseValidator                │
│                                  │
│  ❌ NO secret key stored!        │
│  ✅ Only calls API               │
│                                  │
│  Security Benefits:              │
│  • Secret cannot be extracted    │
│  • Secret cannot be reversed     │
│  • Server controls validation    │
└──────────────────────────────────┘
```

---

## 📊 DATA STRUCTURE BREAKDOWN

### Binary Representation

```
ACTIVATION KEY: HGAQ-3YL2-UPGE-UKBH
                ↓ Remove dashes
                HGAQ3YL2UPGEUKBH
                ↓ Base32 Decode
                [39 30 0E 7E 17 A3 B4 C5 D6 E7]
                ↓ Split
    Data: [39 30 0E 7E 17]    HMAC: [A3 B4 C5 D6 E7]

DATA BYTES BIT LAYOUT:
┌───────────────────────────────────────────────────────────────────┐
│ Byte 0: 0x39 = 0011 1001                                         │
│         └───────────┘                                             │
│           Customer Hash (bits 0-7)                                │
│                                                                   │
│ Byte 1: 0x30 = 0011 0000                                         │
│         └┬┘└───┬───┘                                             │
│          │     └─ Customer Hash (bits 8-14)                       │
│          └─ License Type bit (bit 15): 0 = METERED               │
│                                                                   │
│ Byte 2: 0x0E = 0000 1110                                         │
│         └───────────┘                                             │
│           Issued Days (bits 0-7) = 14 days                        │
│                                                                   │
│ Byte 3: 0x7E = 0111 1110                                         │
│         └─┬─┘└─┬─┘                                               │
│           │    └─ Issued Days (bits 8-11)                         │
│           └─ Expiry Days (bits 0-3)                               │
│                                                                   │
│ Byte 4: 0x17 = 0001 0111                                         │
│         └───────────┘                                             │
│           Expiry Days (bits 4-11)                                 │
└───────────────────────────────────────────────────────────────────┘

EXTRACTED VALUES:
├─ Customer Hash: 0x3039 = 12345 decimal
├─ License Type:  0 = METERED
├─ Issued Days:   14 → 2024-01-01 + 14 = 2024-01-15
└─ Expiry Days:   382 → 2024-01-01 + 382 = 2025-01-18
```

---

## 🎯 KEY VALIDATION STATES

```
┌────────────────────────────────────────────────────────────┐
│           POSSIBLE VALIDATION OUTCOMES                     │
└────────────────────────────────────────────────────────────┘

1. ✅ VALID & ACTIVE (METERED)
   ├─ HMAC checksum matches
   ├─ Current date < Expiry date
   └─ Response: { isValid: true, isExpired: false }

2. ❌ VALID BUT EXPIRED (METERED)
   ├─ HMAC checksum matches
   ├─ Current date > Expiry date
   └─ Response: { isValid: false, isExpired: true }

3. ✅ VALID & ACTIVE (PERMANENT)
   ├─ HMAC checksum matches
   ├─ License type = PERMANENT
   ├─ Never expires (expiresAt = null or 0xFFF)
   └─ Response: { isValid: true, isExpired: false }

4. ❌ INVALID FORMAT
   ├─ Key length != 16 characters
   ├─ Invalid Base32 characters
   └─ Response: null → { isValid: false, message: "Invalid format" }

5. ❌ INVALID CHECKSUM
   ├─ Key format correct
   ├─ HMAC checksum mismatch
   ├─ Possible tampering detected
   └─ Response: null → { isValid: false, message: "Invalid checksum" }

6. ❌ NETWORK ERROR (Client-Side)
   ├─ Cannot connect to server
   ├─ Server not running
   └─ Client displays: "CONNECTION ERROR"

7. ❌ INVALID API KEY
   ├─ X-API-Key header missing or wrong
   └─ Server returns: 401 Unauthorized
```

---

## 🔄 COMPLETE ROUND-TRIP EXAMPLE

```
TIME: 2024-01-15 10:00:00 UTC
ACTION: Generate license for CUSTOMER-001, 1 year METERED

┌─────────────────────────────────────────────────────────────┐
│ GENERATION                                                  │
├─────────────────────────────────────────────────────────────┤
│ Input:                                                      │
│   customerId: "CUSTOMER-001"                                │
│   licenseType: METERED (0)                                  │
│   issuedAt: 2024-01-15 (14 days since epoch)               │
│   expiresAt: 2025-01-15 (379 days since epoch)             │
│                                                             │
│ Process:                                                    │
│   Hash customer → 0x3039 (15 bits)                          │
│   Pack data → [39 30 0E 7E 17]                             │
│   Generate HMAC → [A3 B4 C5 D6 E7]                         │
│   Combine → [39 30 0E 7E 17 A3 B4 C5 D6 E7]               │
│   Base32 encode → "HGAQ3YL2UPGEUKBH"                        │
│   Format → "HGAQ-3YL2-UPGE-UKBH"                           │
│                                                             │
│ Output: HGAQ-3YL2-UPGE-UKBH                                │
└─────────────────────────────────────────────────────────────┘
                         │
                         │ [6 months later]
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ VALIDATION                                                  │
├─────────────────────────────────────────────────────────────┤
│ TIME: 2024-07-15 14:30:00 UTC                              │
│                                                             │
│ Input: "HGAQ-3YL2-UPGE-UKBH"                               │
│                                                             │
│ Process:                                                    │
│   Remove dashes → "HGAQ3YL2UPGEUKBH"                        │
│   Base32 decode → [39 30 0E 7E 17 A3 B4 C5 D6 E7]         │
│   Split → data=[39 30 0E 7E 17], hmac=[A3 B4 C5 D6 E7]    │
│   Verify HMAC → ✅ MATCH!                                   │
│   Unpack data:                                              │
│     customerHash: 12345                                     │
│     licenseType: METERED                                    │
│     issuedAt: 2024-01-15                                    │
│     expiresAt: 2025-01-15                                   │
│   Check expiry:                                             │
│     2024-07-15 > 2025-01-15? → NO                          │
│   Result: VALID & ACTIVE                                    │
│                                                             │
│ Output:                                                     │
│   ✓ VALID                                                   │
│   ✓ ACTIVE                                                  │
│   Time remaining: 184 days                                  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📝 SUMMARY

### Key Generation (5 Steps):
1. **Pack Data** → 5 bytes (customer, type, dates)
2. **Generate HMAC** → 5 bytes (checksum)
3. **Combine** → 10 bytes total
4. **Base32 Encode** → 16 characters
5. **Format** → XXXX-XXXX-XXXX-XXXX

### Key Validation (7 Steps):
1. **Normalize** → Remove dashes, uppercase
2. **Base32 Decode** → 10 bytes
3. **Split** → Data (5) + HMAC (5)
4. **Verify HMAC** → Compare checksums
5. **Unpack Data** → Extract fields
6. **Check Expiry** → Compare dates
7. **Return Result** → Valid/Invalid/Expired

### Security Features:
- ✅ HMAC-SHA256 authentication
- ✅ Secret key never leaves server
- ✅ Tamper-proof (any change invalidates HMAC)
- ✅ Server-side validation via API
- ✅ Audit trail capability
- ✅ Rate limiting support

---

**16 characters protect 80 bits of license data!** 🔐
