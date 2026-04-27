using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Peak.Shared.Services;

public static class OtpApiService
{
    // Android Emulator: http://10.0.2.2:5000/
    // Physical phone:  http://<YOUR_PC_IP>:5000/
    public static string BaseUrl { get; set; } = "http://192.168.100.11:5000/";

    static readonly HttpClient _http = new HttpClient();

    static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static void EnsureBaseAddress()
    {
        // Only set once, and only if it differs
        var desired = new Uri(BaseUrl);
        if (_http.BaseAddress == null || _http.BaseAddress != desired)
            _http.BaseAddress = desired;
    }

    public static async Task<(bool ok, string message)> SendAsync(string email)
    {
        EnsureBaseAddress();

        var payload = JsonSerializer.Serialize(new { email = email }, _jsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsync("otp/send", content);
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }

        if (res.IsSuccessStatusCode)
            return (true, "OTP sent.");

        var body = await res.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? "Failed to send OTP." : body);
    }

    public static async Task<(bool ok, string message)> VerifyAsync(string email, string code)
    {
        EnsureBaseAddress();

        var payload = JsonSerializer.Serialize(new { email = email, code = code }, _jsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsync("otp/verify", content);
        }
        catch (Exception ex)
        {
            return (false, $"Network error: {ex.Message}");
        }

        if (res.IsSuccessStatusCode)
            return (true, "Verified.");

        var body = await res.Content.ReadAsStringAsync();
        return (false, string.IsNullOrWhiteSpace(body) ? "Verification failed." : body);
    }
}