namespace LicensingApp.Models;

public class ActivationKeyValidationRequest
{
    public required string ActivationKey { get; set; }
}

public class ActivationKeyValidationResponse
{
    public bool IsValid { get; set; }
    public bool IsExpired { get; set; }
    public string? LicenseType { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public uint? CustomerIdHash { get; set; }
    public string Message { get; set; } = string.Empty;
}
