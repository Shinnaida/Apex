using System.Collections.ObjectModel;
using System.Text;
using Microsoft.Maui.ApplicationModel;

namespace Peak;

public partial class MePage : ContentPage
{
    private static readonly string[] DefaultAvatarEmojis =
    {
        "\U0001FAE3", "\U0001F644", "\U0001F62E\u200D\U0001F4A8", "\U0001F9E0",
        "\U0001F680", "\U0001F42F", "\U0001F98A", "\U0001F43C"
    };

    private string _activeUsername = string.Empty;
    private bool _hasLoaded;
    private bool _achievementsLoaded;
    private bool _historyLoaded;
    private bool _isAvatarPickerAnimating;
    private bool _isAvatarActionRunning;
    private bool _isSyncingAccessibilitySwitches;
    private bool _isSettingsOpen;
    private bool _isSettingsAnimating;
    private BiometricAvailability _biometricAvailability = new(false, "Biometric login is unavailable.");

    public ObservableCollection<AchievementItem> AchievementBadges { get; } = new();
    public ObservableCollection<GameHistoryEntryViewModel> GameHistoryEntries { get; } = new();

    public MePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            RefreshProfileMetrics();
            await RefreshBiometricUiAsync();
            return;
        }

        _hasLoaded = true;
        await LoadInitialContentAsync();
    }

    private async Task LoadInitialContentAsync()
    {
        if (LocalAccountStore.TryGetProfile(out var profile))
        {
            _activeUsername = profile.Username.Trim();
            ProfileTitleLabel.Text = _activeUsername;
            TopCardSubtitle.Text = "Brain Explorer";
        }
        else
        {
            _activeUsername = string.Empty;
            ProfileTitleLabel.Text = "Your Profile";
            TopCardSubtitle.Text = "Brain Explorer";
        }

        LoadAccessibilityPreferences();
        ApplyAccessibilityPreview();
        LoadSavedAvatar();
        RefreshProfileMetrics();
        RefreshRankBadge();
        await RefreshBiometricUiAsync();
    }

    private void RefreshProfileMetrics()
    {
        var scores = BrainScoreService.GetCurrentScores();
        var sessions = BrainScoreService.GetRecordedSessionCount();
        var activeDays = BrainScoreService.GetActiveDayCount();
        var streakDays = BrainScoreService.GetCurrentStreakDays();

        if (sessions == 0)
        {
            LevelChipLabel.Text = "NEW";
            StreakChipLabel.Text = "0 DAY STREAK";
            CurrentStreakValueLabel.Text = "No streak yet";
            SessionsValueLabel.Text = "0";
            FocusStatValueLabel.Text = "0";
            ActiveDaysValueLabel.Text = "0";
            AccuracyValueLabel.Text = "0/1000";
            ProgressSectionLabel.Text = "Start your first session";
            ProgressSummaryBar.Progress = 0;
            ProgressValueLabel.Text = "No saved stats yet";
            EnergyStatusLabel.Text = "Ready to begin";
            return;
        }

        var tiers = BrainScoreService.GetPeakRankTiers();
        var currentTier = tiers.Last(t => scores.PeakScore >= t.MinScore);
        var nextTier = tiers.FirstOrDefault(t => t.MinScore > scores.PeakScore);

        LevelChipLabel.Text = currentTier.Name.ToUpperInvariant();
        StreakChipLabel.Text = $"{streakDays} DAY STREAK";
        CurrentStreakValueLabel.Text = $"{streakDays} day{(streakDays == 1 ? string.Empty : "s")}";
        SessionsValueLabel.Text = sessions.ToString();
        FocusStatValueLabel.Text = scores.Focus.ToString();
        ActiveDaysValueLabel.Text = activeDays.ToString();
        AccuracyValueLabel.Text = $"{scores.PeakScore}/1000";

        if (nextTier is null)
        {
            ProgressSectionLabel.Text = "Top Rank";
            ProgressSummaryBar.Progress = 1;
            ProgressValueLabel.Text = "Top rank reached";
        }
        else
        {
            var span = Math.Max(1, nextTier.MinScore - currentTier.MinScore);
            var progress = Math.Clamp((scores.PeakScore - currentTier.MinScore) / (double)span, 0, 1);
            ProgressSectionLabel.Text = $"{currentTier.Name} to {nextTier.Name}";
            ProgressSummaryBar.Progress = progress;
            ProgressValueLabel.Text = $"{scores.PeakScore} / {nextTier.MinScore} pts";
        }

        EnergyStatusLabel.Text = sessions == 0
            ? "Ready to begin"
            : GetEnergySummary(scores);
    }

    private static string GetEnergySummary(BrainSkillScores scores)
    {
        var strongest = new[]
        {
            ("Memory", scores.Memory),
            ("Problem Solving", scores.ProblemSolving),
            ("Language", scores.Language),
            ("Focus", scores.Focus),
            ("Mental Agility", scores.MentalAgility)
        }
        .OrderByDescending(item => item.Item2)
        .First();

        return strongest.Item2 switch
        {
            >= 185 => $"{strongest.Item1} is on fire",
            >= 165 => $"{strongest.Item1} is looking strong",
            _ => $"{strongest.Item1} is warming up"
        };
    }

    private void RefreshRankBadge()
    {
        var scores = BrainScoreService.GetCurrentScores();
        var tier = BrainScoreService.GetPeakRankTiers()
            .Last(t => scores.PeakScore >= t.MinScore);
        var (background, textColor) = GetRankPalette(tier.Name);

        RankBadgeLabel.Text = tier.Name;
        RankBadgeIcon.Source = tier.IconSource;
        RankBadgeBorder.BackgroundColor = background;
        RankBadgeBorder.Stroke = new SolidColorBrush(textColor.WithAlpha(0.45f));
        RankBadgeLabel.TextColor = textColor;
        RankBadgeWordLabel.TextColor = textColor.WithAlpha(0.82f);
    }

    private async Task LoadAchievementsSectionAsync()
    {
        AchievementsSection.IsVisible = true;
        AchievementsBadgesView.IsVisible = true;

        if (_achievementsLoaded)
        {
            return;
        }

        _achievementsLoaded = true;
        AchievementsLoadingCard.IsVisible = true;
        AchievementsLoadingIndicator.IsVisible = true;
        AchievementsLoadingIndicator.IsRunning = true;

        try
        {
            await AchievementsService.RefreshCurrentUserAsync(syncLocalProgress: BrainScoreService.HasResolvedCurrentUserHistory);
            var achievements = AchievementsService.GetAchievements();
            var summary = (Unlocked: achievements.Count(item => item.IsUnlocked), Total: achievements.Count);

            AchievementsStatusLabel.Text = $"{summary.Unlocked}/{summary.Total} unlocked";

            AchievementBadges.Clear();
            foreach (var achievement in achievements.Take(6))
            {
                AchievementBadges.Add(achievement);
            }
        }
        finally
        {
            AchievementsLoadingIndicator.IsRunning = false;
            AchievementsLoadingIndicator.IsVisible = false;
            AchievementsLoadingCard.IsVisible = false;
        }
    }

    private Task LoadHistorySectionAsync()
    {
        GameHistorySection.IsVisible = true;
        GameHistoryView.IsVisible = true;

        if (_historyLoaded)
        {
            return Task.CompletedTask;
        }

        _historyLoaded = true;
        GameHistoryLoadingCard.IsVisible = true;
        GameHistoryLoadingIndicator.IsVisible = true;
        GameHistoryLoadingIndicator.IsRunning = true;

        try
        {
            var sessions = BrainScoreService.GetGameSessions()
                .Take(5)
                .Select(GameHistoryEntryViewModel.FromSession)
                .ToList();

            GameHistoryEntries.Clear();
            foreach (var session in sessions)
            {
                GameHistoryEntries.Add(session);
            }

            GameHistorySummaryLabel.Text = sessions.Count == 0
                ? "No sessions yet"
                : $"{sessions.Count} recent session{(sessions.Count == 1 ? string.Empty : "s")}";
        }
        finally
        {
            GameHistoryLoadingIndicator.IsRunning = false;
            GameHistoryLoadingIndicator.IsVisible = false;
            GameHistoryLoadingCard.IsVisible = false;
        }

        return Task.CompletedTask;
    }

    private static (Color Background, Color Text) GetRankPalette(string rankName) => rankName switch
    {
        "Basecamp" => (Color.FromArgb("#FFF0D8"), Color.FromArgb("#CC8400")),
        "Foothill" => (Color.FromArgb("#E4F8E7"), Color.FromArgb("#1C9F47")),
        "Ridge" => (Color.FromArgb("#E5F9FF"), Color.FromArgb("#0E9FC6")),
        "Ascent" => (Color.FromArgb("#E6F0FF"), Color.FromArgb("#2E77E5")),
        "Summit" => (Color.FromArgb("#EFE8FF"), Color.FromArgb("#6950F4")),
        "Apex" => (Color.FromArgb("#F8E6FF"), Color.FromArgb("#B13CF3")),
        "Peak" => (Color.FromArgb("#FFE3EF"), Color.FromArgb("#E53D84")),
        _ => (Color.FromArgb("#EEF3F8"), Color.FromArgb("#607284"))
    };

    private async Task OpenAchievementAsync(Border chip, AchievementItem achievement)
    {
        await chip.ScaleTo(0.97, 70, Easing.CubicOut);
        await chip.ScaleTo(1.0, 90, Easing.CubicIn);
        await PageTransitionService.PushAsync(Navigation, new AchievementSpotlightPage(achievement, _activeUsername));
    }

    private void LoadSavedAvatar()
    {
        if (!string.IsNullOrWhiteSpace(_activeUsername)
            && LocalAccountStore.TryGetAvatar(_activeUsername, out var avatar))
        {
            if (avatar.Mode == LocalAccountStore.AvatarModePhoto && File.Exists(avatar.Value))
            {
                SetPhotoAvatar(avatar.Value);
                return;
            }

            if (avatar.Mode == LocalAccountStore.AvatarModeEmoji)
            {
                var normalizedEmoji = NormalizeLegacyEmoji(avatar.Value);
                if (!string.IsNullOrWhiteSpace(normalizedEmoji))
                {
                    if (!string.Equals(normalizedEmoji, avatar.Value, StringComparison.Ordinal))
                    {
                        LocalAccountStore.SaveAvatarEmoji(_activeUsername, normalizedEmoji);
                    }

                    SetEmojiAvatar(normalizedEmoji);
                    return;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_activeUsername))
        {
            var seededEmoji = GetSeedAvatarEmoji(_activeUsername);
            LocalAccountStore.SaveAvatarEmoji(_activeUsername, seededEmoji);
            SetEmojiAvatar(seededEmoji);
            return;
        }

        SetEmojiAvatar(LocalAccountStore.DefaultAvatarEmoji);
    }

    private async void OnAvatarTapped(object sender, TappedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeUsername))
        {
            await DisplayAlert("No account found", "Create or sign in to an account first, then pick an avatar.", "OK");
            return;
        }

        await ShowAvatarPickerAsync();
    }

    private async void OnAvatarBackdropTapped(object sender, TappedEventArgs e)
    {
        await HideAvatarPickerAsync();
    }

    private async void OnCloseAvatarPickerTapped(object sender, TappedEventArgs e)
    {
        await HideAvatarPickerAsync();
    }

    private async void OnEmojiAvatarClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeUsername))
        {
            await DisplayAlert("No account found", "Create or sign in to an account first, then pick an avatar.", "OK");
            return;
        }

        if (sender is not Button emojiButton || string.IsNullOrWhiteSpace(emojiButton.Text))
        {
            return;
        }

        var emoji = emojiButton.Text;
        LocalAccountStore.SaveAvatarEmoji(_activeUsername, emoji);
        SetEmojiAvatar(emoji);
        _ = PlayerLeaderboardService.SyncCurrentUserAsync();
        await HideAvatarPickerAsync();
    }

    private async void OnPickPhotoClicked(object sender, EventArgs e)
    {
        if (_isAvatarActionRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_activeUsername))
        {
            await DisplayAlert("No account found", "Create or sign in to an account first, then pick an avatar.", "OK");
            return;
        }

        _isAvatarActionRunning = true;
        try
        {
            await HideAvatarPickerAsync();
            await Task.Yield();
            await PickFromGalleryAsync();
        }
        finally
        {
            _isAvatarActionRunning = false;
        }
    }

    private async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        if (_isAvatarActionRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_activeUsername))
        {
            await DisplayAlert("No account found", "Create or sign in to an account first, then pick an avatar.", "OK");
            return;
        }

        _isAvatarActionRunning = true;
        try
        {
            await HideAvatarPickerAsync();
            await Task.Yield();
            await TakePhotoAsync();
        }
        finally
        {
            _isAvatarActionRunning = false;
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var shouldLogout = await DisplayAlert(
            "Log out",
            "You will be signed out from this device. Continue?",
            "Log out",
            "Cancel");

        if (!shouldLogout)
        {
            return;
        }

        await HideAvatarPickerAsync();
        LocalAccountStore.SignOut();
        _activeUsername = string.Empty;
        App.ShowSignedOutExperience();
    }

    private async void OnAchievementsClicked(object sender, EventArgs e)
    {
        await HideAvatarPickerAsync();
        await LoadAchievementsSectionAsync();
    }

    private async void OnHistoryClicked(object sender, EventArgs e)
    {
        await HideAvatarPickerAsync();
        await LoadHistorySectionAsync();
    }

    private async void OnRankBadgeTapped(object sender, TappedEventArgs e)
    {
        await HideAvatarPickerAsync();

        if (Shell.Current is not null)
        {
            await PageTransitionService.GoToAsync("//stats");
            return;
        }

        await PageTransitionService.PushAsync(Navigation, new StatsPage());
    }

    private async void OnFingerprintActionClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_activeUsername))
        {
            await DisplayAlert("No account found", "Create or sign in to an account first.", "OK");
            return;
        }

        if (!_biometricAvailability.IsAvailable)
        {
            await DisplayAlert("Fingerprint unavailable", _biometricAvailability.Message, "OK");
            return;
        }

        var isEnabled = LocalAccountStore.IsBiometricEnabled(_activeUsername);
        if (isEnabled)
        {
            var disable = await DisplayAlert(
                "Disable fingerprint login",
                $"Turn off fingerprint login for {_activeUsername}?",
                "Disable",
                "Cancel");

            if (!disable)
            {
                return;
            }

            LocalAccountStore.SetBiometricEnabled(_activeUsername, false);
            await RefreshBiometricUiAsync();
            return;
        }

        var authResult = await BiometricAuthService.AuthenticateAsync($"Enable fingerprint login for {_activeUsername}");
        if (!authResult.IsSuccess)
        {
            if (!authResult.WasCancelled)
            {
                await DisplayAlert("Could not enable fingerprint", authResult.Message, "OK");
            }

            return;
        }

        LocalAccountStore.SetBiometricEnabled(_activeUsername, true);
        await RefreshBiometricUiAsync();
        await DisplayAlert("Fingerprint enabled", "You can now sign in with fingerprint from the login panel.", "OK");
    }

    private async Task ShowAvatarPickerAsync()
    {
        if (_isAvatarPickerAnimating || AvatarPickerOverlay.IsVisible)
        {
            return;
        }

        _isAvatarPickerAnimating = true;
        AvatarPickerOverlay.IsVisible = true;
        AvatarPickerOverlay.Opacity = 0;
        AvatarPickerSheet.TranslationY = -80;

        await Task.WhenAll(
            AvatarPickerOverlay.FadeTo(1, 150, Easing.CubicOut),
            AvatarPickerSheet.TranslateTo(0, 0, 230, Easing.CubicOut));

        _isAvatarPickerAnimating = false;
    }

    private async Task HideAvatarPickerAsync()
    {
        if (_isAvatarPickerAnimating || !AvatarPickerOverlay.IsVisible)
        {
            return;
        }

        _isAvatarPickerAnimating = true;

        await Task.WhenAll(
            AvatarPickerOverlay.FadeTo(0, 130, Easing.CubicIn),
            AvatarPickerSheet.TranslateTo(0, -80, 190, Easing.CubicIn));

        AvatarPickerOverlay.IsVisible = false;
        _isAvatarPickerAnimating = false;
    }

    private async Task PickFromGalleryAsync()
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync();
            if (result is null)
            {
                return;
            }

            var localPath = await SaveAvatarFileAsync(result);
            LocalAccountStore.SaveAvatarPhotoPath(_activeUsername, localPath);
            MainThread.BeginInvokeOnMainThread(() => SetPhotoAvatar(localPath));
            var syncResult = await PlayerLeaderboardService.SyncCurrentUserProfileOnlyAsync();
            if (!syncResult.IsSuccess)
            {
                await DisplayAlert("Avatar saved locally", "Your photo was saved on this device, but cloud avatar sync did not finish yet.", "OK");
            }
        }
        catch (PermissionException)
        {
            await DisplayAlert("Permission needed", "Please allow photo access to pick an avatar.", "OK");
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert("Not supported", "Photo picking is not supported on this device.", "OK");
        }
        catch (Exception)
        {
            await DisplayAlert("Could not pick photo", "Something went wrong while picking your avatar photo.", "OK");
        }
    }

    private async Task TakePhotoAsync()
    {
        if (!MediaPicker.Default.IsCaptureSupported)
        {
            await DisplayAlert("Not supported", "Camera capture is not supported on this device.", "OK");
            return;
        }

        try
        {
            var permissionGranted = await EnsureCameraPermissionAsync();
            if (!permissionGranted)
            {
                return;
            }

            var result = await MediaPicker.Default.CapturePhotoAsync();
            if (result is null)
            {
                return;
            }

            var localPath = await SaveAvatarFileAsync(result);
            LocalAccountStore.SaveAvatarPhotoPath(_activeUsername, localPath);
            MainThread.BeginInvokeOnMainThread(() => SetPhotoAvatar(localPath));
            var syncResult = await PlayerLeaderboardService.SyncCurrentUserProfileOnlyAsync();
            if (!syncResult.IsSuccess)
            {
                await DisplayAlert("Avatar saved locally", "Your photo was saved on this device, but cloud avatar sync did not finish yet.", "OK");
            }
        }
        catch (PermissionException)
        {
            await DisplayAlert("Permission needed", "Please allow camera access to take an avatar photo.", "OK");
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert("Not supported", "Camera capture is not supported on this device.", "OK");
        }
        catch (Exception)
        {
            await DisplayAlert("Could not take photo", "Something went wrong while taking your avatar photo.", "OK");
        }
    }

    private async Task<bool> EnsureCameraPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        await DisplayAlert("Camera permission needed", "Please allow camera access so you can capture a profile avatar.", "OK");
        return false;
    }

    private async Task<string> SaveAvatarFileAsync(FileResult pickedFile)
    {
        var avatarDir = GetAvatarDirectory(_activeUsername);
        Directory.CreateDirectory(avatarDir);

        foreach (var existing in Directory.GetFiles(avatarDir))
        {
            File.Delete(existing);
        }

        var extension = Path.GetExtension(pickedFile.FileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        var destinationPath = Path.Combine(avatarDir, $"avatar{extension.ToLowerInvariant()}");
        await using var source = await pickedFile.OpenReadAsync();
        await using var destination = File.Open(destinationPath, FileMode.Create, FileAccess.Write);
        await source.CopyToAsync(destination);

        if (!File.Exists(destinationPath))
        {
            throw new FileNotFoundException("Avatar file was not saved successfully.", destinationPath);
        }

        return destinationPath;
    }

    private static string GetAvatarDirectory(string username)
    {
        return Path.Combine(FileSystem.AppDataDirectory, "avatars", SanitizeFilePart(username));
    }

    private static string SanitizeFilePart(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return "default";
        }

        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return sb.ToString();
    }

    private static string GetSeedAvatarEmoji(string username)
    {
        var normalized = username.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return DefaultAvatarEmojis[0];
        }

        uint hash = 2166136261;
        foreach (var ch in normalized)
        {
            hash ^= ch;
            hash *= 16777619;
        }

        var index = (int)(hash % (uint)DefaultAvatarEmojis.Length);
        return DefaultAvatarEmojis[index];
    }

    private static string NormalizeLegacyEmoji(string value)
    {
        if (string.Equals(value, "\U0001FAE3", StringComparison.Ordinal) || string.Equals(value, "🫣", StringComparison.Ordinal))
        {
            return "\U0001FAE3";
        }

        if (string.Equals(value, "\U0001F644", StringComparison.Ordinal) || string.Equals(value, "🙄", StringComparison.Ordinal))
        {
            return "\U0001F644";
        }

        if (string.Equals(value, "\U0001F62E\u200D\U0001F4A8", StringComparison.Ordinal) || string.Equals(value, "😮‍💨", StringComparison.Ordinal))
        {
            return "\U0001F62E\u200D\U0001F4A8";
        }

        return value switch
        {
            "\U0001F600" => "\U0001F600",
            "\U0001F60E" => "\U0001F60E",
            "\U0001F913" => "\U0001F913",
            "\U0001F9E0" => "\U0001F9E0",
            "\U0001F680" => "\U0001F680",
            "\U0001F42F" => "\U0001F42F",
            "\U0001F98A" => "\U0001F98A",
            "\U0001F43C" => "\U0001F43C",
            "\U0001F419" => "\U0001F419",
            "\U0001F984" => "\U0001F984",
            "\U0001F642" => "\U0001F642",
            "ðŸ˜€" => "\U0001F600",
            "ðŸ˜Ž" => "\U0001F60E",
            "ðŸ¤“" => "\U0001F913",
            "ðŸ§ " => "\U0001F9E0",
            "ðŸš€" => "\U0001F680",
            "ðŸ¯" => "\U0001F42F",
            "ðŸ¦Š" => "\U0001F98A",
            "ðŸ¼" => "\U0001F43C",
            "ðŸ™" => "\U0001F419",
            "ðŸ¦„" => "\U0001F984",
            "ðŸ™‚" => "\U0001F642",
            _ => value
        };
    }

    private async Task RefreshBiometricUiAsync()
    {
        _biometricAvailability = await BiometricAuthService.GetAvailabilityAsync();

        if (string.IsNullOrWhiteSpace(_activeUsername))
        {
            FingerprintStatusLabel.Text = "Sign in to manage fingerprint login.";
            FingerprintStatusLabel.TextColor = Color.FromArgb("#8C8C8C");
            FingerprintActionButton.Text = "Enable";
            FingerprintActionButton.IsEnabled = false;
            return;
        }

        if (!_biometricAvailability.IsAvailable)
        {
            FingerprintStatusLabel.Text = _biometricAvailability.Message;
            FingerprintStatusLabel.TextColor = Color.FromArgb("#8C8C8C");
            FingerprintActionButton.Text = "Unavailable";
            FingerprintActionButton.IsEnabled = false;
            return;
        }

        var enabled = LocalAccountStore.IsBiometricEnabled(_activeUsername);
        FingerprintStatusLabel.Text = enabled
            ? "Enabled for this account."
            : "Not enabled for this account.";
        FingerprintStatusLabel.TextColor = enabled ? Color.FromArgb("#1E8E3E") : Color.FromArgb("#8C8C8C");

        FingerprintActionButton.Text = enabled ? "Disable" : "Enable";
        FingerprintActionButton.IsEnabled = true;
    }

    private void SetEmojiAvatar(string emoji)
    {
        AvatarImage.Source = null;
        AvatarImage.IsVisible = false;
        AvatarEmojiLabel.Text = emoji;
        AvatarEmojiLabel.IsVisible = true;
    }

    private void SetPhotoAvatar(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        AvatarEmojiLabel.IsVisible = false;
        AvatarImage.Source = null;
        AvatarImage.Source = ImageSource.FromStream(() => File.OpenRead(path));
        AvatarImage.IsVisible = true;
    }

    private void LoadAccessibilityPreferences()
    {
        _isSyncingAccessibilitySwitches = true;
        LargeTextSwitch.IsToggled = AccessibilityService.IsLargeTextEnabled;
        ColorBlindSwitch.IsToggled = AccessibilityService.IsColorSafeChartsEnabled;
        HighContrastSwitch.IsToggled = AccessibilityService.IsHighContrastEnabled;
        _isSyncingAccessibilitySwitches = false;
    }

    private void ApplyAccessibilityPreview()
    {
        var options = AccessibilityService.GetOptions();
        BackgroundColor = options.HighContrastEnabled
            ? Color.FromArgb("#FFFFFF")
            : Color.FromArgb("#F8FBFF");
        AccessibilityService.ApplyTextScale(this);
    }

    private void OnLargeTextToggled(object sender, ToggledEventArgs e)
    {
        if (_isSyncingAccessibilitySwitches)
        {
            return;
        }

        AccessibilityService.SetLargeTextEnabled(e.Value);
        ApplyAccessibilityPreview();
    }

    private void OnColorSafeChartsToggled(object sender, ToggledEventArgs e)
    {
        if (_isSyncingAccessibilitySwitches)
        {
            return;
        }

        AccessibilityService.SetColorSafeChartsEnabled(e.Value);
    }

    private void OnHighContrastToggled(object sender, ToggledEventArgs e)
    {
        if (_isSyncingAccessibilitySwitches)
        {
            return;
        }

        AccessibilityService.SetHighContrastEnabled(e.Value);
        ApplyAccessibilityPreview();
    }

    private async void OnSettingsTapped(object sender, TappedEventArgs e)
    {
        if (_isSettingsAnimating)
        {
            return;
        }

        _isSettingsAnimating = true;

        if (_isSettingsOpen)
        {
            await SettingsPanel.FadeTo(0, 140, Easing.CubicIn);
            SettingsPanel.IsVisible = false;
            _isSettingsOpen = false;
        }
        else
        {
            SettingsPanel.IsVisible = true;
            SettingsPanel.Opacity = 0;
            await SettingsPanel.FadeTo(1, 180, Easing.CubicOut);
            _isSettingsOpen = true;
        }

        _isSettingsAnimating = false;
    }
}

