using System.Text;
using System.Text.Json;
using LicenseValidator;

Console.WriteLine("===========================================");
Console.WriteLine("    Activation Key Validator v3.0");
Console.WriteLine("===========================================");
Console.WriteLine();

// Get server URL (from environment variable or argument)
string serverUrl = Environment.GetEnvironmentVariable("LICENSE_SERVER_URL") 
    ?? "https://localhost:7042";

string apiKey = Environment.GetEnvironmentVariable("LICENSE_API_KEY")
    ?? "dev-api-key-12345";

if (args.Length > 0)
{
    serverUrl = args[0];
}

if (args.Length > 1)
{
    apiKey = args[1];
}

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"Server URL: {serverUrl}");
Console.WriteLine($"Using API for validation");
Console.ResetColor();
Console.WriteLine();

// Get activation key
Console.Write("Enter the activation key to validate (XXXX-XXXX-XXXX-XXXX): ");
var activationKey = Console.ReadLine();

if (string.IsNullOrWhiteSpace(activationKey))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("ERROR: Activation key cannot be empty");
    Console.ResetColor();
    return 1;
}

Console.WriteLine();
Console.WriteLine("Validating activation key with server...");
Console.WriteLine();

// Call the API
try
{
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    
    // Disable SSL validation for development (localhost with self-signed cert)
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    };
    
    using var client = new HttpClient(handler);
    client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    client.BaseAddress = new Uri(serverUrl);
    client.Timeout = TimeSpan.FromSeconds(30);

    var request = new { activationKey = activationKey };
    var json = JsonSerializer.Serialize(request);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await client.PostAsync("/api/License/validate-activation-key", content);
    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ API ERROR: {response.StatusCode}");
        Console.ResetColor();
        Console.WriteLine($"Response: {responseBody}");
        return 1;
    }

    var validationResponse = JsonSerializer.Deserialize<ActivationKeyValidationResponse>(
        responseBody, 
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
    );

    if (validationResponse == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ INVALID RESPONSE FROM SERVER");
        Console.ResetColor();
        return 1;
    }

    if (!validationResponse.IsValid)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ INVALID ACTIVATION KEY");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"Message: {validationResponse.Message}");
        return 1;
    }

    // Display validation results
    Console.WriteLine("===========================================");
    Console.WriteLine("    ACTIVATION KEY INFORMATION");
    Console.WriteLine("===========================================");
    Console.WriteLine();
    Console.WriteLine($"Activation Key:   {activationKey}");
    Console.WriteLine($"License Type:     {validationResponse.LicenseType}");
    Console.WriteLine($"Customer Hash:    {validationResponse.CustomerIdHash}");
    Console.WriteLine($"Issued At:        {validationResponse.IssuedAt:yyyy-MM-dd HH:mm:ss} UTC");

    if (validationResponse.LicenseType?.ToString() == "PERMANENT")
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Expires At:       PERMANENT (Never expires)");
        Console.ResetColor();
    }
    else if (validationResponse.ExpiresAt.HasValue)
    {
        Console.WriteLine($"Expires At:       {validationResponse.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
    }
    else
    {
        Console.WriteLine($"Expires At:       Not set");
    }
    Console.WriteLine();

    // Calculate license duration
    if (validationResponse.LicenseType?.ToString() == "METERED" && validationResponse.ExpiresAt.HasValue && validationResponse.IssuedAt.HasValue)
    {
        TimeSpan licenseDuration = validationResponse.ExpiresAt.Value - validationResponse.IssuedAt.Value;
        Console.WriteLine($"License Duration: {licenseDuration.Days} days");

        // Calculate time remaining
        TimeSpan timeRemaining = validationResponse.ExpiresAt.Value - DateTime.UtcNow;
        if (timeRemaining.TotalDays > 0)
        {
            Console.WriteLine($"Time Remaining:   {timeRemaining.Days} days, {timeRemaining.Hours} hours");
        }
        else if (!validationResponse.IsExpired)
        {
            Console.WriteLine($"Time Remaining:   {timeRemaining.Hours} hours, {timeRemaining.Minutes} minutes");
        }
        else
        {
            TimeSpan timeSinceExpiry = DateTime.UtcNow - validationResponse.ExpiresAt.Value;
            Console.WriteLine($"Expired:          {timeSinceExpiry.Days} days ago");
        }
    }

    Console.WriteLine();
    Console.WriteLine("===========================================");
    Console.WriteLine("    VALIDATION RESULT");
    Console.WriteLine("===========================================");
    Console.WriteLine();

    // Show validation status
    Console.Write("API Validation:   ");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✓ VALID");
    Console.ResetColor();

    Console.Write("Status:           ");
    if (validationResponse.LicenseType?.ToString() == "PERMANENT")
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ ACTIVE (PERMANENT)");
        Console.ResetColor();
    }
    else if (validationResponse.IsExpired)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("❌ EXPIRED");
        Console.ResetColor();
        if (validationResponse.ExpiresAt.HasValue)
        {
            Console.WriteLine($"                  Activation key expired on {validationResponse.ExpiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC");
        }
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ ACTIVE");
        Console.ResetColor();
    }

    Console.WriteLine();
    Console.WriteLine("===========================================");
    Console.WriteLine();

    // Additional info
    Console.ForegroundColor = ConsoleColor.Gray;
    Console.WriteLine($"Message: {validationResponse.Message}");
    Console.WriteLine();
    Console.WriteLine("Note: Validation is performed server-side for security.");
    Console.WriteLine("      The HMAC secret key never leaves the server.");
    Console.WriteLine("      All activation attempts can be logged and monitored.");
    Console.ResetColor();
    Console.WriteLine();

    // Return appropriate exit code
    if (validationResponse.IsExpired)
    {
        return 1;
    }

    return 0;
}
catch (HttpRequestException ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ CONNECTION ERROR: Unable to connect to server");
    Console.ResetColor();
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("Please ensure:");
    Console.WriteLine($"  • Server is running at {serverUrl}");
    Console.WriteLine("  • Network connection is available");
    Console.WriteLine("  • Firewall allows the connection");
    return 1;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ ERROR: {ex.Message}");
    Console.ResetColor();
    return 1;
}

