using PeakOtpApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<OtpStore>();
builder.Services.AddSingleton<GmailSmtpSender>();

builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin()
         .AllowAnyHeader()
         .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();


// ======================
// SEND OTP
// ======================
app.MapPost("/otp/send", async (
    SendOtpRequest req,
    OtpStore store,
    GmailSmtpSender sender) =>
{
    if (string.IsNullOrWhiteSpace(req.Email))
        return Results.BadRequest("Email required");

    var code = store.Create(req.Email, 5); // 5 minutes expiry

    await sender.SendOtpAsync(req.Email, code, 5);

    return Results.Ok("OTP sent");
});


// ======================
// VERIFY OTP
// ======================
app.MapPost("/otp/verify", (
    VerifyOtpRequest req,
    OtpStore store) =>
{
    var result = store.Verify(req.Email, req.Code);

    if (!result.ok)
        return Results.BadRequest(result.message);

    return Results.Ok("Verified");
});

app.Run();


// ======================
// Simple Request Models
// ======================

public record SendOtpRequest(string Email);
public record VerifyOtpRequest(string Email, string Code);