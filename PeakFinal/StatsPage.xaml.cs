using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Peak;

public partial class StatsPage : ContentPage
{
    private bool _isRankLadderExpanded;
    private bool _isRankLadderAnimating;
    private const int PeakGoalHorizon = 92;
    private const uint StatsBarAnimationLength = 1000;
    private double _peakFillTargetWidth;
    private double _memoryFillTargetWidth;
    private double _problemFillTargetWidth;
    private double _languageFillTargetWidth;
    private double _agilityFillTargetWidth;
    private double _focusFillTargetWidth;
    private double _memoryBenchmarkTargetWidth;
    private double _problemBenchmarkTargetWidth;
    private double _languageBenchmarkTargetWidth;
    private double _agilityBenchmarkTargetWidth;
    private double _focusBenchmarkTargetWidth;

    private static readonly Color[] DefaultSkillBarColors =
    {
        Color.FromArgb("#F5A623"),
        Color.FromArgb("#35C85A"),
        Color.FromArgb("#5A60F0"),
        Color.FromArgb("#1E88FF"),
        Color.FromArgb("#FF2D55")
    };

    private static readonly Color[] ColorSafeSkillBarColors =
    {
        Color.FromArgb("#E69F00"),
        Color.FromArgb("#009E73"),
        Color.FromArgb("#0072B2"),
        Color.FromArgb("#CC79A7"),
        Color.FromArgb("#D55E00")
    };

    public StatsPage()
    {
        InitializeComponent();

        SelectTab(0);
        ApplyLiveScores();
        ApplyAccessibility();
        SetRankLadderExpandedState(false);
        ApplyBarWidthsImmediate();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            ApplyLiveScores();
            ApplyAccessibility();
            await AnimateStatBarsAsync();
        }
        catch
        {
            ApplyAccessibility();
            SetScores(0, 0, 0, 0, 0, 0);
            ApplyGoal(0, hasProgress: false);
            PeakRankLabel.Text = "Basecamp";
            PeakRankHintLabel.Text = "Stats will appear after your first saved session.";
            RenderRankLadder(0);
            ApplyBarWidthsImmediate();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CompareBenchmarkService.ClearActiveComparison();
    }

    void ApplyLiveScores()
    {
        var scores = BrainScoreService.GetCurrentScores();
        var recordedSessions = BrainScoreService.GetRecordedSessionCount();

        if (recordedSessions == 0)
        {
            SetScores(
                peakScore: 0,
                memory: 0,
                problemSolving: 0,
                language: 0,
                mentalAgility: 0,
                focus: 0);
            ApplyGoal(0, hasProgress: false);
            ApplyComparison(scores);
            PeakRankLabel.Text = "Basecamp";
            PeakRankHintLabel.Text = "Stats will appear after your first saved session.";
            RankProgressLabel.Text = "Complete your first saved session to start the rank ladder.";
            RenderRankLadder(0);
            return;
        }

        var rank = BrainScoreService.GetPeakRankInfo(scores.PeakScore);

        SetScores(
            peakScore: scores.PeakScore,
            memory: scores.Memory,
            problemSolving: scores.ProblemSolving,
            language: scores.Language,
            mentalAgility: scores.MentalAgility,
            focus: scores.Focus);

        ApplyGoal(scores.PeakScore, hasProgress: true);
        ApplyComparison(scores);

        PeakRankLabel.Text = rank.Name;
        if (rank.NextMinScore is null || string.IsNullOrWhiteSpace(rank.NextName))
        {
            PeakRankHintLabel.Text = "Top rank reached";
        }
        else
        {
            var pointsLeft = Math.Max(0, rank.NextMinScore.Value - scores.PeakScore);
            PeakRankHintLabel.Text = $"{pointsLeft} pts to {rank.NextName}";
        }

        RenderRankLadder(scores.PeakScore);
    }

