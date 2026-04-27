namespace Peak.Shared.Services;

public static class LocalOtpService
{
    static string? _email;
    static string? _code;
    static DateTime _expiresAtUtc;

    public static Task RequestCodeAsync(string email, int expireMinutes = 5)
    {
        _email = email.Trim();
        _code = new Random().Next(1000, 9999).ToString();
        _expiresAtUtc = DateTime.UtcNow.AddMinutes(expireMinutes);

        System.Diagnostics.Debug.WriteLine(
            $"[LOCAL OTP] Email={_email} Code={_code}");

        return Task.CompletedTask;
    }

    public static Task<(bool ok, string message)> VerifyCodeAsync(string email, string code)
    {
        if (_email is null || _code is null)
            return Task.FromResult((false, "No code requested."));

        if (!email.Equals(_email, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult((false, "Email mismatch."));

        if (DateTime.UtcNow > _expiresAtUtc)
            return Task.FromResult((false, "Code expired."));

        if (code != _code)
            return Task.FromResult((false, "Incorrect code."));

        return Task.FromResult((true, "Verified."));
    }
}