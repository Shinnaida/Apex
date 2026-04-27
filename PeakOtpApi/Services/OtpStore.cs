using System.Collections.Concurrent;

namespace PeakOtpApi.Services;

public class OtpStore
{
    private readonly ConcurrentDictionary<string, (string Code, DateTime ExpiresUtc)> _store = new();

    public string Create(string email, int expiryMinutes)
    {
        var key = Normalize(email);
        var code = Random.Shared.Next(1000, 9999).ToString();
        var expiresUtc = DateTime.UtcNow.AddMinutes(expiryMinutes);

        _store[key] = (code, expiresUtc);
        return code;
    }

    public (bool ok, string message) Verify(string email, string code)
    {
        var key = Normalize(email);

        if (!_store.TryGetValue(key, out var entry))
            return (false, "No OTP requested for this email.");

        if (DateTime.UtcNow > entry.ExpiresUtc)
            return (false, "OTP expired. Please resend.");

        if (!string.Equals(entry.Code, (code ?? "").Trim(), StringComparison.Ordinal))
            return (false, "Incorrect code.");

        // One-time use
        _store.TryRemove(key, out _);
        return (true, "Verified.");
    }

    private static string Normalize(string email) =>
        (email ?? "").Trim().ToLowerInvariant();
}