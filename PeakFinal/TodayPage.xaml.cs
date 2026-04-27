using System.Globalization;
using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class TodayPage : ContentPage
{
    private readonly Random _random = new();
    private readonly string[] _dailyKnowledgeFacts =
    {
        "Your brain can rewire itself through practice, a process called neuroplasticity.",
        "Short focused sessions with breaks often improve memory better than one long cram session.",
        "Sleep helps move new information from short-term memory into long-term storage.",
        "Pattern recognition is one of the fastest ways the brain solves unfamiliar problems.",
        "Explaining something out loud can strengthen understanding more than silently rereading it.",
        "Mixing different kinds of tasks can improve mental flexibility and attention switching.",
        "Retrieval practice, trying to recall instead of reread, is one of the strongest learning tools."
    };
    private bool _isAchievementToastSequenceRunning;
    private bool _isMoreWorkoutsAnimating;
    private bool _isAssessmentSummaryAnimating;
    private bool _isPrimaryLoadRunning;
    private bool _isDeferredLoadRunning;
    private BrainSkill _selectedDailySkill = BrainSkill.ProblemSolving;
    private IDispatcherTimer? _countdownTimer;
    private DateTime _nextWorkoutAvailableAtLocal;
    private int _activeWorkoutTabIndex;
    private List<MoreWorkoutSheetItem> _advancedWorkoutItems = new();
    private List<MoreWorkoutSheetItem> _skillWorkoutItems = new();
    private CancellationTokenSource? _loadCts;

    private sealed record SkillSnapshot(
        BrainSkill Skill,
        string Name,
        int Score,
        int RecentDelta,
        string AccentHex);

    private sealed record WorkoutRecommendation(
        string Title,
        string Subtitle,
        string AccentHex,
        string IconSource);

    private sealed record TodayRecommendationSet(
        BrainSkill HeroSkill,
        string HeroHeader,
        string HeroTitle,
        string PromoTitle,
        string PromoSubtitle,
        string PromoIconSource,
        WorkoutRecommendation First,
        WorkoutRecommendation Second,
        WorkoutRecommendation Third);

    private sealed record MoreWorkoutSheetItem(
        string Title,
        string Subtitle,
        string AccentHex,
        string IconSource,
        string BadgeTextColorHex,
        Func<Task> OnTap);

    private sealed record TodaySummarySnapshot(
        BrainSkillScores Scores,
        int StreakDays,
        DateTime NextWorkoutAvailableAtLocal);

    private sealed record DeferredTodaySnapshot(
        TaskBoardService.TaskBoardSnapshot TaskBoard,
        int PointsBalance);

    public TodayPage()
    {
        InitializeComponent();
        ApplyAccessibility();
        InitializeSafeRecommendations();
        ShowTaskLoadingState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RestartPageLoad();
        await LoadPageAsync(_loadCts!.Token);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _loadCts?.Cancel();
        StopCountdownTimer();
    }

    void RestartPageLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
    }

    async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        var snapshot = await LoadPrimarySnapshotAsync(cancellationToken);
        if (snapshot is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        _ = LoadDeferredSectionsAsync(snapshot, cancellationToken);
        _ = ShowNewAchievementToastsAsync();
    }

    async Task<TodaySummarySnapshot?> LoadPrimarySnapshotAsync(CancellationToken cancellationToken)
    {
        if (_isPrimaryLoadRunning)
        {
            return null;
        }

        _isPrimaryLoadRunning = true;
        try
        {
            if (LocalAccountStore.IsSignedIn && !BrainScoreService.HasResolvedCurrentUserHistory)
            {
                try
                {
                    await BrainScoreService.RefreshCurrentUserFromDatabaseAsync();
                }
                catch
                {
                    // Keep the page responsive even if cloud history refresh fails.
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
            }

            var snapshot = await Task.Run(() =>
            {
                var scores = BrainScoreService.GetCurrentScores();
                var streakDays = BrainScoreService.GetCurrentStreakDays();
                var nextWorkout = ResolveNextWorkoutAvailableAtLocal();
                return new TodaySummarySnapshot(scores, streakDays, nextWorkout);
            }, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            ApplyAccessibility();
            ApplyProfileAvatar();
            ApplyDailyKnowledge();
            ApplyPrimarySummary(snapshot);
            return snapshot;
        }
        finally
        {
            _isPrimaryLoadRunning = false;
        }
    }

    void ApplyPrimarySummary(TodaySummarySnapshot snapshot)
    {
        BrainScoreLabel.Text = snapshot.Scores.PeakScore.ToString();
        ApplyRankVisual(snapshot.Scores.PeakScore);
        StreakLabel.Text = snapshot.StreakDays.ToString();
        UpdateStreakVisual(snapshot.StreakDays);

        _nextWorkoutAvailableAtLocal = snapshot.NextWorkoutAvailableAtLocal;
        UpdateDailyWorkoutCountdown();
        StartCountdownTimer();
    }

    async Task LoadDeferredSectionsAsync(TodaySummarySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (_isDeferredLoadRunning)
        {
            return;
        }

        _isDeferredLoadRunning = true;
        try
        {
            await Task.Yield();
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var deferred = await Task.Run(() => new DeferredTodaySnapshot(
                TaskBoardService.GetTaskBoardSnapshot(),
                GamePointsService.GetBalance()), cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RefreshPointsAndTasks(deferred);
            ApplyRecommendationsSafely(snapshot.Scores);
            ApplyProgressBubble(snapshot.Scores, snapshot.StreakDays);
        }
        finally
        {
            _isDeferredLoadRunning = false;
        }
    }

    void InitializeSafeRecommendations()
    {
        ApplyRecommendationsSafely(CreateFallbackScores());
    }

    void ApplyRecommendationsSafely(BrainSkillScores scores)
    {
        try
        {
            ApplySmartRecommendations(scores);
            BuildMoreWorkoutSheetItems(scores);
        }
        catch
        {
            var fallback = CreateFallbackScores();
            ApplySmartRecommendations(fallback);
            BuildMoreWorkoutSheetItems(fallback);
        }

        try
        {
            ApplyMoreWorkoutTab();
        }
        catch
        {
            _activeWorkoutTabIndex = 0;
            ApplyMoreWorkoutTab();
        }
    }

    static BrainSkillScores CreateFallbackScores()
        => new(
            PeakScore: 0,
            Memory: 100,
            ProblemSolving: 100,
            Language: 100,
            MentalAgility: 100,
            Focus: 100,
            Emotion: 100);

    void RefreshPointsAndTasks(DeferredTodaySnapshot deferred)
    {
        ApexPointsBalanceLabel.Text = deferred.PointsBalance.ToString("N0");
        RenderTaskRows(DailyTasksHost, deferred.TaskBoard.DailyTasks);
        RenderTaskRows(WeeklyTasksHost, deferred.TaskBoard.WeeklyTasks);
    }

    void ApplyProfileAvatar()
    {
        TodayAvatarImage.Source = null;
        TodayAvatarImage.IsVisible = false;
        TodayAvatarEmojiLabel.IsVisible = true;
        TodayAvatarEmojiLabel.Text = LocalAccountStore.DefaultAvatarEmoji;

        if (!LocalAccountStore.TryGetProfile(out var profile))
        {
            return;
        }

        if (!LocalAccountStore.TryGetAvatar(profile.Username, out var avatar))
        {
            return;
        }

        if (string.Equals(avatar.Mode, LocalAccountStore.AvatarModePhoto, StringComparison.OrdinalIgnoreCase)
            && File.Exists(avatar.Value))
        {
            TodayAvatarEmojiLabel.IsVisible = false;
            TodayAvatarImage.Source = ImageSource.FromFile(avatar.Value);
            TodayAvatarImage.IsVisible = true;
            return;
        }

        if (string.Equals(avatar.Mode, LocalAccountStore.AvatarModeEmoji, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(avatar.Value))
        {
            TodayAvatarEmojiLabel.Text = avatar.Value.Trim();
        }
    }

    void ApplyDailyKnowledge()
    {
        var dayIndex = Math.Abs(DateTime.Now.Date.GetHashCode());
        var fact = _dailyKnowledgeFacts[dayIndex % _dailyKnowledgeFacts.Length];
        DailyKnowledgeTitleLabel.Text = "Daily brain fact";
        DailyKnowledgeBodyLabel.Text = fact;
    }

    void ApplyProgressBubble(BrainSkillScores scores, int streakDays)
    {
        var peakScore = scores.PeakScore;
        var points = GamePointsService.GetBalance();
        var strongest = new[]
        {
            ("Memory", scores.Memory),
            ("Problem Solving", scores.ProblemSolving),
            ("Language", scores.Language),
            ("Mental Agility", scores.MentalAgility),
            ("Focus", scores.Focus)
        }
        .OrderByDescending(item => item.Item2)
        .First().Item1;

        ProgressBubbleLabel.Text = streakDays switch
        {
            0 when peakScore < 140 => $"Your journey is just starting. Try a quick session today and build your first streak while {strongest} warms up.",
            0 => $"You're building momentum already. Your best area is {strongest}, and one more run can start a new streak.",
            < 3 => $"Nice start. You're on a {streakDays}-day streak, your strongest area is {strongest}, and you have {points:N0} Apex Points ready to spend.",
            < 7 => $"You're getting consistent. A {streakDays}-day streak plus {points:N0} Apex Points puts you in a strong spot to unlock another game.",
            _ => $"You're on fire with a {streakDays}-day streak. Keep pushing {strongest} and use your {points:N0} Apex Points to shape your next training path."
        };
    }

    void RenderTaskRows(VerticalStackLayout host, IReadOnlyList<TodayTaskItem> tasks)
    {
        host.Children.Clear();

        foreach (var task in tasks)
        {
            host.Children.Add(BuildTaskRow(task));
        }
    }

    View BuildTaskRow(TodayTaskItem task)
    {
        var progressText = task.Kind switch
        {
            TaskKind.PlaySessions => $"{task.ProgressValue}/{task.TargetValue} runs",
            _ => $"{task.ProgressValue}/{task.TargetValue} peak"
        };

        string actionText = task.IsClaimed
            ? "Claimed"
            : task.IsComplete
                ? "Claim"
                : $"+{task.RewardPoints}";

        string actionBackground = task.IsClaimed
            ? "#EEF2F5"
            : task.IsComplete
                ? "#DFF4E8"
                : "#E8F4FF";

        string actionTextColor = task.IsClaimed
            ? "#7D8792"
            : task.IsComplete
                ? "#1E9F48"
                : "#2E8FEA";

        var iconFrame = new Border
        {
            WidthRequest = 58,
            HeightRequest = 58,
            BackgroundColor = Color.FromArgb(task.AccentHex),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Opacity = 0.12f,
                Offset = new Point(0, 8),
                Radius = 14
            },
            Content = new Image
            {
                Source = task.IconSource,
                WidthRequest = 50,
                HeightRequest = 50,
                Aspect = Aspect.AspectFit
            }
        };

        View actionContent = new Label
        {
            Text = actionText,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(actionTextColor)
        };

        if (!task.IsClaimed && !task.IsComplete)
        {
            actionContent = new HorizontalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Image
                    {
                        Source = "rainbow_puzzle_icon.svg",
                        WidthRequest = 14,
                        HeightRequest = 14,
                        Aspect = Aspect.AspectFit,
                        VerticalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = task.RewardPoints.ToString("N0"),
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb(actionTextColor),
                        VerticalTextAlignment = TextAlignment.Center
                    }
                }
            };
        }

        var actionChip = new Border
        {
            Padding = new Thickness(10, 6),
            BackgroundColor = Color.FromArgb(actionBackground),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            VerticalOptions = LayoutOptions.Center,
            Content = actionContent
        };

        var card = new Border
        {
            Padding = new Thickness(0),
            BackgroundColor = Color.FromArgb("#F9FBFD"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Opacity = 0.05f,
                Offset = new Point(0, 6),
                Radius = 12
            }
        };

        var grid = new Grid
        {
            Padding = new Thickness(12, 12),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        grid.Children.Add(iconFrame);

        var details = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = task.Title,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#44454D")
                },
                new Label
                {
                    Text = task.Subtitle,
                    FontSize = 12,
                    LineBreakMode = LineBreakMode.WordWrap,
                    TextColor = Color.FromArgb("#7B8692")
                },
                new Label
                {
                    Text = progressText,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb(task.AccentHex)
                }
            }
        };

        Grid.SetColumn(details, 1);
        grid.Children.Add(details);

        Grid.SetColumn(actionChip, 2);
        grid.Children.Add(actionChip);

        card.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OnTaskTappedAsync(task);
        card.GestureRecognizers.Add(tap);

        return card;
    }

    async Task OnTaskTappedAsync(TodayTaskItem task)
    {
        if (task.IsComplete && !task.IsClaimed)
        {
            if (TaskBoardService.TryClaimTask(task.Id, out var reward, out var message))
            {
                await DisplayAlert("Task Reward", $"+{reward:N0} points\n\n{message}", "Nice");
                RefreshPointsAndTasks(new DeferredTodaySnapshot(
                    TaskBoardService.GetTaskBoardSnapshot(),
                    GamePointsService.GetBalance()));
            }
            else
            {
                await DisplayAlert("Task", message, "OK");
            }

            return;
        }

        await OpenGameByTitleAsync(task.GameTitle);
    }

    void UpdateStreakVisual(int streakDays)
    {
        if (streakDays > 0)
        {
            StreakBadge.BackgroundColor = Color.FromArgb("#4DB8FF");
            StreakBadgeGlyph.Text = "\u2713";
            StreakBadgeGlyph.FontSize = 15;
            StreakPanel.Opacity = 1;
            return;
        }

        StreakBadge.BackgroundColor = Color.FromArgb("#B9C4D0");
        StreakBadgeGlyph.Text = "!";
        StreakBadgeGlyph.FontSize = 16;
        StreakPanel.Opacity = 0.9;
    }

    void ApplyAccessibility()
    {
        var options = AccessibilityService.GetOptions();
        AccessibilityService.ApplyTextScale(this);

        BackgroundColor = options.HighContrastEnabled
            ? Color.FromArgb("#FFFFFF")
            : Color.FromArgb("#F4F7FB");
    }

    void ApplyRankVisual(int peakScore)
    {
        var tier = BrainScoreService.GetPeakRankTiers()
            .Last(t => peakScore >= t.MinScore);
        var (background, textColor) = GetRankPalette(tier.Name);

        BrainRankLabel.Text = tier.Name;
        BrainRankIcon.Source = tier.IconSource;
        BrainRankChip.BackgroundColor = background;
        BrainRankLabel.TextColor = textColor;
    }

    static (Color Background, Color Text) GetRankPalette(string rankName) => rankName switch
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

    void StartCountdownTimer()
    {
        StopCountdownTimer();

        _countdownTimer = Dispatcher.CreateTimer();
        _countdownTimer.Interval = TimeSpan.FromSeconds(1);
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    void StopCountdownTimer()
    {
        if (_countdownTimer is null)
        {
            return;
        }

        _countdownTimer.Stop();
        _countdownTimer.Tick -= OnCountdownTick;
        _countdownTimer = null;
    }

    void OnCountdownTick(object? sender, EventArgs e)
    {
        UpdateDailyWorkoutCountdown();
    }

    void UpdateDailyWorkoutCountdown()
    {
        var remaining = _nextWorkoutAvailableAtLocal - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            DailyHeaderLabel.Text = "Daily Workout Ready";
            DailyAvailabilityLabel.Text = "Jump back in:";
            UnlockNowLabel.Text = "Your daily workout is ready to be played";
            HoursTimerCard.IsVisible = false;
            MinutesTimerCard.IsVisible = false;
            SecondsTimerCard.IsVisible = false;
            DailyWorkoutPlayButton.IsEnabled = true;
            DailyWorkoutPlayButton.Opacity = 1;
            DailyWorkoutPlayButton.Text = "PLAY";
            DailyWorkoutPlayButton.WidthRequest = 88;
            return;
        }

        DailyHeaderLabel.Text = "Next Daily Workout";
        DailyAvailabilityLabel.Text = "Available in:";
        HoursTimerCard.IsVisible = true;
        MinutesTimerCard.IsVisible = true;
        SecondsTimerCard.IsVisible = true;
        HoursValueLabel.Text = Math.Max(0, (int)remaining.TotalHours).ToString("00");
        MinutesValueLabel.Text = Math.Max(0, remaining.Minutes).ToString("00");
        SecondsValueLabel.Text = Math.Max(0, remaining.Seconds).ToString("00");
        HoursCaptionLabel.Text = "hours";
        MinutesCaptionLabel.Text = "mins";
        SecondsCaptionLabel.Text = "secs";
        UnlockNowLabel.Text = "Daily mix refreshes tomorrow";
        DailyWorkoutPlayButton.IsEnabled = false;
        DailyWorkoutPlayButton.Opacity = 0.55;
        DailyWorkoutPlayButton.Text = "\u203A";
        DailyWorkoutPlayButton.WidthRequest = 72;
    }

    DateTime ResolveNextWorkoutAvailableAtLocal()
    {
        var latestPlayed = BrainScoreService.GetPlayedGameScores()
            .OrderByDescending(item => item.LastPlayedUtc)
            .FirstOrDefault();

        if (latestPlayed is null)
        {
            return DateTime.Now.Date.AddDays(1);
        }

        return latestPlayed.LastPlayedUtc.ToLocalTime().AddDays(1);
    }

    async Task ShowNewAchievementToastsAsync()
    {
        if (_isAchievementToastSequenceRunning)
        {
            return;
        }

        var unlocked = await AchievementsService.GetNewlyUnlockedAchievementsAsync();
        if (unlocked.Count == 0)
        {
            return;
        }

        _isAchievementToastSequenceRunning = true;

        try
        {
            foreach (var achievement in unlocked)
            {
                await ShowAchievementToastAsync(achievement);
                await Task.Delay(220);
            }
        }
        finally
        {
            _isAchievementToastSequenceRunning = false;
        }
    }

    async Task ShowAchievementToastAsync(AchievementItem achievement)
    {
        AchievementToastIcon.Source = achievement.IconSource;
        AchievementToastTitle.Text = "Achievement unlocked";
        AchievementToastMessage.Text = achievement.Title;

        AchievementToast.IsVisible = true;
        AchievementToast.Opacity = 0;
        AchievementToast.TranslationY = 24;

        await Task.WhenAll(
            AchievementToast.FadeTo(1, 180, Easing.CubicOut),
            AchievementToast.TranslateTo(0, 0, 180, Easing.CubicOut));

        await Task.Delay(1800);

        await Task.WhenAll(
            AchievementToast.FadeTo(0, 150, Easing.CubicIn),
            AchievementToast.TranslateTo(0, 24, 150, Easing.CubicIn));

        AchievementToast.IsVisible = false;
    }

    void ApplySmartRecommendations(BrainSkillScores scores)
    {
        var recs = BuildRecommendations(scores);

        _selectedDailySkill = recs.HeroSkill;
        DailyHeaderLabel.Text = "Next Daily Workout";
        HeroWorkoutTitleLabel.Text = "Assessment";
        PromoTitleLabel.Text = recs.PromoTitle;
        PromoSubtitleLabel.Text = recs.PromoSubtitle;
        PromoIconImage.Source = recs.PromoIconSource;

        ApplyWorkoutRecommendationCard(
            WorkoutItem1IconFrame,
            WorkoutItem1IconImage,
            WorkoutItem1TitleLabel,
            WorkoutItem1SubtitleLabel,
            recs.First);
        ApplyWorkoutRecommendationCard(
            WorkoutItem2IconFrame,
            WorkoutItem2IconImage,
            WorkoutItem2TitleLabel,
            WorkoutItem2SubtitleLabel,
            recs.Second);
        ApplyWorkoutRecommendationCard(
            WorkoutItem3IconFrame,
            WorkoutItem3IconImage,
            WorkoutItem3TitleLabel,
            WorkoutItem3SubtitleLabel,
            recs.Third);
    }

    static void ApplyWorkoutRecommendationCard(
        Border iconFrame,
        Image iconImage,
        Label titleLabel,
        Label subtitleLabel,
        WorkoutRecommendation recommendation)
    {
        var (source, useCustomLogo) = ResolveDailyMixIcon(recommendation);
        iconFrame.BackgroundColor = useCustomLogo
            ? Colors.Transparent
            : Color.FromArgb(recommendation.AccentHex);
        iconImage.Source = source;
        iconImage.WidthRequest = useCustomLogo ? 68 : 34;
        iconImage.HeightRequest = useCustomLogo ? 68 : 34;
        titleLabel.Text = recommendation.Title;
        subtitleLabel.Text = recommendation.Subtitle;
    }

    static (string Source, bool UseCustomLogo) ResolveDailyMixIcon(WorkoutRecommendation recommendation)
    {
        var normalizedTitle = recommendation.Title.Trim().Replace("-", " ");
        return normalizedTitle switch
        {
            "Language" => ("daily_language_logo.png", true),
            "Memory" => ("daily_memory_logo.png", true),
            "Problem Solving" => ("daily_problem_solving_logo.png", true),
            "Focus" => ("daily_focus_logo.png", true),
            "Mental Agility" => ("daily_mental_agility_logo.png", true),
            "Emotion" => ("daily_emotion_logo.svg", true),
            "Coffee Break" => ("daily_streak_keeper_logo.png", true),
            "Streak Keeper" => ("daily_streak_keeper_logo.png", true),
            _ => (recommendation.IconSource, false)
        };
    }

    void ShowTaskLoadingState()
    {
        ShowTaskLoadingState(DailyTasksHost, "Loading daily tasks...");
        ShowTaskLoadingState(WeeklyTasksHost, "Loading weekly tasks...");
    }

    static void ShowTaskLoadingState(VerticalStackLayout host, string message)
    {
        host.Children.Clear();
        host.Children.Add(new Border
        {
            Padding = new Thickness(14, 12),
            BackgroundColor = Color.FromArgb("#F4F8FC"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = new HorizontalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new ActivityIndicator
                    {
                        IsRunning = true,
                        Color = Color.FromArgb("#3B9EF5"),
                        WidthRequest = 20,
                        HeightRequest = 20,
                        VerticalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = message,
                        FontSize = 13,
                        TextColor = Color.FromArgb("#6C8198"),
                        VerticalTextAlignment = TextAlignment.Center
                    }
                }
            }
        });
    }

    void BuildMoreWorkoutSheetItems(BrainSkillScores scores)
    {
        var recs = BuildRecommendations(scores);
        var weakestSkill = BuildSkillSnapshots(scores, BrainScoreService.GetOverTimeTrend(lookbackDays: 14, maxPoints: 14))
            .OrderBy(x => x.Score)
            .First()
            .Skill;

        _advancedWorkoutItems = new()
        {
            new MoreWorkoutSheetItem("Just for you!", "A workout designed with your progress in mind.", "#70C9FF", "just_for_you_logo.svg", "#47AEF5", () => OpenWorkoutCollectionAsync(CreateJustForYouPage(recs.HeroSkill))),
            new MoreWorkoutSheetItem("Coffee Break", "A short and sharp workout.", "#FF9CA0", "coffee_break_logo.png", "#FF6D6D", () => OpenWorkoutCollectionAsync(CreateCoffeeBreakPage())),
            new MoreWorkoutSheetItem("Weakest Link", "Games you could improve on.", "#C74AD7", "weakest_link_logo.png", "#B63AD0", () => OpenWorkoutCollectionAsync(CreateWeakestLinkPage(weakestSkill))),
            new MoreWorkoutSheetItem("Low Rank", "Games you could rank better for.", "#F5A132", "low_rank_logo.png", "#F09017", () => OpenWorkoutCollectionAsync(CreateLowRankPage(weakestSkill))),
            new MoreWorkoutSheetItem("The Total Workout", "Games to challenge all 7 cognitive functions.", "#7DCBFF", "total_workout_logo.svg", "#47AEF5", () => OpenWorkoutCollectionAsync(CreateTotalWorkoutPage())),
            new MoreWorkoutSheetItem("Smooth Sailing", "Here are some games that are easier to make progress with.", "#60D1B4", "smooth_sailing_logo.svg", "#3ABF9C", () => OpenWorkoutCollectionAsync(CreateSmoothSailingPage()))
        };

        _skillWorkoutItems = new()
        {
            new MoreWorkoutSheetItem(
                "Language",
                "Games to challenge your language skills.",
                "#6D63F4",
                "daily_language_logo.png",
                "#5E59F0",
                () => PageTransitionService.PushAsync(Navigation, new SkillGamesPage(BrainSkill.Language, "Language", "Games to challenge your language skills.", "#6B63F5", "#3F39CE"))),
            new MoreWorkoutSheetItem(
                "Problem Solving",
                "Games to make you think creatively.",
                "#67E66F",
                "daily_problem_solving_logo.png",
                "#48C55A",
                () => PageTransitionService.PushAsync(Navigation, new SkillGamesPage(BrainSkill.ProblemSolving, "Problem Solving", "Games to make you think creatively.", "#67E66F", "#1E9F48"))),
            new MoreWorkoutSheetItem(
                "Focus",
                "Games to keep your mind on point.",
                "#FF6A78",
                "daily_focus_logo.png",
                "#F14C60",
                () => PageTransitionService.PushAsync(Navigation, new SkillGamesPage(BrainSkill.Focus, "Focus", "Games to keep your mind on point.", "#FF6A78", "#DD3359"))),
            new MoreWorkoutSheetItem(
                "Mental Agility",
                "Games to help you move between tasks easily.",
                "#5A91FF",
                "daily_mental_agility_logo.png",
                "#3D7BF0",
                () => PageTransitionService.PushAsync(Navigation, new SkillGamesPage(BrainSkill.MentalAgility, "Mental Agility", "Games to help you move between tasks easily.", "#44A7FF", "#197BDB"))),
            new MoreWorkoutSheetItem(
                "Emotion",
                "Games to help you deal with the world around us.",
                "#B65AF6",
                "daily_emotion_logo.svg",
                "#9747E5",
                () => PageTransitionService.PushAsync(Navigation, new SkillGamesPage(BrainSkill.Emotion, "Emotion", "Games to help you deal with the world around us.", "#B65AF6", "#9747E5"))),
            new MoreWorkoutSheetItem(
                "Memory",
                "Games to keep your memory banks stocked.",
                "#F7B038",
                "daily_memory_logo.png",
                "#EB9D13",
                () => PageTransitionService.PushAsync(Navigation, new SkillGamesPage(BrainSkill.Memory, "Memory", "Games to keep your memory banks stocked.", "#F7B038", "#E18A00")))
        };
    }

    void ApplyMoreWorkoutTab()
    {
        var source = _activeWorkoutTabIndex == 0 ? _advancedWorkoutItems : _skillWorkoutItems;
        if (source.Count == 0)
        {
            return;
        }

        var rows = new[]
        {
            (SheetItem1IconFrame, SheetItem1IconImage, SheetItem1TitleLabel, SheetItem1SubtitleLabel, SheetItem1BadgeLabel),
            (SheetItem2IconFrame, SheetItem2IconImage, SheetItem2TitleLabel, SheetItem2SubtitleLabel, SheetItem2BadgeLabel),
            (SheetItem3IconFrame, SheetItem3IconImage, SheetItem3TitleLabel, SheetItem3SubtitleLabel, SheetItem3BadgeLabel),
            (SheetItem4IconFrame, SheetItem4IconImage, SheetItem4TitleLabel, SheetItem4SubtitleLabel, SheetItem4BadgeLabel),
            (SheetItem5IconFrame, SheetItem5IconImage, SheetItem5TitleLabel, SheetItem5SubtitleLabel, SheetItem5BadgeLabel),
            (SheetItem6IconFrame, SheetItem6IconImage, SheetItem6TitleLabel, SheetItem6SubtitleLabel, SheetItem6BadgeLabel)
        };

        var visibleCount = Math.Min(rows.Length, source.Count);
        for (int i = 0; i < visibleCount; i++)
        { 
            var item = source[i];
            rows[i].Item1.BackgroundColor = Color.FromArgb(item.AccentHex);
            rows[i].Item2.Source = item.IconSource;
            bool useCustomLogo = IsLargeWorkoutLogo(item.IconSource);
            rows[i].Item1.BackgroundColor = useCustomLogo ? Colors.Transparent : Color.FromArgb(item.AccentHex);
            rows[i].Item2.WidthRequest = useCustomLogo ? 74 : 38;
            rows[i].Item2.HeightRequest = useCustomLogo ? 74 : 38;
            rows[i].Item3.Text = item.Title;
            rows[i].Item4.Text = item.Subtitle;
            rows[i].Item5.TextColor = Color.FromArgb(item.BadgeTextColorHex);
        }

        for (int i = visibleCount; i < rows.Length; i++)
        {
            rows[i].Item1.BackgroundColor = Color.FromArgb("#EAF0F6");
            rows[i].Item2.Source = null;
            rows[i].Item3.Text = string.Empty;
            rows[i].Item4.Text = string.Empty;
        }

        AdvancedTabButton.BackgroundColor = _activeWorkoutTabIndex == 0 ? Color.FromArgb("#47AEF5") : Colors.Transparent;
        AdvancedTabButton.TextColor = _activeWorkoutTabIndex == 0 ? Colors.White : Color.FromArgb("#7C7C82");
        SkillsTabButton.BackgroundColor = _activeWorkoutTabIndex == 1 ? Color.FromArgb("#47AEF5") : Colors.Transparent;
        SkillsTabButton.TextColor = _activeWorkoutTabIndex == 1 ? Colors.White : Color.FromArgb("#7C7C82");
    }

    static bool IsLargeWorkoutLogo(string iconSource)
    {
        return iconSource.StartsWith("daily_", StringComparison.OrdinalIgnoreCase) ||
               iconSource.Equals("coffee_break_logo.png", StringComparison.OrdinalIgnoreCase) ||
               iconSource.Equals("weakest_link_logo.png", StringComparison.OrdinalIgnoreCase) ||
               iconSource.Equals("low_rank_logo.png", StringComparison.OrdinalIgnoreCase) ||
               iconSource.Equals("total_workout_logo.svg", StringComparison.OrdinalIgnoreCase) ||
               iconSource.Equals("smooth_sailing_logo.svg", StringComparison.OrdinalIgnoreCase) ||
               iconSource.Equals("just_for_you_logo.svg", StringComparison.OrdinalIgnoreCase);
    }

    static TodayRecommendationSet BuildRecommendations(BrainSkillScores scores)
    {
        var trend = BrainScoreService.GetOverTimeTrend(lookbackDays: 14, maxPoints: 14);
        var snapshots = BuildSkillSnapshots(scores, trend);

        var weakest = snapshots
            .OrderBy(x => x.Score)
            .ThenBy(x => x.RecentDelta)
            .First();

        var biggestDrop = snapshots
            .OrderBy(x => x.RecentDelta)
            .ThenBy(x => x.Score)
            .First();

        var biggestRise = snapshots
            .OrderByDescending(x => x.RecentDelta)
            .ThenBy(x => x.Score)
            .First();

        var streak = BrainScoreService.GetCurrentStreakDays();
        var heroHeader = $"{DateTime.Now.ToString("dddd", CultureInfo.InvariantCulture).ToUpperInvariant()}'S";
        var heroTitle = weakest.RecentDelta switch
        {
            <= -4 => $"{weakest.Name} Reset",
            >= 4 => $"{weakest.Name} Power",
            _ => $"{weakest.Name} Workout"
        };

        string promoTitle;
        string promoSubtitle;
        string promoIconSource;

        if (biggestDrop.RecentDelta <= -4)
        {
            promoTitle = $"{biggestDrop.Name} slipped";
            promoSubtitle = $"Down {Math.Abs(biggestDrop.RecentDelta)} pts this week. A focused run can recover it.";
            promoIconSource = GetSkillIconSource(biggestDrop.Skill);
        }
        else if (biggestRise.RecentDelta >= 4)
        {
            promoTitle = $"{biggestRise.Name} is climbing";
            promoSubtitle = $"Up {biggestRise.RecentDelta} pts recently. Keep your momentum while it is hot.";
            promoIconSource = "ach_expedition.svg";
        }
        else
        {
            promoTitle = "Get Pro for Free";
            promoSubtitle = "Unlock more workouts, sharper insights, and a more complete training plan.";
            promoIconSource = "ach_all_terrain.svg";
        }

        var first = new WorkoutRecommendation(
            Title: $"{weakest.Name}",
            Subtitle: weakest.RecentDelta switch
            {
                <= -4 => $"Recent drop ({Math.Abs(weakest.RecentDelta)} pts). Prioritize recovery drills.",
                >= 4 => "Fast improvement detected. Push now to lock in gains.",
                _ => $"Games to make your {weakest.Name.ToLowerInvariant()} stronger."
            },
            AccentHex: weakest.AccentHex,
            IconSource: GetSkillIconSource(weakest.Skill));

        WorkoutRecommendation second;
        if (biggestDrop.RecentDelta <= -3 && biggestDrop.Skill != weakest.Skill)
        {
            second = new WorkoutRecommendation(
                Title: $"{biggestDrop.Name}",
                Subtitle: $"This skill dipped by {Math.Abs(biggestDrop.RecentDelta)} pts. Add one recovery round.",
                AccentHex: biggestDrop.AccentHex,
                IconSource: GetSkillIconSource(biggestDrop.Skill));
        }
        else if (biggestRise.RecentDelta >= 4 && biggestRise.Skill != weakest.Skill)
        {
            second = new WorkoutRecommendation(
                Title: $"{biggestRise.Name}",
                Subtitle: $"Up {biggestRise.RecentDelta} pts this week. Keep building while confidence is high.",
                AccentHex: biggestRise.AccentHex,
                IconSource: "ach_expedition.svg");
        }
        else
        {
            second = new WorkoutRecommendation(
                Title: "Language",
                Subtitle: "Games to challenge your language skills.",
                AccentHex: "#6B63F5",
                IconSource: "ach_word_lift.svg");
        }

        var third = new WorkoutRecommendation(
            Title: streak == 0 ? "Coffee Break" : "Streak Keeper",
            Subtitle: streak == 0
                ? "A short and sharp workout."
                : $"Protect your {streak}-day streak with one quick session today.",
            AccentHex: "#FF8F8F",
            IconSource: streak == 0 ? "ach_steady_climber.svg" : "ach_steady_climber.svg");

        return new TodayRecommendationSet(
            HeroSkill: weakest.Skill,
            HeroHeader: heroHeader,
            HeroTitle: heroTitle,
            PromoTitle: promoTitle,
            PromoSubtitle: promoSubtitle,
            PromoIconSource: promoIconSource,
            First: first,
            Second: second,
            Third: third);
    }

    static IReadOnlyList<SkillSnapshot> BuildSkillSnapshots(
        BrainSkillScores scores,
        IReadOnlyList<BrainScoreTrendPoint> trend)
    {
        var latest = trend.Count > 0 ? trend[^1].Scores : scores;
        var startIndex = trend.Count > 0 ? Math.Max(0, trend.Count - 8) : 0;
        var recentStart = trend.Count > 0 ? trend[startIndex].Scores : scores;

        return new[]
        {
            BuildSkillSnapshot(BrainSkill.Memory, latest, recentStart),
            BuildSkillSnapshot(BrainSkill.ProblemSolving, latest, recentStart),
            BuildSkillSnapshot(BrainSkill.Language, latest, recentStart),
            BuildSkillSnapshot(BrainSkill.Focus, latest, recentStart)
        };
    }

    static SkillSnapshot BuildSkillSnapshot(
        BrainSkill skill,
        BrainSkillScores latest,
        BrainSkillScores recentStart)
    {
        var score = GetSkillScore(skill, latest);
        var start = GetSkillScore(skill, recentStart);
        var delta = score - start;
        var (name, accentHex) = GetSkillDisplay(skill);

        return new SkillSnapshot(
            Skill: skill,
            Name: name,
            Score: score,
            RecentDelta: delta,
            AccentHex: accentHex);
    }

    static int GetSkillScore(BrainSkill skill, BrainSkillScores scores)
    {
        return skill switch
        {
            BrainSkill.Memory => scores.Memory,
            BrainSkill.ProblemSolving => scores.ProblemSolving,
            BrainSkill.Language => scores.Language,
            BrainSkill.Focus => scores.Focus,
            _ => scores.MentalAgility
        };
    }

    static (string Name, string AccentHex) GetSkillDisplay(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Memory => ("Memory", "#F7B038"),
            BrainSkill.ProblemSolving => ("Problem Solving", "#67E66F"),
            BrainSkill.Language => ("Language", "#6B63F5"),
            BrainSkill.Focus => ("Focus", "#FF6A78"),
            _ => ("Mental Agility", "#5A91FF")
        };
    }

    static string GetSkillIconSource(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Memory => "ach_memory_surge.svg",
            BrainSkill.ProblemSolving => "ach_logic_fire.svg",
            BrainSkill.Language => "ach_word_lift.svg",
            BrainSkill.Focus => "ach_focus_lock.svg",
            _ => "ach_all_terrain.svg"
        };
    }

    async void OnPlayClicked(object sender, EventArgs e)
    {
        if (_nextWorkoutAvailableAtLocal > DateTime.Now)
        {
            await DisplayAlert(
                "Daily mix locked",
                "Your next daily mix is not ready yet. Check the countdown and come back when it refreshes.",
                "OK");
            return;
        }

        await LaunchRecommendedWorkoutAsync(_selectedDailySkill);
    }

    async void OnAssessmentTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new TestsPage());
    }

    async void OnWorkoutItem1Tapped(object sender, TappedEventArgs e)
    {
        await OpenSkillCollectionAsync(WorkoutItem1TitleLabel.Text);
    }

    async void OnWorkoutItem2Tapped(object sender, TappedEventArgs e)
    {
        await OpenSkillCollectionAsync(WorkoutItem2TitleLabel.Text);
    }

    async void OnWorkoutItem3Tapped(object sender, TappedEventArgs e)
    {
        var title = WorkoutItem3TitleLabel.Text;
        if (string.Equals(title, "Coffee Break", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(title, "Streak Keeper", StringComparison.OrdinalIgnoreCase))
        {
            await OpenSkillCollectionAsync(title);
            return;
        }

        await OpenSkillCollectionAsync(title);
    }

    async Task LaunchRecommendedWorkoutAsync(BrainSkill skill)
    {
        var gameFactories = GetPlayableGameFactories(skill);
        if (gameFactories.Count == 0)
        {
            await PageTransitionService.PushAsync(Navigation, new WorkoutPlanPage());
            return;
        }

        var pickedFactory = gameFactories[_random.Next(gameFactories.Count)];
        await PageTransitionService.PushAsync(Navigation, pickedFactory());
    }

    async Task LaunchQuickWorkoutAsync()
    {
        await PageTransitionService.PushAsync(Navigation, new TestYourselfPage(IQCatalog.QuickCheck));
    }

    async Task OpenWorkoutCollectionAsync(ContentPage page)
    {
        await PageTransitionService.PushAsync(Navigation, page);
    }

    static IReadOnlyList<Func<ContentPage>> GetPlayableGameFactories(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Memory => new Func<ContentPage>[]
            {
                static () => new MemoryGamePage(),
                static () => new SpinCycleGamePage()
            },
            BrainSkill.ProblemSolving => new Func<ContentPage>[]
            {
                static () => new MatchaMadnessGamePage(),
                static () => new MovingMathGamePage(),
                static () => new PerilousPathGamePage(),
                static () => new SquareNumbersGamePage()
            },
            BrainSkill.Language => new Func<ContentPage>[]
            {
                static () => new WordALikeGamePage(),
                static () => new WordFreshGamePage()
            },
            BrainSkill.Focus => new Func<ContentPage>[]
            {
                static () => new MustSortGamePage(),
                static () => new PartialMatchGamePage(),
                static () => new TapTrapGamePage(),
                static () => new TrueColorGamePage(),
                static () => new TurtleTrafficGamePage(),
                static () => new UniqueGamePage(),
                static () => new DecoderGamePage()
            },
            _ => Array.Empty<Func<ContentPage>>()
        };
    }

    async void OnAssessmentSummaryClicked(object sender, EventArgs e)
    {
        PopulateAssessmentSummary();
        await OpenAssessmentSummaryAsync();
    }

    async void OnStreakTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.GoToAsync(nameof(StatsPage));
    }

    async void OnMoreWorkoutsTapped(object sender, TappedEventArgs e)
    {
        if (_isMoreWorkoutsAnimating || MoreWorkoutsOverlay.IsVisible)
        {
            return;
        }

        _isMoreWorkoutsAnimating = true;
        MoreWorkoutsOverlay.IsVisible = true;
        MoreWorkoutsOverlay.Opacity = 0;
        MoreWorkoutsPanel.TranslationY = 18;

        await Task.WhenAll(
            MoreWorkoutsOverlay.FadeTo(1, 180, Easing.CubicOut),
            MoreWorkoutsPanel.TranslateTo(0, 0, 220, Easing.CubicOut));

        _isMoreWorkoutsAnimating = false;
    }

    async void OnCloseMoreWorkoutsClicked(object sender, EventArgs e)
    {
        await CloseMoreWorkoutsAsync();
    }

    async void OnMoreWorkoutsOverlayTapped(object sender, TappedEventArgs e)
    {
        await CloseMoreWorkoutsAsync();
    }

    async Task CloseMoreWorkoutsAsync()
    {
        if (_isMoreWorkoutsAnimating || !MoreWorkoutsOverlay.IsVisible)
        {
            return;
        }

        _isMoreWorkoutsAnimating = true;

        await Task.WhenAll(
            MoreWorkoutsOverlay.FadeTo(0, 150, Easing.CubicIn),
            MoreWorkoutsPanel.TranslateTo(0, 18, 150, Easing.CubicIn));

        MoreWorkoutsOverlay.IsVisible = false;
        _isMoreWorkoutsAnimating = false;
    }

    void PopulateAssessmentSummary()
    {
        var scores = BrainScoreService.GetCurrentScores();
        var rank = BrainScoreService.GetPeakRankName(scores.PeakScore);
        var streakDays = BrainScoreService.GetCurrentStreakDays();
        var points = GamePointsService.GetBalance();

        AssessmentPeakScoreLabel.Text = scores.PeakScore.ToString();
        AssessmentRankLabel.Text = rank;
        AssessmentMemoryLabel.Text = scores.Memory.ToString();
        AssessmentProblemSolvingLabel.Text = scores.ProblemSolving.ToString();
        AssessmentLanguageLabel.Text = scores.Language.ToString();
        AssessmentFocusLabel.Text = scores.Focus.ToString();
        AssessmentApexPointsLabel.Text = points.ToString("N0");
        AssessmentStreakLabel.Text = streakDays.ToString();

        var strongest = new[]
        {
            ("Memory", scores.Memory),
            ("Problem Solving", scores.ProblemSolving),
            ("Language", scores.Language),
            ("Focus", scores.Focus)
        }.OrderByDescending(x => x.Item2).First();

        AssessmentSummaryBodyLabel.Text =
            $"Your strongest area right now is {strongest.Item1} at {strongest.Item2}. Keep your sessions steady to push your overall rank beyond {rank}.";
        AssessmentRewardBodyLabel.Text =
            $"You have {points:N0} Apex Points ready to spend and a {streakDays}-day streak building momentum. Daily and weekly tasks are the fastest way to grow both.";

        SetAssessmentSummaryTab(overviewSelected: true);
    }

    void SetAssessmentSummaryTab(bool overviewSelected)
    {
        AssessmentOverviewTab.IsVisible = overviewSelected;
        AssessmentRewardsTab.IsVisible = !overviewSelected;

        AssessmentOverviewTabButton.BackgroundColor = overviewSelected ? Color.FromArgb("#FF4377") : Colors.Transparent;
        AssessmentOverviewTabButton.TextColor = overviewSelected ? Colors.White : Color.FromArgb("#C54773");
        AssessmentRewardsTabButton.BackgroundColor = !overviewSelected ? Color.FromArgb("#FF4377") : Colors.Transparent;
        AssessmentRewardsTabButton.TextColor = !overviewSelected ? Colors.White : Color.FromArgb("#C54773");
    }

    async Task OpenAssessmentSummaryAsync()
    {
        if (_isAssessmentSummaryAnimating || AssessmentSummaryOverlay.IsVisible)
        {
            return;
        }

        _isAssessmentSummaryAnimating = true;
        AssessmentSummaryOverlay.IsVisible = true;
        AssessmentSummaryOverlay.Opacity = 0;
        AssessmentSummaryPanel.TranslationY = 24;

        await Task.WhenAll(
            AssessmentSummaryOverlay.FadeTo(1, 180, Easing.CubicOut),
            AssessmentSummaryPanel.TranslateTo(0, 0, 220, Easing.CubicOut));

        _isAssessmentSummaryAnimating = false;
    }

    async Task CloseAssessmentSummaryAsync()
    {
        if (_isAssessmentSummaryAnimating || !AssessmentSummaryOverlay.IsVisible)
        {
            return;
        }

        _isAssessmentSummaryAnimating = true;

        await Task.WhenAll(
            AssessmentSummaryOverlay.FadeTo(0, 150, Easing.CubicIn),
            AssessmentSummaryPanel.TranslateTo(0, 24, 150, Easing.CubicIn));

        AssessmentSummaryOverlay.IsVisible = false;
        _isAssessmentSummaryAnimating = false;
    }

    void OnAssessmentOverviewTabClicked(object sender, EventArgs e) => SetAssessmentSummaryTab(true);

    void OnAssessmentRewardsTabClicked(object sender, EventArgs e) => SetAssessmentSummaryTab(false);

    async void OnCloseAssessmentSummaryClicked(object sender, EventArgs e) => await CloseAssessmentSummaryAsync();

    async void OnAssessmentSummaryOverlayTapped(object sender, TappedEventArgs e) => await CloseAssessmentSummaryAsync();

    void OnAdvancedTabClicked(object sender, EventArgs e)
    {
        _activeWorkoutTabIndex = 0;
        ApplyMoreWorkoutTab();
    }

    void OnSkillsTabClicked(object sender, EventArgs e)
    {
        _activeWorkoutTabIndex = 1;
        ApplyMoreWorkoutTab();
    }

    async void OnSheetItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element ||
            !int.TryParse(element.ClassId, out var index))
        {
            return;
        }

        var source = _activeWorkoutTabIndex == 0 ? _advancedWorkoutItems : _skillWorkoutItems;
        if (index < 0 || index >= source.Count)
        {
            await PageTransitionService.GoToAsync("//games");
            return;
        }

        await CloseMoreWorkoutsAsync();
        await source[index].OnTap();
    }

    async Task OpenSkillCollectionAsync(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        if (TryMapSkillPage(title, out var page))
        {
            await PageTransitionService.PushAsync(Navigation, page);
            return;
        }

        if (TryBuildWorkoutCollectionPage(title, out var collectionPage))
        {
            await PageTransitionService.PushAsync(Navigation, collectionPage);
            return;
        }

        await PageTransitionService.GoToAsync("//games");
    }

    async Task OpenGameByTitleAsync(string title)
    {
        if (GameEntryNavigationService.CreatePage(title.Trim()) is { } page)
        {
            await PageTransitionService.PushAsync(Navigation, page);
            return;
        }

        await PageTransitionService.GoToAsync("//games");
    }

    static bool TryMapSkillPage(string title, out ContentPage page)
    {
        switch (title.Trim())
        {
            case "Problem Solving":
                page = new SkillGamesPage(BrainSkill.ProblemSolving, "Problem Solving", "Games to make you think creatively.", "#67E66F", "#1E9F48");
                return true;
            case "Language":
                page = new SkillGamesPage(BrainSkill.Language, "Language", "Games to challenge your language skills.", "#6B63F5", "#3F39CE");
                return true;
            case "Memory":
                page = new SkillGamesPage(BrainSkill.Memory, "Memory", "Games to keep your memory bank active.", "#F7B038", "#E18A00");
                return true;
            case "Focus":
                page = new SkillGamesPage(BrainSkill.Focus, "Focus", "Games to keep your mind on point.", "#FF6A78", "#DD3359");
                return true;
            case "Mental Agility":
                page = new SkillGamesPage(BrainSkill.MentalAgility, "Mental Agility", "Games to help you move between tasks easily.", "#44A7FF", "#197BDB");
                return true;
            case "Emotion":
                page = new SkillGamesPage(BrainSkill.Emotion, "Emotion", "Games to help you deal with the world around us.", "#B65AF6", "#9747E5");
                return true;
            default:
                page = null!;
                return false;
        }
    }

    bool TryBuildWorkoutCollectionPage(string title, out ContentPage page)
    {
        switch (title.Trim())
        {
            case "Streak Keeper":
                page = CreateStreakKeeperPage();
                return true;
            case "Coffee Break":
                page = CreateCoffeeBreakPage();
                return true;
            case "Just for you!":
                page = CreateJustForYouPage(_selectedDailySkill);
                return true;
            case "Weakest Link":
                page = CreateWeakestLinkPage(GetLowestSkill());
                return true;
            case "Low Rank":
                page = CreateLowRankPage(GetLowestSkill());
                return true;
            case "The Total Workout":
                page = CreateTotalWorkoutPage();
                return true;
            case "Smooth Sailing":
                page = CreateSmoothSailingPage();
                return true;
            default:
                page = null!;
                return false;
        }
    }

    BrainSkill GetLowestSkill()
    {
        var scores = BrainScoreService.GetCurrentScores();
        return BuildSkillSnapshots(scores, BrainScoreService.GetOverTimeTrend(lookbackDays: 14, maxPoints: 14))
            .OrderBy(x => x.Score)
            .First()
            .Skill;
    }

    ContentPage CreateStreakKeeperPage()
    {
        return new SkillGamesPage(
            "Daily mix",
            "Streak Keeper",
            "Quick games to protect your current training streak.",
            "#FF8C99",
            "#E04A6E",
            "daily_streak_keeper_logo.png",
            new[]
            {
                WorkoutCard("Word Fresh", "Language warm-up", "#6B63F5", "#3F39CE", "word_fresh_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Fresh")),
                WorkoutCard("Decoder", "Fast focus reset", "#FF6483", "#DD3359", "focus_decoder_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Decoder")),
                WorkoutCard("Spin Cycle", "Memory sequencing", "#FFBC47", "#E18A00", "spin_cycle_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Spin Cycle")),
                WorkoutCard("Moving Math", "Math under pressure", "#4FD37B", "#1E9F48", "moving_math_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Moving Math"))
            });
    }

    ContentPage CreateJustForYouPage(BrainSkill heroSkill)
    {
        var accent = GetSkillAccent(heroSkill);
        var deep = GetSkillAccentDeep(heroSkill);
        var title = heroSkill switch
        {
            BrainSkill.ProblemSolving => "Problem Solving",
            BrainSkill.Language => "Language",
            BrainSkill.Memory => "Memory",
            BrainSkill.Focus => "Focus",
            BrainSkill.MentalAgility => "Mental Agility",
            BrainSkill.Emotion => "Emotion",
            _ => "Focus"
        };

        return new SkillGamesPage(
            "Advanced workout",
            "Just for you!",
            $"A custom set shaped around your current {title.ToLowerInvariant()} trend.",
            accent,
            deep,
            "just_for_you_logo.svg",
            SkillGamesPage.BuildGamesForSkill(heroSkill, accent, deep));
    }

    ContentPage CreateCoffeeBreakPage()
    {
        return new SkillGamesPage(
            "Advanced workout",
            "Coffee Break",
            "A short, sharp set for a quick mental refresh.",
            "#FF9C54",
            "#F06B21",
            "coffee_break_logo.png",
            new[]
            {
                WorkoutCard("Decoder", "Fast focus sprint", "#FF6483", "#DD3359", "focus_decoder_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Decoder")),
                WorkoutCard("Word Fresh", "Quick word boost", "#6B63F5", "#3F39CE", "word_fresh_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Fresh")),
                WorkoutCard("Partial Match", "Short memory check", "#FFBC47", "#E18A00", "partial_match_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Partial Match")),
                WorkoutCard("Moving Math", "Rapid number flow", "#4FD37B", "#1E9F48", "moving_math_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Moving Math"))
            });
    }

    ContentPage CreateWeakestLinkPage(BrainSkill weakestSkill)
    {
        var accent = "#C74AD7";
        var deep = "#A728C0";
        return new SkillGamesPage(
            "Advanced workout",
            "Weakest Link",
            "A focused set to lift the area that needs the most help right now.",
            accent,
            deep,
            "weakest_link_logo.png",
            SkillGamesPage.BuildGamesForSkill(weakestSkill, GetSkillAccent(weakestSkill), GetSkillAccentDeep(weakestSkill)));
    }

    ContentPage CreateLowRankPage(BrainSkill weakestSkill)
    {
        return new SkillGamesPage(
            "Advanced workout",
            "Low Rank",
            "These games can help raise the categories where you are trailing most.",
            "#F5A132",
            "#E97C13",
            "low_rank_logo.png",
            SkillGamesPage.BuildGamesForSkill(weakestSkill, GetSkillAccent(weakestSkill), GetSkillAccentDeep(weakestSkill)));
    }

    ContentPage CreateTotalWorkoutPage()
    {
        return new SkillGamesPage(
            "Advanced workout",
            "The Total Workout",
            "A balanced all-around set across your main cognitive skills.",
            "#48B7FF",
            "#1578D9",
            "total_workout_logo.svg",
            new[]
            {
                WorkoutCard("Word Fresh", "Language", "#6B63F5", "#3F39CE", "word_fresh_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Fresh")),
                WorkoutCard("Perilous Path", "Memory", "#FFBC47", "#E18A00", "perilous_path_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Perilous Path")),
                WorkoutCard("Square Numbers", "Problem Solving", "#4FD37B", "#1E9F48", "square_numbers_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Square Numbers")),
                WorkoutCard("Decoder", "Focus", "#FF6483", "#DD3359", "focus_decoder_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Decoder")),
                WorkoutCard("Turtle Traffic", "Mental Agility", "#44A7FF", "#197BDB", "turtle_traffic_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Turtle Traffic")),
                WorkoutCard("Smile On Me", "Emotion", "#C56AF8", "#9044C8", "smile_on_me_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Smile On Me"))
            });
    }

    ContentPage CreateSmoothSailingPage()
    {
        return new SkillGamesPage(
            "Advanced workout",
            "Smooth Sailing",
            "A calmer selection of approachable games for steady progress.",
            "#60D1B4",
            "#24A88B",
            "smooth_sailing_logo.svg",
            new[]
            {
                WorkoutCard("Word Fresh", "Gentle language boost", "#6B63F5", "#3F39CE", "word_fresh_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Fresh")),
                WorkoutCard("Partial Match", "Easy memory flow", "#FFBC47", "#E18A00", "partial_match_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Partial Match")),
                WorkoutCard("Decoder", "Short focus training", "#FF6483", "#DD3359", "focus_decoder_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Decoder")),
                WorkoutCard("Turtle Traffic", "Adaptive pacing", "#44A7FF", "#197BDB", "turtle_traffic_icon.svg", () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Turtle Traffic"))
            });
    }

    static SkillGameCardViewModel WorkoutCard(string title, string subtitle, string accent, string deep, string icon, Func<Task>? openAsync, bool isPlayable = true)
        => new(title, subtitle, accent, deep, string.Empty, icon, isPlayable, openAsync);

    static string GetSkillAccent(BrainSkill skill) => skill switch
    {
        BrainSkill.Language => "#6B63F5",
        BrainSkill.Memory => "#F7B038",
        BrainSkill.ProblemSolving => "#67E66F",
        BrainSkill.Focus => "#FF6A78",
        BrainSkill.MentalAgility => "#44A7FF",
        BrainSkill.Emotion => "#B65AF6",
        _ => "#44A7FF"
    };

    static string GetSkillAccentDeep(BrainSkill skill) => skill switch
    {
        BrainSkill.Language => "#3F39CE",
        BrainSkill.Memory => "#E18A00",
        BrainSkill.ProblemSolving => "#1E9F48",
        BrainSkill.Focus => "#DD3359",
        BrainSkill.MentalAgility => "#197BDB",
        BrainSkill.Emotion => "#9747E5",
        _ => "#197BDB"
    };

}
