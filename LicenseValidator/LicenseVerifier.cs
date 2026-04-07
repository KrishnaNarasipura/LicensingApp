using System.Security.Cryptography;
using System.Text;

namespace LicenseValidator;

/// <summary>
/// License types supported by the activation key system
/// </summary>
public enum LicenseType
{
    METERED,
    PERMANENT
}

/// <summary>
/// Utility class for verifying activation keys.
/// </summary>
public class LicenseVerifier
{
    private readonly string _hmacSecretKey;

    /// <summary>
    /// Creates a new instance of LicenseVerifier
    /// </summary>
    /// <param name="hmacSecretKey">The HMAC secret key for activation key validation</param>
    public LicenseVerifier(string hmacSecretKey)
    {
        _hmacSecretKey = hmacSecretKey ?? throw new ArgumentNullException(nameof(hmacSecretKey));
    }

    /// <summary>
    /// Verifies an activation key (format: XXXX-XXXX-XXXX-XXXX)
    /// </summary>
    /// <param name="activationKey">The activation key to verify</param>
    /// <returns>True if the activation key is valid, false otherwise</returns>
    public bool VerifyLicense(string activationKey)
    {
        var licenseData = VerifyAndExtractLicense(activationKey);
        return licenseData != null;
    }

    /// <summary>
    /// Verifies and extracts license data from an activation key
    /// </summary>
    /// <param name="activationKey">The activation key to verify and parse (format: XXXX-XXXX-XXXX-XXXX)</param>
    /// <returns>License data if valid, null if invalid</returns>
    public LicenseData? VerifyAndExtractLicense(string activationKey)
    {
        try
        {
            // Remove dashes
            string key = activationKey.Replace("-", "").ToUpper();
            
            if (key.Length != 16)
                return null;

            // Decode from Base32
            byte[] combined = FromBase32(key);
            
            if (combined.Length != 10)
                return null;

            // Split data and HMAC
            byte[] data = new byte[5];
            byte[] receivedHmac = new byte[5];
            Array.Copy(combined, 0, data, 0, 5);
            Array.Copy(combined, 5, receivedHmac, 0, 5);

            // Verify HMAC
            byte[] expectedHmac = GenerateHMAC(data);
            byte[] expectedHmacTruncated = expectedHmac.Take(5).ToArray();
            
            // Compare byte by byte to ensure proper validation
            for (int i = 0; i < 5; i++)
            {
                if (receivedHmac[i] != expectedHmacTruncated[i])
                    return null;
            }

            // Unpack data
            return UnpackLicenseData(data);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if an activation key is currently valid (signature valid and not expired)
    /// </summary>
    /// <param name="activationKey">The activation key to validate</param>
    /// <returns>True if valid and not expired, false otherwise</returns>
    public bool IsLicenseValid(string activationKey)
    {
        var licenseData = VerifyAndExtractLicense(activationKey);
        if (licenseData == null)
        {
            return false;
        }

        // PERMANENT licenses never expire
        if (licenseData.LicenseType == LicenseType.PERMANENT)
        {
            return true;
        }

        // Check if METERED license is expired
        return !licenseData.ExpiresAt.HasValue || DateTime.UtcNow <= licenseData.ExpiresAt.Value;
    }

    private LicenseData UnpackLicenseData(byte[] data)
    {
        // Extract customer hash (15 bits)
        uint customerHash = (uint)(data[0] | ((data[1] & 0x7F) << 8));
        
        // Extract license type (1 bit)
        byte licenseTypeBit = (byte)((data[1] >> 7) & 0x01);
        LicenseType licenseType = licenseTypeBit == 1 ? LicenseType.PERMANENT : LicenseType.METERED;
        
        // Extract issued days (12 bits)
        ushort issuedDays = (ushort)(data[2] | ((data[3] & 0x0F) << 8));
        
        // Extract expiry days (12 bits)
        ushort expiryDays = (ushort)(((data[3] >> 4) & 0x0F) | (data[4] << 4));
        
        // Convert back to dates
        DateTime epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime issuedAt = epoch.AddDays(issuedDays);
        
        // For PERMANENT licenses or max value, ExpiresAt is null
        DateTime? expiresAt = null;
        if (licenseType == LicenseType.METERED && expiryDays != 0xFFF)
        {
            expiresAt = epoch.AddDays(expiryDays);
        }
        
        return new LicenseData
        {
            CustomerIdHash = customerHash,
            LicenseType = licenseType,
            CustomerId = $"HASH-{customerHash}",
            DeviceId = string.Empty,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            LicenseId = string.Empty
        };
    }

    private byte[] GenerateHMAC(byte[] data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSecretKey));
        return hmac.ComputeHash(data);
    }

    #region Base32 Encoding (RFC 4648)
    
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private byte[] FromBase32(string base32)
    {
        if (string.IsNullOrEmpty(base32))
            return Array.Empty<byte>();

        base32 = base32.ToUpper().TrimEnd('=');
        var result = new List<byte>();
        int bits = 0;
        int currentByte = 0;

        foreach (char c in base32)
        {
            int value = Base32Alphabet.IndexOf(c);
            if (value < 0)
                throw new ArgumentException("Invalid Base32 character");

            currentByte = (currentByte << 5) | value;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                result.Add((byte)(currentByte >> bits));
                currentByte &= (1 << bits) - 1;
            }
        }

        return result.ToArray();
    }

    #endregion

    /// <summary>
    /// Static helper method to verify activation key without creating an instance
    /// </summary>
    public static bool VerifyActivationKey(string activationKey, string hmacSecretKey)
    {
        var verifier = new LicenseVerifier(hmacSecretKey);
        return verifier.VerifyLicense(activationKey);
    }

    /// <summary>
    /// Static helper method to verify and extract activation key data without creating an instance
    /// </summary>
    public static LicenseData? VerifyAndExtractActivationKey(string activationKey, string hmacSecretKey)
    {
        var verifier = new LicenseVerifier(hmacSecretKey);
        return verifier.VerifyAndExtractLicense(activationKey);
    }
}

public class LicenseData
{
    public uint CustomerIdHash { get; set; }
    public LicenseType LicenseType { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string LicenseId { get; set; } = string.Empty;
}