    void SetScores(int peakScore, int memory, int problemSolving, int language, int mentalAgility, int focus)
    {
        PeakScoreLabel.Text = $"{peakScore}/1000";

        MemoryValue.Text = memory.ToString();
        ProblemValue.Text = problemSolving.ToString();
        LanguageValue.Text = language.ToString();
        AgilityValue.Text = mentalAgility.ToString();
        FocusValue.Text = focus.ToString();

        _peakFillTargetWidth = MapPeakToWidth(peakScore);
        _memoryFillTargetWidth = MapSkillToWidth(memory);
        _problemFillTargetWidth = MapSkillToWidth(problemSolving);
        _languageFillTargetWidth = MapSkillToWidth(language);
        _agilityFillTargetWidth = MapSkillToWidth(mentalAgility);
        _focusFillTargetWidth = MapSkillToWidth(focus);
    }

    void ApplyGoal(int peakScore, bool hasProgress)
    {
        if (!hasProgress || peakScore <= 0)
        {
            PeakGoalLabel.Text = "0";
            GoalMarker.IsVisible = false;
            GoalMarker.TranslationX = 0;
            return;
        }

        var goal = Math.Min(1000, Math.Max(peakScore + PeakGoalHorizon, 180));
        PeakGoalLabel.Text = goal.ToString();
        GoalMarker.IsVisible = true;
        GoalMarker.TranslationX = MapPeakToWidth(goal, max: 240);
    }

    void ApplyComparison(BrainSkillScores scores)
    {
        var hasComparison = CompareBenchmarkService.HasActiveComparison();
        SetComparisonVisibility(hasComparison);

        if (!hasComparison)
        {
            return;
        }

        var selection = CompareBenchmarkService.GetSelection();
        var benchmark = CompareBenchmarkService.GetBenchmark(selection);

        CompareTargetLabel.Text = benchmark.Label.ToUpperInvariant();

        _memoryBenchmarkTargetWidth = MapSkillToWidth(benchmark.Memory);
        _problemBenchmarkTargetWidth = MapSkillToWidth(benchmark.ProblemSolving);
        _languageBenchmarkTargetWidth = MapSkillToWidth(benchmark.Language);
        _agilityBenchmarkTargetWidth = MapSkillToWidth(benchmark.MentalAgility);
        _focusBenchmarkTargetWidth = MapSkillToWidth(benchmark.Focus);

        _memoryFillTargetWidth = MapSkillToWidth(scores.Memory);
        _problemFillTargetWidth = MapSkillToWidth(scores.ProblemSolving);
        _languageFillTargetWidth = MapSkillToWidth(scores.Language);
        _agilityFillTargetWidth = MapSkillToWidth(scores.MentalAgility);
        _focusFillTargetWidth = MapSkillToWidth(scores.Focus);
    }

    void SetComparisonVisibility(bool isVisible)
    {
        CompareHeaderRow.IsVisible = isVisible;
        MemoryBenchmarkRow.IsVisible = isVisible;
        ProblemBenchmarkRow.IsVisible = isVisible;
        LanguageBenchmarkRow.IsVisible = isVisible;
        AgilityBenchmarkRow.IsVisible = isVisible;
        FocusBenchmarkRow.IsVisible = isVisible;
    }

    static double MapToWidth(int value, int maxScore, double maxWidth)
    {
        if (value <= 0 || maxScore <= 0)
        {
            return 0;
        }

        var clamped = Math.Clamp(value, 0, maxScore);
        return maxWidth * (clamped / (double)maxScore);
    }

    static double MapPeakToWidth(int value, double max = 260)
    {
        return MapToWidth(value, maxScore: 1000, maxWidth: max);
    }

    static double MapSkillToWidth(int value, double max = 260)
    {
        return MapToWidth(value, maxScore: 200, maxWidth: max);
    }

