using LicensingApp.Models;

namespace LicensingApp.Services;

public interface ILicenseService
{
    string GenerateLicenseKey(LicenseRequest request);
}
