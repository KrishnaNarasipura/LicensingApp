namespace LicensingApp.Models;

public class LicenseResponse
{
    //public required string LicenseKey { get; set; }
    public required string ActivationKey { get; set; }
    //public required string LicenseId { get; set; }
    public required string CustomerId { get; set; }
    public required LicenseType LicenseType { get; set; }
    public required DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
