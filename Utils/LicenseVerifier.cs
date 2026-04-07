using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LicensingApp.Utils;

/// <summary>
/// Utility class for verifying license keys.
/// This can be distributed to clients for license verification.
/// </summary>
public class LicenseVerifier
{
    /// <summary>
    /// Verifies a license key using the public key
    /// </summary>
    /// <param name="licenseKey">The license key to verify (format: base64Data.base64Signature)</param>
    /// <param name="publicKeyPem">The RSA public key in PEM format</param>
    /// <returns>True if the license is valid, false otherwise</returns>
    public static bool VerifyLicense(string licenseKey, string publicKeyPem)
    {
        try
        {
            var parts = licenseKey.Split('.');
            if (parts.Length != 2)
            {
                return false;
            }

            var dataBytes = Convert.FromBase64String(parts[0]);
            var signatureBytes = Convert.FromBase64String(parts[1]);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem.ToCharArray());

            return rsa.VerifyData(dataBytes, signatureBytes, 
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifies and extracts license data from a license key
    /// </summary>
    /// <param name="licenseKey">The license key to verify and parse</param>
    /// <param name="publicKeyPem">The RSA public key in PEM format</param>
    /// <returns>License data if valid, null if invalid</returns>
    public static LicenseData? VerifyAndExtractLicense(string licenseKey, string publicKeyPem)
    {
        try
        {
            if (!VerifyLicense(licenseKey, publicKeyPem))
            {
                return null;
            }

            var parts = licenseKey.Split('.');
            var dataBytes = Convert.FromBase64String(parts[0]);
            var jsonData = Encoding.UTF8.GetString(dataBytes);

            var licenseData = JsonSerializer.Deserialize<LicenseData>(jsonData);
            return licenseData;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a license is currently valid (signature valid and not expired)
    /// </summary>
    public static bool IsLicenseValid(string licenseKey, string publicKeyPem)
    {
        var licenseData = VerifyAndExtractLicense(licenseKey, publicKeyPem);
        if (licenseData == null)
        {
            return false;
        }

        // Check if license is expired
        return DateTime.UtcNow <= licenseData.ExpiresAt;
    }
}

public class LicenseData
{
    public string CustomerId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
