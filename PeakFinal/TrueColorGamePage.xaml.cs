namespace Peak;

public partial class TrueColorGamePage : ContentPage
{
    sealed record ColorCue(string Name, string InkHex, string SoftHex);
    sealed record Difficulty(
        int Stage,
        int PaletteSize,
        int BasePoints,
        int TransitionMs,
        string Hint);

    static readonly Difficulty[] Difficulties =
    {
        new(0, 3, 90, 190, "Start by checking whether the top word matches the bottom ink color."),
        new(1, 4, 108, 175, "More colors unlock as the score rises, so the false matches get trickier."),
        new(2, 5, 126, 160, "Later tiers use tighter color sets that are harder to reject on impulse."),
        new(3, 6, 146, 145, "At higher scores the cards refresh faster, so trust the ink, not the word."),
        new(4, 6, 164, 130, "Late rounds keep the same rule but push your response control much harder.")
    };

    static readonly ColorCue[] Palette =
    {
        new("Blue", "#2C91FF", "#DFF0FF"),
        new("Red", "#FF525B", "#FFE1E3"),
        new("Green", "#24BF6F", "#DAF7E8"),
        new("Yellow", "#F0C332", "#FFF1C5"),
        new("Purple", "#8D61FF", "#ECE4FF"),
        new("Orange", "#FF8A3D", "#FFE8D7")
    };

    const int StartingTimeSeconds = 45;
    const int ScoreTierStep = 320;

    readonly Random _random = new();

    IDispatcherTimer? _timer;
    DateTime _promptShownUtc = DateTime.UtcNow;

    ColorCue? _targetCue;
    ColorCue? _actualInkCue;
    ColorCue? _bottomWordCue;
    bool _isMatch;
    int _timeLeft;
    int _score;
    int _bestScore;
    int _correctAnswers;
    int _wrongAnswers;
    int _currentStreak;
    int _longestStreak;
    int _fastestReactionMs = int.MaxValue;
    bool _started;
    bool _isPaused;
    bool _isGameOver;
    bool _isTransitioning;
    bool _inputLocked;

    public TrueColorGamePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("true_color");
        if (_started)
        {
            return;
        }

