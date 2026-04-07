# Licensing App

A .NET 8 Web API application that generates cryptographically signed license keys using RSA signatures.

## Features

- Generate signed license keys with custom fields (customerId, deviceId, issuedAt, expiresAt, licenseId)
- API Key authentication for security
- RSA signature-based license key generation
- RESTful API with Swagger documentation

## Prerequisites

- .NET 8 SDK
- OpenSSL (for generating RSA key pairs)

## Getting Started

### 1. Generate RSA Key Pair

Before running the application, you need to generate an RSA private/public key pair:

```bash
# Generate private key (2048-bit)
openssl genpkey -algorithm RSA -out private_key.pem -pkeyopt rsa_keygen_bits:2048

# Generate public key from private key
openssl rsa -pubout -in private_key.pem -out public_key.pem
```

### 2. Configure the Application

Update `appsettings.Development.json` (or `appsettings.json` for production):

```json
{
  "ApiKeys": [
    "your-secure-api-key-here"
  ],
  "Licensing": {
    "PrivateKey": "-----BEGIN PRIVATE KEY-----\nYOUR_PRIVATE_KEY_CONTENT_HERE\n-----END PRIVATE KEY-----"
  }
}
```

**Note:** Copy the entire content from `private_key.pem` and replace `\n` with actual newlines in the JSON, or use `\n` as escape sequences.

### 3. Run the Application

```bash
dotnet run
```

The application will start on `https://localhost:5001` (or the configured port).

## API Usage

### Generate License Key

**Endpoint:** `POST /api/License/generate`

**Headers:**
```
X-API-Key: your-api-key-here
Content-Type: application/json
```

**Request Body:**
```json
{
  "customerId": "CUST-12345",
  "deviceId": "DEV-67890",
  "issuedAt": "2024-01-15T10:00:00Z",
  "expiresAt": "2025-01-15T10:00:00Z",
  "licenseId": "LIC-ABC-001"
}
```

**Response:**
```json
{
  "licenseKey": "eyJDdXN0b21lcklkIjoiQ1VTVC0xMjM0NSIsIkRldmljZUlkIjoiREVWLTY3ODkwIiwiSXNzdWVkQXQiOiIyMDI0LTAxLTE1VDEwOjAwOjAwWiIsIkV4cGlyZXNBdCI6IjIwMjUtMDEtMTVUMTA6MDA6MDBaIiwiTGljZW5zZUlkIjoiTElDLUFCQy0wMDEifQ==.cGFkZGluZz0=",
  "licenseId": "LIC-ABC-001",
  "customerId": "CUST-12345",
  "deviceId": "DEV-67890",
  "issuedAt": "2024-01-15T10:00:00Z",
  "expiresAt": "2025-01-15T10:00:00Z"
}
```

### Health Check

**Endpoint:** `GET /api/License/health`

No authentication required for health check.

## License Key Format

The license key consists of two Base64-encoded parts separated by a dot (`.`):

```
[Base64(JSON_DATA)].[Base64(RSA_SIGNATURE)]
```

- **First part:** Base64-encoded JSON containing license fields
- **Second part:** Base64-encoded RSA-SHA256 signature

## Verifying License Keys

To verify a license key, you need the public key. The verification process:

1. Split the license key by the dot separator
2. Base64-decode both parts
3. Verify the RSA signature using the public key
4. Parse the JSON data

Example C# verification code:

```csharp
using System.Security.Cryptography;
using System.Text;

public bool VerifyLicense(string licenseKey, string publicKeyPem)
{
    var parts = licenseKey.Split('.');
    if (parts.Length != 2) return false;

    var dataBytes = Convert.FromBase64String(parts[0]);
    var signatureBytes = Convert.FromBase64String(parts[1]);

    using var rsa = RSA.Create();
    rsa.ImportFromPem(publicKeyPem.ToCharArray());

    return rsa.VerifyData(dataBytes, signatureBytes, 
        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}
```

## Security Considerations

- **API Keys:** Store API keys securely and use strong, randomly generated values
- **Private Key:** Never expose the private key. Keep it secure and use proper key management
- **HTTPS:** Always use HTTPS in production
- **Key Rotation:** Implement a strategy for rotating keys periodically
- **Environment Variables:** Consider using environment variables or Azure Key Vault for secrets

## Swagger Documentation

Access the Swagger UI at: `https://localhost:5001/swagger`

You can test the API directly from the Swagger interface by:
1. Clicking the "Authorize" button
2. Entering your API key
3. Testing the endpoints

## Project Structure

```
LicensingApp/
??? Controllers/
?   ??? LicenseController.cs       # API endpoints
??? Middleware/
?   ??? ApiKeyAuthenticationMiddleware.cs  # API key authentication
??? Models/
?   ??? LicenseRequest.cs          # Request model
?   ??? LicenseResponse.cs         # Response model
??? Services/
?   ??? ILicenseService.cs         # Service interface
?   ??? LicenseService.cs          # License generation logic
??? appsettings.json               # Production configuration
??? appsettings.Development.json   # Development configuration
??? Program.cs                     # Application entry point
```

## Testing with cURL

```bash
curl -X POST "https://localhost:5001/api/License/generate" \
  -H "X-API-Key: dev-api-key-12345" \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "CUST-12345",
    "deviceId": "DEV-67890",
    "issuedAt": "2024-01-15T10:00:00Z",
    "expiresAt": "2025-01-15T10:00:00Z",
    "licenseId": "LIC-ABC-001"
  }'
```

## License

This project is provided as-is for demonstration purposes.