    void ApplyBarWidthsImmediate()
    {
        PeakFill.WidthRequest = _peakFillTargetWidth;
        MemoryFill.WidthRequest = _memoryFillTargetWidth;
        ProblemFill.WidthRequest = _problemFillTargetWidth;
        LanguageFill.WidthRequest = _languageFillTargetWidth;
        AgilityFill.WidthRequest = _agilityFillTargetWidth;
        FocusFill.WidthRequest = _focusFillTargetWidth;
        MemoryBenchmarkFill.WidthRequest = _memoryBenchmarkTargetWidth;
        ProblemBenchmarkFill.WidthRequest = _problemBenchmarkTargetWidth;
        LanguageBenchmarkFill.WidthRequest = _languageBenchmarkTargetWidth;
        AgilityBenchmarkFill.WidthRequest = _agilityBenchmarkTargetWidth;
        FocusBenchmarkFill.WidthRequest = _focusBenchmarkTargetWidth;
    }

    async Task AnimateStatBarsAsync()
    {
        const double minPrimaryWidth = 6;
        const double minPeakWidth = 10;
        const double minBenchmarkWidth = 4;

        ResetBarForAnimation(PeakFill, _peakFillTargetWidth > 0 ? minPeakWidth : 0);
        ResetBarForAnimation(MemoryFill, _memoryFillTargetWidth > 0 ? minPrimaryWidth : 0);
        ResetBarForAnimation(ProblemFill, _problemFillTargetWidth > 0 ? minPrimaryWidth : 0);
        ResetBarForAnimation(LanguageFill, _languageFillTargetWidth > 0 ? minPrimaryWidth : 0);
        ResetBarForAnimation(AgilityFill, _agilityFillTargetWidth > 0 ? minPrimaryWidth : 0);
        ResetBarForAnimation(FocusFill, _focusFillTargetWidth > 0 ? minPrimaryWidth : 0);

        if (MemoryBenchmarkRow.IsVisible) ResetBarForAnimation(MemoryBenchmarkFill, _memoryBenchmarkTargetWidth > 0 ? minBenchmarkWidth : 0);
        if (ProblemBenchmarkRow.IsVisible) ResetBarForAnimation(ProblemBenchmarkFill, _problemBenchmarkTargetWidth > 0 ? minBenchmarkWidth : 0);
        if (LanguageBenchmarkRow.IsVisible) ResetBarForAnimation(LanguageBenchmarkFill, _languageBenchmarkTargetWidth > 0 ? minBenchmarkWidth : 0);
        if (AgilityBenchmarkRow.IsVisible) ResetBarForAnimation(AgilityBenchmarkFill, _agilityBenchmarkTargetWidth > 0 ? minBenchmarkWidth : 0);
        if (FocusBenchmarkRow.IsVisible) ResetBarForAnimation(FocusBenchmarkFill, _focusBenchmarkTargetWidth > 0 ? minBenchmarkWidth : 0);

        await Task.Delay(35);

        var animations = new List<Task>
        {
            AnimateWidthAsync(PeakFill, _peakFillTargetWidth),
            AnimateWidthAsync(MemoryFill, _memoryFillTargetWidth),
            AnimateWidthAsync(ProblemFill, _problemFillTargetWidth),
            AnimateWidthAsync(LanguageFill, _languageFillTargetWidth),
            AnimateWidthAsync(AgilityFill, _agilityFillTargetWidth),
            AnimateWidthAsync(FocusFill, _focusFillTargetWidth)
        };

        if (MemoryBenchmarkRow.IsVisible) animations.Add(AnimateWidthAsync(MemoryBenchmarkFill, _memoryBenchmarkTargetWidth));
        if (ProblemBenchmarkRow.IsVisible) animations.Add(AnimateWidthAsync(ProblemBenchmarkFill, _problemBenchmarkTargetWidth));
        if (LanguageBenchmarkRow.IsVisible) animations.Add(AnimateWidthAsync(LanguageBenchmarkFill, _languageBenchmarkTargetWidth));
        if (AgilityBenchmarkRow.IsVisible) animations.Add(AnimateWidthAsync(AgilityBenchmarkFill, _agilityBenchmarkTargetWidth));
        if (FocusBenchmarkRow.IsVisible) animations.Add(AnimateWidthAsync(FocusBenchmarkFill, _focusBenchmarkTargetWidth));

        await Task.WhenAll(animations);
    }

    static void ResetBarForAnimation(VisualElement element, double width)
    {
        element.AbortAnimation("WidthRequestAnimation");
        element.WidthRequest = width;
    }

