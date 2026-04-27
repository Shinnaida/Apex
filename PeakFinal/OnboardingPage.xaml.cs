using System.Collections.ObjectModel;

namespace Peak;

public partial class OnboardingPage : ContentPage
{
    private bool _isOverlayAnimating;

    public ObservableCollection<Slide> Slides { get; } = new()
    {
        new Slide("\U0001F9E0", "Welcome!", "We're about to embark on a journey together to improve your mental skills!"),
        new Slide("\U0001F3AF", "Play & Improve", "Train your memory to help you remember everything you need."),
        new Slide("\U0001F3C6", "Stay Focused", "With Peak you'll learn to improve your focus and avoid distractions."),
        new Slide("\U0001F9E9", "Become a great problem-solver", "Whether you want to split a bill, calculate a tip or work out a discount, we'll help you improve.")
    };

    public OnboardingPage()
    {
        InitializeComponent();
        BindingContext = this;

        if (LocalAccountStore.TryGetProfile(out var profile))
        {
            LoginUsernameEntry.Text = profile.Username;
            SignupUsernameEntry.Text = profile.Username;
            SignupAgeEntry.Text = profile.Age > 0 ? profile.Age.ToString() : string.Empty;
        }
        else
        {
            var lastUsername = LocalAccountStore.GetLastActiveUsername();
            if (!string.IsNullOrWhiteSpace(lastUsername))
            {
                LoginUsernameEntry.Text = lastUsername;
                SignupUsernameEntry.Text = lastUsername;
            }
        }

        UpdateButtonText(0);
        _ = RefreshBiometricLoginButtonAsync();
    }

    void OnPositionChanged(object? sender, PositionChangedEventArgs e)
        => UpdateButtonText(e.CurrentPosition);

    async void OnPrimaryClicked(object sender, EventArgs e)
    {
        int pos = OnboardingCarousel.Position;
        bool isLast = pos >= Slides.Count - 1;

        if (isLast)
        {
            await PageTransitionService.PushAsync(Navigation, new TrainingPickerPage());
            return;
        }

        OnboardingCarousel.ScrollTo(pos + 1, position: ScrollToPosition.Center, animate: true);
    }

