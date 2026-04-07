using LicensingApp.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LicensingApp.Services;

public class LicenseService : ILicenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(IConfiguration configuration, ILogger<LicenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GenerateLicenseKey(LicenseRequest request)
    {
        var licenseData = new
        {
            request.CustomerId,
            request.LicenseType,
            request.IssuedAt,
            request.ExpiresAt
        };

        var jsonData = JsonSerializer.Serialize(licenseData);
        var dataBytes = Encoding.UTF8.GetBytes(jsonData);

        var privateKeyFileName = _configuration["Licensing:PrivateKeyFile"];
        if (string.IsNullOrEmpty(privateKeyFileName))
        {
            throw new InvalidOperationException("Private key file name not configured");
        }

        if (!File.Exists(privateKeyFileName))
        {
            throw new FileNotFoundException($"Private key file not found: {privateKeyFileName}");
        }

        _logger.LogDebug("Loading private key from file: {FileName}", privateKeyFileName);
        var privateKeyPem = File.ReadAllText(privateKeyFileName);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.ToCharArray());

        var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var licenseKey = $"{Convert.ToBase64String(dataBytes)}.{Convert.ToBase64String(signature)}";
        return licenseKey;
    }
}
