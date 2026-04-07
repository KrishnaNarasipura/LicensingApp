namespace LicensingApp.Models;

public class LicenseRequest
{
    public required string CustomerId { get; set; }
    public required LicenseType LicenseType { get; set; }
    public required DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    //public required string LicenseId { get; set; }
}