    async void OnAccountAccessClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new AccountAccessPage());
    }

    async void OnOverlayBackdropTapped(object sender, TappedEventArgs e)
    {
        await HideAccountOverlayAsync();
    }

    async void OnCloseOverlayTapped(object sender, TappedEventArgs e)
    {
        await HideAccountOverlayAsync();
    }

    async void OnLoginChoiceClicked(object sender, EventArgs e)
    {
        OverlaySubtitle.Text = "Log in with your player account.";
        await SwitchPanelAsync(LoginPanel);
        await RefreshBiometricLoginButtonAsync();

        if (LocalAccountStore.TryGetProfile(out var profile))
        {
            LoginUsernameEntry.Text = profile.Username;
        }

        LoginUsernameEntry.Focus();
    }

    async void OnSignupChoiceClicked(object sender, EventArgs e)
    {
        OverlaySubtitle.Text = "Create your account and continue to Today.";
        await SwitchPanelAsync(SignupPanel);
        SignupUsernameEntry.Focus();
    }

    async void OnBackToChoicesClicked(object sender, EventArgs e)
    {
        OverlaySubtitle.Text = "Choose how you want to continue.";
        await SwitchPanelAsync(ChoicePanel);
        ClearFlowStatus();
    }

    async void OnSignInClicked(object sender, EventArgs e)
    {
        var username = LoginUsernameEntry.Text?.Trim() ?? string.Empty;
        var password = LoginPasswordEntry.Text ?? string.Empty;

        if (!LocalAccountStore.TrySignIn(username, password, out var error))
        {
            ShowFlowStatus(error, isError: true);
            return;
        }

        var syncResult = await App.RefreshSignedInCloudStateAsync();
        await ShowSyncDebugStatusAsync(
            successMessage: $"Welcome back, {username}. Online sync succeeded. Loading Today...",
            failurePrefix: "Signed in locally, but online sync failed.",
            syncResult);
        await Task.Delay(300);
        await ContinueToTodayAsync();
    }

    async void OnBiometricSignInClicked(object sender, EventArgs e)
    {
        var username = await ResolveBiometricUsernameAsync();
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        if (!LocalAccountStore.IsBiometricEnabled(username))
        {
            ShowFlowStatus("Fingerprint login is not enabled for this account yet.", isError: true);
            return;
        }

        var authResult = await BiometricAuthService.AuthenticateAsync($"Sign in as {username}");
        if (!authResult.IsSuccess)
        {
            if (!authResult.WasCancelled)
            {
                ShowFlowStatus(authResult.Message, isError: true);
            }

            return;
        }

        if (!LocalAccountStore.TrySignInWithBiometric(username, out var error))
        {
            ShowFlowStatus(error, isError: true);
            return;
        }

        LoginUsernameEntry.Text = username;
        LoginPasswordEntry.Text = string.Empty;
        var syncResult = await App.RefreshSignedInCloudStateAsync();
        await ShowSyncDebugStatusAsync(
            successMessage: $"Welcome back, {username}. Online sync succeeded. Loading Today...",
            failurePrefix: "Signed in locally, but online sync failed.",
            syncResult);
        await Task.Delay(300);
        await ContinueToTodayAsync();
    }

    async void OnCreateAndContinueClicked(object sender, EventArgs e)
    {
        var username = SignupUsernameEntry.Text?.Trim() ?? string.Empty;
        var ageText = SignupAgeEntry.Text?.Trim() ?? string.Empty;
        var password = SignupPasswordEntry.Text ?? string.Empty;
        var confirmPassword = SignupConfirmPasswordEntry.Text ?? string.Empty;

        if (!LocalAccountStore.TryValidateRegistration(username, ageText, password, confirmPassword, out var age, out var error))
        {
            ShowFlowStatus(error, isError: true);
            return;
        }

        LocalAccountStore.SaveAccount(username, age, password);

        LoginUsernameEntry.Text = username;
        LoginPasswordEntry.Text = string.Empty;
        SignupPasswordEntry.Text = string.Empty;
        SignupConfirmPasswordEntry.Text = string.Empty;

        var syncResult = await App.RefreshSignedInCloudStateAsync(isNewAccount: true);
        await ShowSyncDebugStatusAsync(
            successMessage: $"Account created for {username}. Online sync succeeded. Entering Today...",
            failurePrefix: "Account created locally, but online sync failed.",
            syncResult);
        await Task.Delay(320);
        await ContinueToTodayAsync();
    }

    async Task ShowAccountOverlayAsync()
    {
        if (_isOverlayAnimating || AccountOverlay.IsVisible)
        {
            return;
        }

        _isOverlayAnimating = true;
        ClearFlowStatus();
        OverlaySubtitle.Text = "Choose how you want to continue.";

        ChoicePanel.IsVisible = true;
        ChoicePanel.Opacity = 1;
        LoginPanel.IsVisible = false;
        SignupPanel.IsVisible = false;

        AccountOverlay.IsVisible = true;
        AccountOverlay.Opacity = 0;
        AccountSheet.TranslationY = 420;

        await Task.WhenAll(
            AccountOverlay.FadeTo(1, 170, Easing.CubicOut),
            AccountSheet.TranslateTo(0, 0, 240, Easing.CubicOut));

        _isOverlayAnimating = false;
    }

    async Task HideAccountOverlayAsync()
    {
        if (_isOverlayAnimating || !AccountOverlay.IsVisible)
        {
            return;
        }

        _isOverlayAnimating = true;

        await Task.WhenAll(
            AccountOverlay.FadeTo(0, 140, Easing.CubicIn),
            AccountSheet.TranslateTo(0, 420, 190, Easing.CubicIn));

        AccountOverlay.IsVisible = false;
        _isOverlayAnimating = false;
    }

    async Task SwitchPanelAsync(VisualElement incomingPanel)
    {
        if (_isOverlayAnimating)
        {
            return;
        }

        _isOverlayAnimating = true;

        var panels = new VisualElement[] { ChoicePanel, LoginPanel, SignupPanel };

        foreach (var panel in panels)
        {
            if (ReferenceEquals(panel, incomingPanel))
            {
                continue;
            }

            if (panel.IsVisible)
            {
                await panel.FadeTo(0, 90, Easing.CubicIn);
                panel.IsVisible = false;
                panel.Opacity = 0;
            }
        }

        incomingPanel.IsVisible = true;
        incomingPanel.Opacity = 0;
        await incomingPanel.FadeTo(1, 130, Easing.CubicOut);

        _isOverlayAnimating = false;
    }

    async Task ContinueToTodayAsync()
    {
        App.ShowSignedInExperience();
        await Task.CompletedTask;
    }

    async Task RefreshBiometricLoginButtonAsync()
    {
        if (!LocalAccountStore.HasBiometricEnabledAccounts())
        {
            FingerprintLoginButton.IsVisible = false;
            return;
        }

        var availability = await BiometricAuthService.GetAvailabilityAsync();
        FingerprintLoginButton.IsVisible = availability.IsAvailable;
    }

    async Task<string> ResolveBiometricUsernameAsync()
    {
        var enteredUsername = LoginUsernameEntry.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(enteredUsername))
        {
            return enteredUsername;
        }

        var candidates = LocalAccountStore.GetBiometricEnabledAccounts();
        if (candidates.Count == 0)
        {
            ShowFlowStatus("No fingerprint-enabled account found.", isError: true);
            return string.Empty;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var selected = await DisplayActionSheet("Choose account", "Cancel", null, candidates.ToArray());
        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel")
        {
            return string.Empty;
        }

        return selected;
    }

    void ClearFlowStatus()
    {
        AccountFlowStatusLabel.Text = string.Empty;
        AccountFlowStatusLabel.IsVisible = false;
    }

    void ShowFlowStatus(string message, bool isError)
    {
        AccountFlowStatusLabel.Text = message;
        AccountFlowStatusLabel.TextColor = isError ? Color.FromArgb("#C83A4A") : Color.FromArgb("#1E8E3E");
        AccountFlowStatusLabel.IsVisible = true;
    }

    async Task ShowSyncDebugStatusAsync(string successMessage, string failurePrefix, OnlineSyncResult syncResult)
    {
        if (syncResult.IsSuccess)
        {
            ShowFlowStatus(successMessage, isError: false);
            return;
        }

        ShowFlowStatus($"{failurePrefix} Check the details below.", isError: true);
        await DisplayAlert(
            "Supabase sync failed",
            $"{syncResult.Message}\n\nThe account still exists locally on this device.",
            "OK");
    }

    void UpdateButtonText(int pos)
    {
        PrimaryButton.Text = (pos >= Slides.Count - 1) ? "Get started" : "Next";
    }
}

public record Slide(string Emoji, string Title, string Subtitle);

