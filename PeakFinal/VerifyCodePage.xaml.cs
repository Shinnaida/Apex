using System.Timers;
using Peak.Shared.Services;

namespace Peak;

[QueryProperty(nameof(Email), "email")]
public partial class VerifyCodePage : ContentPage
{
    public string Email { get; set; } = "";

    int _secondsLeft = 30;
    System.Timers.Timer? _timer;

    public VerifyCodePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        EmailLabel.Text = Email;
        StartResendCountdown();

        // Focus first digit
        MainThread.BeginInvokeOnMainThread(() => D1.Focus());
    }

    void StartResendCountdown()
    {
        _timer?.Stop();
        _timer?.Dispose();

        _secondsLeft = 30;
        ResendLabel.Text = $"Resend code {_secondsLeft} secs";
        ResendLabel.TextColor = Color.FromArgb("#666");
        ResendLabel.GestureRecognizers.Clear();

        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (_, __) =>
        {
            _secondsLeft--;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_secondsLeft <= 0)
                {
                    _timer?.Stop();
                    ResendLabel.Text = "Resend code";
                    ResendLabel.TextColor = Color.FromArgb("#1B73E8");

                    ResendLabel.GestureRecognizers.Clear();
                    ResendLabel.GestureRecognizers.Add(new TapGestureRecognizer
                    {
                        Command = new Command(async () => await ResendAsync())
                    });
                }
                else
                {
                    ResendLabel.Text = $"Resend code {_secondsLeft} secs";
                }
            });
        };
        _timer.Start();
    }

    async Task ResendAsync()
    {
        ErrorLabel.IsVisible = false;

        var (ok, msg) = await OtpApiService.SendAsync(Email);
        if (!ok)
        {
            ErrorLabel.Text = msg;
            ErrorLabel.IsVisible = true;
            return;
        }

        StartResendCountdown();
    }

    async void OnDigitChanged(object sender, TextChangedEventArgs e)
    {
        // Allow only 0-9
        var entry = (Entry)sender;
        if (!string.IsNullOrEmpty(entry.Text) && !char.IsDigit(entry.Text[0]))
        {
            entry.Text = "";
            return;
        }

        // Auto-advance
        if (entry.Text?.Length == 1)
        {
            if (entry == D1) D2.Focus();
            else if (entry == D2) D3.Focus();
            else if (entry == D3) D4.Focus();
            else if (entry == D4)
            {
                // Try verify when all 4 digits are filled
                var code = $"{D1.Text}{D2.Text}{D3.Text}{D4.Text}";
                if (code.Length == 4)
                    await VerifyAsync();
            }
        }
    }

    async Task VerifyAsync()
    {
        var code = $"{D1.Text}{D2.Text}{D3.Text}{D4.Text}";

        var (ok, msg) = await OtpApiService.VerifyAsync(Email, code);
        if (!ok)
        {
            ErrorLabel.Text = msg;
            ErrorLabel.IsVisible = true;
            return;
        }

        await DisplayAlert("Success", "Email verified!", "OK");
        await PageTransitionService.GoToAsync("..");
    }

    async void OnChangeEmailTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.GoToAsync("..");
    }
}
