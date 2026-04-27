using Microsoft.AspNetCore.Mvc;
using PeakOtpApi.Models;
using PeakOtpApi.Services;

namespace PeakOtpApi.Controllers;

[ApiController]
[Route("otp")]
public class OtpController : ControllerBase
{
    private readonly OtpStore _store;
    private readonly GmailSmtpSender _sender;
    private readonly IConfiguration _config;

    public OtpController(OtpStore store, GmailSmtpSender sender, IConfiguration config)
    {
        _store = store;
        _sender = sender;
        _config = config;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendOtpRequest req)
    {
        var email = (req?.Email ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required." });

        // Gmail-only restriction (remove if you want any email)
        if (!email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Please use a Gmail address." });

        var expiry = int.TryParse(_config["Otp:ExpiryMinutes"], out var mins) ? mins : 5;

        var code = _store.Create(email, expiry);
        await _sender.SendOtpAsync(email, code, expiry);

        return Ok(new { message = "OTP sent." });
    }

    [HttpPost("verify")]
    public IActionResult Verify([FromBody] VerifyOtpRequest req)
    {
        var email = (req?.Email ?? "").Trim();
        var code = (req?.Code ?? "").Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Email and code are required." });

        var (ok, message) = _store.Verify(email, code);

        if (!ok)
            return BadRequest(new { ok = false, message });

        return Ok(new { ok = true });
    }
}