public sealed class GameHistoryEntryViewModel
{
    private GameHistoryEntryViewModel(
        string title,
        string subtitle,
        string scoreText,
        string playedText,
        string glyph,
        string accentFill,
        string accentText)
    {
        Title = title;
        Subtitle = subtitle;
        ScoreText = scoreText;
        PlayedText = playedText;
        Glyph = glyph;
        AccentFill = accentFill;
        AccentText = accentText;
    }

    public string Title { get; }
    public string Subtitle { get; }
    public string ScoreText { get; }
    public string PlayedText { get; }
    public string Glyph { get; }
    public string AccentFill { get; }
    public string AccentText { get; }

    public static GameHistoryEntryViewModel FromSession(GameSessionRecord session)
    {
        var title = PlayerLeaderboardService.GetGameDisplayName(session.SourceId);
        var playedAt = session.PlayedUtc.Kind == DateTimeKind.Utc
            ? session.PlayedUtc.ToLocalTime()
            : session.PlayedUtc;
        var palette = ResolvePalette(session.Skill);

        return new GameHistoryEntryViewModel(
            title,
            $"{session.Skill} session",
            session.PeakGameScore.ToString(),
            playedAt.ToString("dd MMM • h:mm tt"),
            palette.Glyph,
            palette.Fill,
            palette.Text);
    }

    private static (string Glyph, string Fill, string Text) ResolvePalette(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Language => ("Aa", "#EEF0FF", "#6B63F5"),
            BrainSkill.Memory => ("M", "#FFF3DE", "#E49B1B"),
            BrainSkill.ProblemSolving => ("+", "#EAF9F0", "#23A65D"),
            BrainSkill.Focus => ("F", "#FFF0F4", "#F0506B"),
            BrainSkill.MentalAgility => ("A", "#EAF5FF", "#318EE8"),
            _ => ("G", "#EEF4FB", "#607284")
        };
    }
}

