using System.Security.Cryptography;
using System.Text;
using LicensingApp.Models;

namespace LicensingApp.Services;

/// <summary>
/// Service for generating short activation keys (XXXX-XXXX-XXXX-XXXX format)
/// These keys contain minimal data: Customer ID hash + Expiry date + Checksum
/// </summary>
public class ActivationKeyService
{
    private readonly string _hmacSecretKey;

    public ActivationKeyService(IConfiguration configuration)
    {
        _hmacSecretKey = configuration["Licensing:ActivationKeySecret"] 
            ?? throw new InvalidOperationException("ActivationKeySecret not configured");
    }

    /// <summary>
    /// Generates a 16-character activation key in format XXXX-XXXX-XXXX-XXXX
    /// Contains: CustomerID hash (15 bits) + LicenseType (1 bit) + IssuedAt (12 bits) + ExpiresAt (12 bits) + HMAC checksum (40 bits)
    /// For PERMANENT licenses, ExpiresAt is encoded as 0xFFF (max value)
    /// </summary>
    public string GenerateActivationKey(string customerId, LicenseType licenseType, DateTime issuedAt, DateTime? expiresAt)
    {
        // Pack data into bytes
        byte[] data = PackLicenseData(customerId, licenseType, issuedAt, expiresAt);
        
        // Generate HMAC for authentication
        byte[] hmac = GenerateHMAC(data);
        
        // Combine: data (5 bytes) + hmac (5 bytes) = 10 bytes = 80 bits
        byte[] combined = new byte[10];
        Array.Copy(data, 0, combined, 0, 5);
        Array.Copy(hmac, 0, combined, 5, 5);
        
        // Convert to Base32 (more compact and readable than Base64)
        string base32 = ToBase32(combined);
        
        // Format as XXXX-XXXX-XXXX-XXXX
        return FormatActivationKey(base32);
    }

    /// <summary>
    /// Validates an activation key and extracts embedded data
    /// Returns null if invalid
    /// </summary>
    public ActivationKeyData? ValidateActivationKey(string activationKey)
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

    private byte[] PackLicenseData(string customerId, LicenseType licenseType, DateTime issuedAt, DateTime? expiresAt)
    {
        byte[] data = new byte[5];
        
        // Customer ID: Hash to 15 bits
        uint customerHash = (uint)customerId.GetHashCode() & 0x7FFF; // 15 bits
        
        // License Type: 1 bit (0 = METERED, 1 = PERMANENT)
        byte licenseTypeBit = (byte)(licenseType == LicenseType.PERMANENT ? 1 : 0);
        
        // Issued date: Days since 2024-01-01 (12 bits = ~11 years range)
        DateTime epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        int issuedDaysSinceEpoch = (int)(issuedAt.Date - epoch.Date).TotalDays;
        ushort issuedDays = (ushort)Math.Max(0, Math.Min(issuedDaysSinceEpoch, 0xFFF)); // 12 bits
        
        // Expiry date: Days since 2024-01-01 (12 bits = ~11 years range)
        // For PERMANENT licenses, use 0xFFF (max value) to indicate no expiry
        ushort expiryDays;
        if (licenseType == LicenseType.PERMANENT || !expiresAt.HasValue)
        {
            expiryDays = 0xFFF; // Max value indicates permanent/no expiry
        }
        else
        {
            int expiryDaysSinceEpoch = (int)(expiresAt.Value.Date - epoch.Date).TotalDays;
            expiryDays = (ushort)Math.Max(0, Math.Min(expiryDaysSinceEpoch, 0xFFF)); // 12 bits
        }
        
        // Pack into 5 bytes (40 bits):
        // Bits 0-14:  Customer hash (15 bits)
        // Bit  15:    License type (1 bit: 0=METERED, 1=PERMANENT)
        // Bits 16-27: Issued days (12 bits)
        // Bits 28-39: Expiry days (12 bits, 0xFFF=permanent)
        
        data[0] = (byte)(customerHash & 0xFF);
        data[1] = (byte)(((customerHash >> 8) & 0x7F) | (licenseTypeBit << 7));
        data[2] = (byte)(issuedDays & 0xFF);
        data[3] = (byte)(((issuedDays >> 8) & 0x0F) | ((expiryDays & 0x0F) << 4));
        data[4] = (byte)((expiryDays >> 4) & 0xFF);
        
        return data;
    }

    private ActivationKeyData UnpackLicenseData(byte[] data)
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
        
        return new ActivationKeyData
        {
            CustomerIdHash = customerHash,
            LicenseType = licenseType,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt
        };
    }

    private byte[] GenerateHMAC(byte[] data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSecretKey));
        return hmac.ComputeHash(data);
    }

    private string FormatActivationKey(string base32)
    {
        // Take 16 characters and format as XXXX-XXXX-XXXX-XXXX
        if (base32.Length < 16)
            base32 = base32.PadRight(16, '0');
        
        return $"{base32.Substring(0, 4)}-{base32.Substring(4, 4)}-{base32.Substring(8, 4)}-{base32.Substring(12, 4)}";
    }

    #region Base32 Encoding (RFC 4648)
    
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private string ToBase32(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        var result = new StringBuilder();
        int bits = 0;
        int currentByte = 0;

        foreach (byte b in data)
        {
            currentByte = (currentByte << 8) | b;
            bits += 8;

            while (bits >= 5)
            {
                bits -= 5;
                result.Append(Base32Alphabet[(currentByte >> bits) & 0x1F]);
            }
        }

        if (bits > 0)
        {
            result.Append(Base32Alphabet[(currentByte << (5 - bits)) & 0x1F]);
        }

        return result.ToString();
    }

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
}

public class ActivationKeyData
{
    public uint CustomerIdHash { get; set; }
    public LicenseType LicenseType { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