        _started = true;
        _ = StartGameAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
        _ = GameAudioService.StopGameAtmosphereAsync();
    }

    protected override bool OnBackButtonPressed() => true;

    async Task StartGameAsync()
    {
        ResetState();
        await RunCountdownAsync();
        StartTimer();
        await ShowPromptAsync(animated: false);
    }

    void ResetState()
    {
        StopTimer();
        _timeLeft = StartingTimeSeconds;
        _score = 0;
        _correctAnswers = 0;
        _wrongAnswers = 0;
        _currentStreak = 0;
        _longestStreak = 0;
        _fastestReactionMs = int.MaxValue;
        _bestScore = BrainScoreService.GetGamePerformance("true_color")?.BestScore ?? 0;
        _isPaused = false;
        _isGameOver = false;
        _isTransitioning = false;
        _inputLocked = true;

        CountdownOverlay.IsVisible = true;
        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        ScoreDeltaLabel.Opacity = 0;

        UpdateHud();
        UpdateDifficultyHints();
    }

    async Task RunCountdownAsync()
    {
        LoadingSpinner.IsRunning = true;
        CountdownValueLabel.Text = string.Empty;
        await Task.Delay(650);
        LoadingSpinner.IsRunning = false;

        for (int count = 3; count >= 1; count--)
        {
            CountdownCaptionLabel.Text = "GET READY";
            CountdownValueLabel.Text = count.ToString();
            CountdownValueLabel.Scale = 0.85;
            await CountdownValueLabel.ScaleTo(1, 170, Easing.CubicOut);
            await Task.Delay(240);
        }

        CountdownOverlay.IsVisible = false;
    }

    void StartTimer()
    {
        StopTimer();
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer = null;
    }

    void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _isTransitioning)
        {
            return;
        }

        _timeLeft = Math.Max(0, _timeLeft - 1);
        UpdateHud();

        if (_timeLeft == 0)
        {
            _ = EndGameAsync();
        }
    }

    Difficulty GetDifficulty()
    {
        int tier = Math.Min(Difficulties.Length - 1, Math.Max(0, _score / ScoreTierStep));
        return Difficulties[tier];
    }

    async Task ShowPromptAsync(bool animated)
    {
        if (_isGameOver)
        {
            return;
        }

        _isTransitioning = true;
        _inputLocked = true;

        Difficulty difficulty = GetDifficulty();

        if (animated)
        {
            uint duration = (uint)difficulty.TransitionMs;
            await Task.WhenAll(
                TopCard.TranslateTo(0, -16, duration, Easing.CubicIn),
                TopCard.FadeTo(0.2, duration, Easing.CubicIn),
                BottomCard.TranslateTo(0, 16, duration, Easing.CubicIn),
                BottomCard.FadeTo(0.2, duration, Easing.CubicIn));
        }

        var activePalette = Palette.Take(Math.Min(difficulty.PaletteSize, Palette.Length)).ToArray();
        _targetCue = activePalette[_random.Next(activePalette.Length)];
        _isMatch = _random.NextDouble() >= 0.45;
        _actualInkCue = _isMatch
            ? _targetCue
            : activePalette.First(cue => cue != _targetCue && cue.Name != _targetCue.Name);
        _bottomWordCue = SelectBottomWord(activePalette, _actualInkCue, _targetCue);

        ApplyPrompt(difficulty);
        _promptShownUtc = DateTime.UtcNow;

        if (animated)
        {
            TopCard.TranslationY = -16;
            BottomCard.TranslationY = 16;
            uint duration = (uint)(difficulty.TransitionMs + 30);
            await Task.WhenAll(
                TopCard.TranslateTo(0, 0, duration, Easing.CubicOut),
                TopCard.FadeTo(1, duration, Easing.CubicOut),
                BottomCard.TranslateTo(0, 0, duration, Easing.CubicOut),
                BottomCard.FadeTo(1, duration, Easing.CubicOut));
        }

        _isTransitioning = false;
        _inputLocked = false;
    }

    ColorCue SelectBottomWord(IReadOnlyList<ColorCue> palette, ColorCue actualInkCue, ColorCue targetCue)
    {
        var candidates = palette
            .Where(cue => cue != actualInkCue)
            .ToList();

        if (candidates.Count == 0)
        {
            return actualInkCue;
        }

        if (candidates.Count > 1)
        {
            var withoutTarget = candidates.Where(cue => cue != targetCue).ToList();
            if (withoutTarget.Count > 0 && _random.NextDouble() < 0.8)
            {
                candidates = withoutTarget;
            }
        }

        return candidates[_random.Next(candidates.Count)];
    }

    void ApplyPrompt(Difficulty difficulty)
    {
        if (_targetCue is null || _actualInkCue is null || _bottomWordCue is null)
        {
            return;
        }

        StageLabel.Text = $"LEVEL {difficulty.Stage + 1}";
        ObjectiveCaptionLabel.Text = "MATCH THE WORD TO THE BOTTOM INK";
        TargetPromptLabel.Text = $"Does {_targetCue.Name.ToUpperInvariant()} match the bottom ink?";
        InstructionLabel.Text = "Ignore the bottom word meaning.";
        TopWordLabel.Text = _targetCue.Name.ToUpperInvariant();
        BottomWordLabel.Text = _bottomWordCue.Name.ToUpperInvariant();
        BottomWordLabel.TextColor = Color.FromArgb(_actualInkCue.InkHex);

        TopCard.BackgroundColor = Color.FromArgb("#72B0FF");
        BottomCard.BackgroundColor = Color.FromArgb("#FFFFFF");
        BoardShell.BackgroundColor = Color.FromArgb("#0E61DB");

        UpdateDifficultyHints();
    }

    async Task HandleAnswerAsync(bool answerTrue)
    {
        if (_inputLocked || _isPaused || _isGameOver || _isTransitioning)
        {
            return;
        }

        _inputLocked = true;

        if (answerTrue == _isMatch)
        {
            await HandleCorrectAnswerAsync();
        }
        else
        {
            await HandleWrongAnswerAsync();
        }
    }

    async Task HandleCorrectAnswerAsync()
    {
        _correctAnswers++;
        _currentStreak++;
        _longestStreak = Math.Max(_longestStreak, _currentStreak);

        int reactionMs = Math.Max(180, (int)(DateTime.UtcNow - _promptShownUtc).TotalMilliseconds);
        _fastestReactionMs = Math.Min(_fastestReactionMs, reactionMs);

        Difficulty difficulty = GetDifficulty();
        int speedBonus = (int)Math.Round(Math.Clamp((2200 - reactionMs) / 28.0, 0, 82));
        int streakBonus = Math.Min(72, (_currentStreak - 1) * 8);
        int gain = difficulty.BasePoints + speedBonus + streakBonus;
        _score += gain;

        UpdateHud();
        UpdateDifficultyHints();
        _ = AnimateScoreDeltaAsync($"+{gain}", true);

        await Task.WhenAll(
            TopCard.ScaleTo(1.04, 90, Easing.CubicOut),
            BottomCard.ScaleTo(1.04, 90, Easing.CubicOut),
            TopCard.ScaleTo(1, 110, Easing.CubicIn),
            BottomCard.ScaleTo(1, 110, Easing.CubicIn));

        await ShowPromptAsync(animated: true);
    }

    async Task HandleWrongAnswerAsync()
    {
        _wrongAnswers++;
        _currentStreak = 0;
        _score = Math.Max(0, _score - 35);

        UpdateHud();
        UpdateDifficultyHints();
        _ = AnimateScoreDeltaAsync("-35", false);

        await Task.WhenAll(
            BottomCard.TranslateTo(-10, 0, 45, Easing.CubicIn),
            TopCard.TranslateTo(10, 0, 45, Easing.CubicIn));
        await Task.WhenAll(
            BottomCard.TranslateTo(10, 0, 65, Easing.CubicOut),
            TopCard.TranslateTo(-10, 0, 65, Easing.CubicOut));
        await Task.WhenAll(
            BottomCard.TranslateTo(0, 0, 45, Easing.CubicIn),
            TopCard.TranslateTo(0, 0, 45, Easing.CubicIn));

        _inputLocked = false;
    }

    async Task AnimateScoreDeltaAsync(string text, bool positive)
    {
        ScoreDeltaLabel.Text = text;
        ScoreDeltaLabel.TextColor = positive ? Colors.White : Color.FromArgb("#D8ECFF");
        ScoreDeltaLabel.Opacity = 0;
        ScoreDeltaLabel.TranslationY = 0;

        await Task.WhenAll(
            ScoreDeltaLabel.FadeTo(1, 100, Easing.CubicOut),
            ScoreDeltaLabel.TranslateTo(0, -12, 220, Easing.CubicOut));

        await ScoreDeltaLabel.FadeTo(0, 180, Easing.CubicIn);
        ScoreDeltaLabel.TranslationY = 0;
    }

    void UpdateHud()
    {
        TimerLabel.Text = $"00:{_timeLeft:00}";
        ScoreLabel.Text = _score.ToString();
        StreakLabel.Text = $"Streak {_currentStreak}";
        StreakBadge.IsVisible = _currentStreak > 0;
    }

    void UpdateDifficultyHints()
    {
        Difficulty difficulty = GetDifficulty();
        HintLabel.Text = difficulty.Hint;
        DifficultyHintLabel.Text = $"Difficulty rises every {ScoreTierStep} points. Current palette: {difficulty.PaletteSize} colors.";
    }

    async Task EndGameAsync()
    {
        if (_isGameOver)
        {
            return;
        }

        _isGameOver = true;
        _inputLocked = true;
        StopTimer();

        int bestAfterRun = Math.Max(_bestScore, _score);
        bool isNewBest = _score >= _bestScore && _score > 0;

        int apexPoints = BrainScoreService.RecordGameScore("true_color", BrainSkill.Focus, _score, TrueColorProgress.ExpectedTopScore);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "True Color",
                score: _score,
                bestScore: bestAfterRun,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new TrueColorGamePage(),
                accentHex: "#FF6A73",
                secondaryLabel: "Rank",
                secondaryValue: TrueColorProgress.ResolveRank(bestAfterRun)));
    }

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isGameOver || _isTransitioning)
        {
            return;
        }

        _isPaused = true;

        var action = await GamePauseService.ShowAsync(
            this,
            "True Color",
            "Read the word, judge the actual color, and answer as fast as your focus allows.");

        if (action == GamePauseAction.Restart)
        {
            StopTimer();
            await GamePauseService.RestartCurrentPageAsync(this);
            return;
        }

        if (action == GamePauseAction.Exit)
        {
            StopTimer();
            await PageTransitionService.PopAsync(Navigation);
            return;
        }

        _isPaused = false;
    }

    async void OnResumeClicked(object sender, EventArgs e)
    {
        await PauseOverlay.FadeTo(0, 120, Easing.CubicIn);
        PauseOverlay.IsVisible = false;
        _isPaused = false;
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        StopTimer();
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        int fastest = _fastestReactionMs == int.MaxValue ? 0 : _fastestReactionMs;
        await PageTransitionService.PushAsync(
            Navigation,
            new TrueColorInsightsPage(_score, _correctAnswers, _wrongAnswers, fastest, _longestStreak));
        Navigation.RemovePage(this);
    }

    async void OnTrueClicked(object sender, EventArgs e)
    {
        await HandleAnswerAsync(answerTrue: true);
    }

    async void OnFalseClicked(object sender, EventArgs e)
    {
        await HandleAnswerAsync(answerTrue: false);
    }
}