    static Task AnimateWidthAsync(VisualElement element, double targetWidth)
    {
        var tcs = new TaskCompletionSource();
        double startWidth = element.WidthRequest;
        element.AbortAnimation("WidthRequestAnimation");

        var animation = new Animation(
            callback: width => element.WidthRequest = width,
            start: startWidth,
            end: targetWidth,
            easing: Easing.CubicOut);

        animation.Commit(
            owner: element,
            name: "WidthRequestAnimation",
            rate: 16,
            length: StatsBarAnimationLength,
            finished: (_, __) => tcs.TrySetResult());

        return tcs.Task;
    }

    void RenderRankLadder(int peakScore)
    {
        RankLadderContainer.Children.Clear();
        var hasRecordedSessions = BrainScoreService.GetRecordedSessionCount() > 0;

        var options = AccessibilityService.GetOptions();
        var currentRowColor = options.HighContrastEnabled
            ? Color.FromArgb("#DDF1FF")
            : Color.FromArgb("#E9F5FF");
        var currentBorderColor = options.HighContrastEnabled
            ? Color.FromArgb("#1E88E5")
            : Color.FromArgb("#7ABFFF");
        var defaultBorderColor = options.HighContrastEnabled
            ? Color.FromArgb("#C2CDD8")
            : Color.FromArgb("#E4E8EC");
        var nameColor = options.HighContrastEnabled
            ? Color.FromArgb("#13202E")
            : Color.FromArgb("#2A3644");
        var secondaryColor = options.HighContrastEnabled
            ? Color.FromArgb("#42515F")
            : Color.FromArgb("#6E7B88");
        var currentAccent = options.ColorSafeChartsEnabled
            ? Color.FromArgb("#0072B2")
            : Color.FromArgb("#0A8FE7");
        var statusDefault = options.HighContrastEnabled
            ? Color.FromArgb("#4B5C6C")
            : Color.FromArgb("#7A8795");

        var tiers = BrainScoreService.GetPeakRankTiers();
        var nextTier = tiers.FirstOrDefault(t => peakScore < t.MinScore);

        RankProgressLabel.Text = !hasRecordedSessions
            ? "Complete your first saved session to start the rank ladder."
            : nextTier is null
                ? "You reached the highest rank. Keep it up."
                : $"{Math.Max(0, nextTier.MinScore - peakScore)} points to unlock {nextTier.Name}.";

        foreach (var tier in tiers)
        {
            var isCurrent = peakScore >= tier.MinScore && peakScore <= tier.MaxScore;
            var isUnlocked = peakScore >= tier.MinScore;

            var statusText = isCurrent
                ? "Current rank"
                : isUnlocked
                    ? "Unlocked"
                    : $"+{Math.Max(0, tier.MinScore - peakScore)} to unlock";

            var rangeText = tier.MaxScore >= 1000
                ? $"{tier.MinScore}+"
                : $"{tier.MinScore}-{tier.MaxScore}";

            var row = new Frame
            {
                CornerRadius = 12,
                HasShadow = false,
                Padding = new Thickness(10, 8),
                BackgroundColor = isCurrent ? currentRowColor : Colors.White,
                BorderColor = isCurrent ? currentBorderColor : defaultBorderColor
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10
            };

            var icon = new Image
            {
                Source = tier.IconSource,
                WidthRequest = 24,
                HeightRequest = 24,
                VerticalOptions = LayoutOptions.Center
            };

            var nameLabel = new Label
            {
                Text = tier.Name,
                FontSize = 15,
                FontAttributes = isCurrent ? FontAttributes.Bold : FontAttributes.None,
                TextColor = nameColor,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(nameLabel, 1);

            var rangeLabel = new Label
            {
                Text = rangeText,
                FontSize = 12,
                TextColor = secondaryColor,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(rangeLabel, 2);

            var statusLabel = new Label
            {
                Text = statusText,
                FontSize = 12,
                FontAttributes = isCurrent ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isCurrent ? currentAccent : statusDefault,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(statusLabel, 3);

            grid.Children.Add(icon);
            grid.Children.Add(nameLabel);
            grid.Children.Add(rangeLabel);
            grid.Children.Add(statusLabel);

            row.Content = grid;
            RankLadderContainer.Children.Add(row);
        }

        AccessibilityService.ApplyTextScale(this);
    }

    async void OnRankLadderHeaderTapped(object sender, TappedEventArgs e)
    {
        await ToggleRankLadderAsync();
    }

    async Task ToggleRankLadderAsync()
    {
        if (_isRankLadderAnimating)
        {
            return;
        }

        _isRankLadderAnimating = true;

        if (_isRankLadderExpanded)
        {
            RankLadderChevronLabel.Text = "v";
            await Task.WhenAll(
                RankLadderContainer.FadeTo(0, 120, Easing.CubicIn),
                RankLadderContainer.TranslateTo(0, -6, 120, Easing.CubicIn));
            RankLadderContainer.IsVisible = false;
            _isRankLadderExpanded = false;
        }
        else
        {
            RankLadderContainer.IsVisible = true;
            RankLadderContainer.Opacity = 0;
            RankLadderContainer.TranslationY = -6;
            RankLadderChevronLabel.Text = "^";
            await Task.WhenAll(
                RankLadderContainer.FadeTo(1, 150, Easing.CubicOut),
                RankLadderContainer.TranslateTo(0, 0, 150, Easing.CubicOut));
            _isRankLadderExpanded = true;
        }

        _isRankLadderAnimating = false;
    }

    void SetRankLadderExpandedState(bool expanded)
    {
        _isRankLadderExpanded = expanded;
        RankLadderChevronLabel.Text = expanded ? "^" : "v";
        RankLadderContainer.IsVisible = expanded;
        RankLadderContainer.Opacity = expanded ? 1 : 0;
        RankLadderContainer.TranslationY = expanded ? 0 : -6;
    }

    void ApplyAccessibility()
    {
        var options = AccessibilityService.GetOptions();
        AccessibilityService.ApplyTextScale(this);

        BackgroundColor = options.HighContrastEnabled
            ? Color.FromArgb("#FFFFFF")
            : Color.FromArgb("#F2F2F2");

        var headingColor = options.HighContrastEnabled
            ? Color.FromArgb("#1B2430")
            : Color.FromArgb("#444444");
        var secondaryColor = options.HighContrastEnabled
            ? Color.FromArgb("#374553")
            : Color.FromArgb("#777777");
        var hintColor = options.HighContrastEnabled
            ? Color.FromArgb("#42515F")
            : Color.FromArgb("#9AA0A6");
        var accentColor = options.ColorSafeChartsEnabled
            ? Color.FromArgb("#0072B2")
            : Color.FromArgb("#F5A623");

        PeakScoreLabel.TextColor = headingColor;
        MemoryValue.TextColor = secondaryColor;
        ProblemValue.TextColor = secondaryColor;
        LanguageValue.TextColor = secondaryColor;
        AgilityValue.TextColor = secondaryColor;
        FocusValue.TextColor = secondaryColor;
        PeakRankHintLabel.TextColor = hintColor;
        PeakRankLabel.TextColor = accentColor;
        RankProgressLabel.TextColor = options.HighContrastEnabled
            ? Color.FromArgb("#3F4E5D")
            : Color.FromArgb("#7A8795");

        CompareYouLabel.TextColor = options.ColorSafeChartsEnabled
            ? Color.FromArgb("#56B4E9")
            : Color.FromArgb("#53C5F0");
        CompareTargetLabel.TextColor = options.HighContrastEnabled
            ? Color.FromArgb("#5A5D66")
            : Color.FromArgb("#8B8D95");

        ApplySkillBarPalette(options.ColorSafeChartsEnabled);
        RenderRankLadder(BrainScoreService.GetCurrentScores().PeakScore);
    }

    void ApplySkillBarPalette(bool colorSafe)
    {
        PeakFill.BackgroundColor = colorSafe ? Color.FromArgb("#56B4E9") : Color.FromArgb("#12B5E4");

        var palette = colorSafe ? ColorSafeSkillBarColors : DefaultSkillBarColors;
        MemoryFill.BackgroundColor = palette[0];
        ProblemFill.BackgroundColor = palette[1];
        LanguageFill.BackgroundColor = palette[2];
        AgilityFill.BackgroundColor = palette[3];
        FocusFill.BackgroundColor = palette[4];

        var benchmarkColor = colorSafe
            ? Color.FromArgb("#7E7E86")
            : Color.FromArgb("#8F8F96");

        MemoryBenchmarkFill.BackgroundColor = benchmarkColor;
        ProblemBenchmarkFill.BackgroundColor = benchmarkColor;
        LanguageBenchmarkFill.BackgroundColor = benchmarkColor;
        AgilityBenchmarkFill.BackgroundColor = benchmarkColor;
        FocusBenchmarkFill.BackgroundColor = benchmarkColor;
    }

    void SelectTab(int index)
    {
        TabBrain.TextColor = index == 0 ? Colors.White : Color.FromArgb("#CCFFFFFF");
        TabOverTime.TextColor = index == 1 ? Colors.White : Color.FromArgb("#CCFFFFFF");
        TabGames.TextColor = index == 2 ? Colors.White : Color.FromArgb("#CCFFFFFF");
        TabLeaderboards.TextColor = index == 3 ? Colors.White : Color.FromArgb("#CCFFFFFF");

        Grid.SetColumn(TabUnderline, index);
    }

    void OnTabBrainTapped(object sender, TappedEventArgs e) => SelectTab(0);

    async void OnCompareClicked(object sender, EventArgs e)
    {
        await PageTransitionService.GoToAsync(nameof(ComparePage));
    }

    async void OnShareClicked(object sender, EventArgs e)
    {
        try
        {
            var scores = BrainScoreService.GetCurrentScores();
            var rank = BrainScoreService.GetPeakRankInfo(scores.PeakScore);
            var playerName = LocalAccountStore.TryGetProfile(out var profile) && !string.IsNullOrWhiteSpace(profile.Username)
                ? profile.Username.Trim()
                : "Apex Player";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Share your Apex Brain summary",
                Subject = "Apex Brain summary",
                Text = BuildStatsShareText(playerName, rank.Name, scores)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Share unavailable", $"We couldn't open the share menu right now.\n\n{ex.Message}", "OK");
        }
    }

    static string BuildStatsShareText(string playerName, string rankName, BrainSkillScores scores)
    {
        static string BuildBar(string label, int value)
        {
            var filled = (int)Math.Round(Math.Clamp(value / 300d, 0d, 1d) * 12d);
            var empty = Math.Max(0, 12 - filled);
            return $"{label,-16} {new string('█', filled)}{new string('░', empty)} {value}";
        }

        return $$"""
                 APEX BRAIN SUMMARY

                 {{playerName}}
                 Rank: {{rankName}}
                 Apex Brain Score: {{scores.PeakScore}}/1000

                 {{BuildBar("Memory", scores.Memory)}}
                 {{BuildBar("Problem Solving", scores.ProblemSolving)}}
                 {{BuildBar("Language", scores.Language)}}
                 {{BuildBar("Mental Agility", scores.MentalAgility)}}
                 {{BuildBar("Focus", scores.Focus)}}
                 {{BuildBar("Emotion", scores.Emotion)}}
                 """;
    }

    async void OnTabOverTimeTapped(object sender, TappedEventArgs e)
    {
        SelectTab(1);
        await PageTransitionService.GoToAsync(nameof(OverTimePage));
    }

    async void OnTabGamesTapped(object sender, TappedEventArgs e)
    {
        SelectTab(2);
        await PageTransitionService.GoToAsync(nameof(GamesStatsPage));
    }

    async void OnTabLeaderboardsTapped(object sender, TappedEventArgs e)
    {
        SelectTab(3);
        await PageTransitionService.GoToAsync(nameof(LeaderboardsPage));
    }
}

