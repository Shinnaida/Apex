namespace Peak;

public partial class AccountAccessPage : ContentPage
{
    bool _showingLogin = true;

    public AccountAccessPage()
    {
        InitializeComponent();

        var lastUsername = LocalAccountStore.GetLastActiveUsername();
        RecentAccountLabel.Text = string.IsNullOrWhiteSpace(lastUsername) ? "Saved account" : lastUsername;
        ApplyRecentAccountAvatar(lastUsername);

        if (!string.IsNullOrWhiteSpace(lastUsername))
        {
            LoginUsernameEntry.Text = lastUsername;
            SignupUsernameEntry.Text = lastUsername;
        }

        if (LocalAccountStore.TryGetProfile(out var profile))
        {
            LoginUsernameEntry.Text = profile.Username;
            SignupUsernameEntry.Text = profile.Username;
            SignupAgeEntry.Text = profile.Age > 0 ? profile.Age.ToString() : string.Empty;
        }

        _ = RefreshBiometricLoginButtonAsync();
        ApplyTabState();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BackToOnboardingButton.IsVisible = Navigation.NavigationStack.Count > 1;
        ApplyRecentAccountAvatar(RecentAccountLabel.Text);
        _ = RefreshBiometricLoginButtonAsync();
    }

    void OnLoginTabClicked(object sender, EventArgs e)
    {
        _showingLogin = true;
        ClearFlowStatus();
        ApplyTabState();
    }

    void OnSignupTabClicked(object sender, EventArgs e)
    {
        _showingLogin = false;
        ClearFlowStatus();
        ApplyTabState();
    }

    void OnLoginUsernameEntryTextChanged(object sender, TextChangedEventArgs e)
    {
        _ = RefreshBiometricLoginButtonAsync();
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
            successMessage: $"Welcome back, {username}. Online sync succeeded. Loading your account...",
            failurePrefix: "Signed in locally, but online sync failed.",
            syncResult);
        await Task.Delay(240);
        App.ShowSignedInExperience();
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
            successMessage: $"Welcome back, {username}. Online sync succeeded. Loading your account...",
            failurePrefix: "Signed in locally, but online sync failed.",
            syncResult);
        await Task.Delay(220);
        App.ShowSignedInExperience();
    }

    async void OnCreateAccountClicked(object sender, EventArgs e)
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
            successMessage: $"Account created for {username}. Online sync succeeded. Loading your account...",
            failurePrefix: "Account created locally, but online sync failed.",
            syncResult);
        await Task.Delay(240);
        App.ShowSignedInExperience();
    }

    async void OnBackToOnboardingClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    void ApplyTabState()
    {
        LoginPanel.IsVisible = _showingLogin;
        SignupPanel.IsVisible = !_showingLogin;

        LoginTabButton.BackgroundColor = _showingLogin ? Color.FromArgb("#1A6EE8") : Color.FromArgb("#EEF3FB");
        LoginTabButton.TextColor = _showingLogin ? Colors.White : Color.FromArgb("#61748B");

        SignupTabButton.BackgroundColor = !_showingLogin ? Color.FromArgb("#61748B") : Color.FromArgb("#EEF3FB");
        SignupTabButton.TextColor = !_showingLogin ? Colors.White : Color.FromArgb("#61748B");

        if (_showingLogin)
        {
            HeroTitleLabel.Text = "Welcome Back";
            HeroSubtitleLabel.Text = "Your progress is saved on this device. Sign in and jump right back in.";
            RecentAccountCard.IsVisible = true;
            _ = RefreshBiometricLoginButtonAsync();
            return;
        }

        HeroTitleLabel.Text = "Join us";
        HeroSubtitleLabel.Text = "Create a fresh account and start building your brain training routine today.";
        RecentAccountCard.IsVisible = false;
        BiometricPanel.IsVisible = false;
    }

    async Task RefreshBiometricLoginButtonAsync()
    {
        if (!_showingLogin)
        {
            BiometricPanel.IsVisible = false;
            return;
        }

        var availability = await BiometricAuthService.GetAvailabilityAsync();
        var biometricUsername = ResolveBiometricDisplayTarget();
        var canShowBiometric = availability.IsAvailable && !string.IsNullOrWhiteSpace(biometricUsername) &&
                               LocalAccountStore.IsBiometricEnabled(biometricUsername);

        BiometricPanel.IsVisible = canShowBiometric;

        if (!canShowBiometric)
        {
            FingerprintLoginButton.IsEnabled = false;
            FingerprintLoginButton.Opacity = 0.55;
            return;
        }

        FingerprintLoginButton.Opacity = 1;
        FingerprintLoginButton.IsEnabled = true;
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

    string ResolveBiometricDisplayTarget()
    {
        var enteredUsername = LoginUsernameEntry.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(enteredUsername))
        {
            return enteredUsername;
        }

        return RecentAccountLabel.Text?.Trim() ?? string.Empty;
    }

    void ClearFlowStatus()
    {
        FlowStatusLabel.Text = string.Empty;
        FlowStatusLabel.IsVisible = false;
    }

    void ShowFlowStatus(string message, bool isError)
    {
        FlowStatusLabel.Text = message;
        FlowStatusLabel.TextColor = isError ? Color.FromArgb("#C83A4A") : Color.FromArgb("#1E8E3E");
        FlowStatusLabel.IsVisible = true;
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

    void ApplyRecentAccountAvatar(string username)
    {
        RecentAccountAvatarImage.Source = null;

        if (LocalAccountStore.TryGetAvatar(username, out var avatar))
        {
            if (avatar.Mode == LocalAccountStore.AvatarModePhoto && File.Exists(avatar.Value))
            {
                RecentAccountAvatarEmojiLabel.IsVisible = false;
                RecentAccountAvatarImage.Source = ImageSource.FromFile(avatar.Value);
                RecentAccountAvatarImage.IsVisible = true;
                return;
            }

            if (avatar.Mode == LocalAccountStore.AvatarModeEmoji && !string.IsNullOrWhiteSpace(avatar.Value))
            {
                RecentAccountAvatarImage.IsVisible = false;
                RecentAccountAvatarEmojiLabel.Text = avatar.Value;
                RecentAccountAvatarEmojiLabel.IsVisible = true;
                return;
            }
        }

        RecentAccountAvatarImage.IsVisible = false;
        RecentAccountAvatarEmojiLabel.Text = LocalAccountStore.DefaultAvatarEmoji;
        RecentAccountAvatarEmojiLabel.IsVisible = true;
    }
}

