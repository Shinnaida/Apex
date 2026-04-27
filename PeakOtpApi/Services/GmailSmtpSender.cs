using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace PeakOtpApi.Services;

public class GmailSmtpSender
{
    private readonly IConfiguration _config;

    public GmailSmtpSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendOtpAsync(string toEmail, string code, int expiryMinutes)
    {
        var senderEmail = _config["Otp:SenderEmail"];
        var password = _config["Otp:SenderAppPassword"];

        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            throw new InvalidOperationException("Otp:SenderEmail is not configured.");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Otp:SenderAppPassword is not configured.");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(senderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Your OTP Code";
        message.Body = new TextPart("plain")
        {
            Text = $"Your verification code is: {code}"
        };

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(senderEmail, password);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
    }
}
