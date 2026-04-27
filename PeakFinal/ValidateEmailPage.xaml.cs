using System.Text.RegularExpressions;
using Peak.Shared.Services;

namespace Peak;

public partial class ValidateEmailPage : ContentPage
{
    public ValidateEmailPage()
    {
        InitializeComponent();
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.GoToAsync("..");
    }

    void OnEmailChanged(object sender, TextChangedEventArgs e)
    {
        var email = (e.NewTextValue ?? "").Trim();

        // Basic email format + must be gmail.com
        bool looksLikeEmail = Regex.IsMatch(
            email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase
        );

        bool isGmail = looksLikeEmail && email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase);

        SubmitButton.IsEnabled = isGmail;
        SubmitButton.Opacity = isGmail ? 1 : 0.45;

        ErrorLabel.IsVisible = email.Length > 0 && !isGmail;
        ErrorLabel.Text = "Please enter a valid Gmail address (example@gmail.com).";
    }

    async void OnSubmitClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? "";

        var (ok, msg) = await OtpApiService.SendAsync(email);
        if (!ok)
        {
            await DisplayAlert("Error", msg, "OK");
            return;
        }

        await PageTransitionService.GoToAsync($"{nameof(VerifyCodePage)}?email={Uri.EscapeDataString(email)}");
    }
}
