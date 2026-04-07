using LicensingApp.Models;
using LicensingApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace LicensingApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LicenseController : ControllerBase
{
    private readonly ILicenseService _licenseService;
    private readonly ActivationKeyService _activationKeyService;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(ILicenseService licenseService, ActivationKeyService activationKeyService, ILogger<LicenseController> logger)
    {
        _licenseService = licenseService;
        _activationKeyService = activationKeyService;
        _logger = logger;
    }

    [HttpPost("generate")]
    [ProducesResponseType(typeof(LicenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public IActionResult GenerateLicense([FromBody] LicenseRequest request)
    {
        try
        {
            // Validate ExpiresAt for METERED licenses
            if (request.LicenseType == LicenseType.METERED)
            {
                if (!request.ExpiresAt.HasValue)
                {
                    return BadRequest("ExpiresAt is required for METERED licenses");
                }
                if (request.ExpiresAt.Value <= request.IssuedAt)
                {
                    return BadRequest("ExpiresAt must be after IssuedAt");
                }
            }
            else if (request.LicenseType == LicenseType.PERMANENT)
            {
                // For PERMANENT licenses, ExpiresAt should be null
                if (request.ExpiresAt.HasValue)
                {
                    _logger.LogWarning("ExpiresAt provided for PERMANENT license, will be ignored");
                }
            }

            //var licenseKey = _licenseService.GenerateLicenseKey(request);
            var activationKey = _activationKeyService.GenerateActivationKey(
                request.CustomerId, 
                request.LicenseType,
                request.IssuedAt, 
                request.ExpiresAt
            );

            var response = new LicenseResponse
            {
                ActivationKey = activationKey,
                CustomerId = request.CustomerId,
                LicenseType = request.LicenseType,
                IssuedAt = request.IssuedAt,
                ExpiresAt = request.ExpiresAt
            };

            _logger.LogInformation("License generated successfully for CustomerId: {CustomerId}", 
                request.CustomerId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating license");
            return StatusCode(500, "An error occurred while generating the license");
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpPost("validate-activation-key")]
    [ProducesResponseType(typeof(ActivationKeyValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ValidateActivationKey([FromBody] ActivationKeyValidationRequest request)
    {
        try
        {
            var keyData = _activationKeyService.ValidateActivationKey(request.ActivationKey);

            if (keyData == null)
            {
                return Ok(new ActivationKeyValidationResponse
                {
                    IsValid = false,
                    Message = "Invalid activation key format or checksum"
                });
            }

            bool isExpired = keyData.LicenseType == LicenseType.METERED 
                && keyData.ExpiresAt.HasValue 
                && DateTime.UtcNow > keyData.ExpiresAt.Value;

            string message;
            if (keyData.LicenseType == LicenseType.PERMANENT)
            {
                message = "Activation key is valid (PERMANENT license)";
            }
            else if (isExpired)
            {
                message = "Activation key has expired";
            }
            else
            {
                message = "Activation key is valid";
            }

            return Ok(new ActivationKeyValidationResponse
            {
                IsValid = !isExpired,
                IsExpired = isExpired,
                LicenseType = keyData.LicenseType.ToString(),
                IssuedAt = keyData.IssuedAt,
                ExpiresAt = keyData.ExpiresAt,
                CustomerIdHash = keyData.CustomerIdHash,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating activation key");
            return BadRequest(new { error = "Invalid activation key format" });
        }
    }
}
