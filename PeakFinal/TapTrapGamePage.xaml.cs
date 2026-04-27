using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class TapTrapGamePage : ContentPage
{
    enum ShapeKind { Star, Triangle, Circle, Diamond, Square }
    enum TargetRuleKind { ShapeOnly, ColorOnly, ShapeAndColor }

    sealed record Difficulty(
        int Stage,
        int VisibleCards,
        int MinMatches,
        int MaxMatches,
        bool AllowColorOnly,
        bool AllowCombined,
        int PaletteSize,
        int BasePoints);

    sealed record CardTone(string Name, string FillHex, string AccentHex, string DarkHex);

    sealed record TargetRule(
        TargetRuleKind Kind,
        ShapeKind? Shape,
        CardTone? Tone,
        string Prompt,
        string Caption);

    sealed class CardState
    {
        public required int Index { get; init; }
        public required Border View { get; init; }
        public required Label GlyphLabel { get; init; }
        public ShapeKind Shape { get; set; }
        public CardTone? Tone { get; set; }
        public bool IsMatch { get; set; }
        public bool IsResolved { get; set; }
    }

    static readonly Difficulty[] Difficulties =
    {
        new(0, 4, 1, 1, false, false, 2, 90),
        new(1, 6, 1, 2, true, false, 3, 105),
        new(2, 6, 1, 2, true, true, 4, 125),
        new(3, 8, 2, 2, true, true, 5, 145)
    };

    static readonly CardTone[] Palette =
    {
        new("Pink", "#FF5D87", "#FF7AA0", "#71233E"),
        new("Coral", "#FF7B62", "#FF9A84", "#7A3327"),
        new("Sun", "#F5B72A", "#FFD35A", "#77520A"),
        new("Purple", "#B75CFF", "#D18FFF", "#58267A"),
        new("Teal", "#29C6C2", "#71ECE8", "#0F5A58")
    };

    const int StartingTimeSeconds = 45;
    const int BoardColumns = 2;
    const int BoardRows = 4;
    const int ComboWindowMs = 280;

    readonly Random _random = new();
    readonly List<CardState> _cards = new();

    IDispatcherTimer? _timer;
    TargetRule? _currentTarget;
    DateTime _lastCorrectTapUtc = DateTime.MinValue;

    int _timeLeft;
    int _score;
    int _bestScore;
    int _correctTaps;
    int _wrongTaps;
    int _currentStreak;
    int _longestStreak;
    int _comboPairs;
    int _matchCount;
    int _remainingMatches;
    int _paletteSize = 2;

    bool _started;
    bool _isPaused;
    bool _isGameOver;
    bool _isTransitioning;
    bool _inputLocked;

    public TapTrapGamePage()
    {
        InitializeComponent();
        BuildBoard();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("tap_trap");
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
        await GenerateBoardAsync(animated: false);
    }

    void ResetState()
    {
        StopTimer();
        _timeLeft = StartingTimeSeconds;
        _score = 0;
        _correctTaps = 0;
        _wrongTaps = 0;
        _currentStreak = 0;
        _longestStreak = 0;
        _comboPairs = 0;
        _matchCount = 0;
        _remainingMatches = 0;
        _lastCorrectTapUtc = DateTime.MinValue;
        _bestScore = BrainScoreService.GetGamePerformance("tap_trap")?.BestScore ?? 0;
        _isPaused = false;
        _isGameOver = false;
        _isTransitioning = false;
        _inputLocked = true;
        _paletteSize = 2;

        CountdownOverlay.IsVisible = true;
        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        PaletteUnlockBorder.Opacity = 0;
        ScoreDeltaLabel.Opacity = 0;

        UpdateHud();
        UpdateHint();
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

    async Task GenerateBoardAsync(bool animated)
    {
        if (_isGameOver)
        {
            return;
        }

        _isTransitioning = true;
        _inputLocked = true;

        if (animated)
        {
            await Task.WhenAll(
                BoardShell.FadeTo(0.85, 90, Easing.CubicIn),
                BoardShell.ScaleTo(0.985, 90, Easing.CubicIn));
        }

        Difficulty difficulty = GetDifficulty();
        CardTone[] activePalette = Palette.Take(GetPaletteSize(difficulty)).ToArray();
        ShapeKind[] activeShapes = GetActiveShapes(difficulty);

        _currentTarget = CreateTargetRule(difficulty, activePalette, activeShapes);
        _matchCount = Math.Min(
            _random.Next(difficulty.MinMatches, difficulty.MaxMatches + 1),
            Math.Max(1, difficulty.VisibleCards - 1));
        _remainingMatches = _matchCount;
        _lastCorrectTapUtc = DateTime.MinValue;

        var nextCards = BuildCards(difficulty, _currentTarget, activePalette, activeShapes)
            .OrderBy(_ => _random.Next())
            .ToList();

        for (int index = 0; index < _cards.Count; index++)
        {
            CardState slot = _cards[index];
            if (index < nextCards.Count)
            {
                var nextCard = nextCards[index];
                ApplyCard(slot, nextCard.Shape, nextCard.Tone, nextCard.IsMatch, true);
            }
            else
            {
                slot.View.IsVisible = false;
                slot.View.InputTransparent = true;
                slot.IsResolved = false;
            }
        }

        ApplyObjective(_currentTarget, difficulty);
        ApplyPaletteTheme(activePalette.Last());
        UpdateHint();

        if (animated)
        {
            BoardShell.Scale = 0.985;
            BoardShell.Opacity = 0.85;
            await Task.WhenAll(
                BoardShell.FadeTo(1, 120, Easing.CubicOut),
                BoardShell.ScaleTo(1, 120, Easing.CubicOut));
        }

        _inputLocked = false;
        _isTransitioning = false;
    }

    IEnumerable<(ShapeKind Shape, CardTone Tone, bool IsMatch)> BuildCards(
        Difficulty difficulty,
        TargetRule target,
        CardTone[] activePalette,
        ShapeKind[] activeShapes)
    {
        var cards = new List<(ShapeKind Shape, CardTone Tone, bool IsMatch)>();

        for (int index = 0; index < _matchCount; index++)
        {
            cards.Add(CreateMatchingCard(target, activePalette, activeShapes));
        }

        while (cards.Count < difficulty.VisibleCards)
        {
            cards.Add(CreateDecoyCard(target, activePalette, activeShapes));
        }

        return cards;
    }

    (ShapeKind Shape, CardTone Tone, bool IsMatch) CreateMatchingCard(TargetRule target, CardTone[] activePalette, ShapeKind[] activeShapes)
    {
        ShapeKind shape = target.Shape ?? activeShapes[_random.Next(activeShapes.Length)];
        CardTone tone = target.Tone ?? activePalette[_random.Next(activePalette.Length)];
        return (shape, tone, true);
    }

    (ShapeKind Shape, CardTone Tone, bool IsMatch) CreateDecoyCard(TargetRule target, CardTone[] activePalette, ShapeKind[] activeShapes)
    {
        for (int attempt = 0; attempt < 160; attempt++)
        {
            ShapeKind shape = activeShapes[_random.Next(activeShapes.Length)];
            CardTone tone = activePalette[_random.Next(activePalette.Length)];
            if (!MatchesTarget(shape, tone, target))
            {
                return (shape, tone, false);
            }
        }

        ShapeKind fallbackShape = activeShapes[0];
        CardTone fallbackTone = activePalette[^1];
        return (fallbackShape, fallbackTone, false);
    }

    TargetRule CreateTargetRule(Difficulty difficulty, CardTone[] activePalette, ShapeKind[] activeShapes)
    {
        TargetRuleKind kind = difficulty.Stage switch
        {
            0 => TargetRuleKind.ShapeOnly,
            1 => _random.NextDouble() < 0.35 ? TargetRuleKind.ColorOnly : TargetRuleKind.ShapeOnly,
            2 => _random.NextDouble() < 0.35 ? TargetRuleKind.ShapeAndColor : (_random.NextDouble() < 0.5 ? TargetRuleKind.ShapeOnly : TargetRuleKind.ColorOnly),
            _ => _random.NextDouble() < 0.6 ? TargetRuleKind.ShapeAndColor : (_random.NextDouble() < 0.5 ? TargetRuleKind.ColorOnly : TargetRuleKind.ShapeOnly)
        };

        ShapeKind shape = activeShapes[_random.Next(activeShapes.Length)];
        CardTone tone = activePalette[_random.Next(activePalette.Length)];

        return kind switch
        {
            TargetRuleKind.ShapeOnly => new TargetRule(kind, shape, null, $"TAP ALL {GetShapePlural(shape)}", "SHAPE TARGET"),
            TargetRuleKind.ColorOnly => new TargetRule(kind, null, tone, $"TAP ALL {tone.Name.ToUpperInvariant()} CARDS", "COLOR TARGET"),
            _ => new TargetRule(kind, shape, tone, $"TAP ALL {tone.Name.ToUpperInvariant()} {GetShapePlural(shape)}", "COLOR + SHAPE")
        };
    }

    async Task OnCardTappedAsync(CardState card)
    {
        if (_inputLocked || _isPaused || _isGameOver || _isTransitioning || card.IsResolved || !card.View.IsVisible)
        {
            return;
        }

        if (card.IsMatch)
        {
            card.IsResolved = true;
            card.View.InputTransparent = true;
            _remainingMatches--;
            _correctTaps++;
            _currentStreak++;
            _longestStreak = Math.Max(_longestStreak, _currentStreak);

            DateTime now = DateTime.UtcNow;
            bool combo = _matchCount >= 2 && _remainingMatches < _matchCount - 1 && (now - _lastCorrectTapUtc).TotalMilliseconds <= ComboWindowMs;
            _lastCorrectTapUtc = now;
            if (combo)
            {
                _comboPairs++;
            }

            int gained = CalculatePoints(GetDifficulty(), combo);
            _score += gained;
            UpdateHud();
            ApplyResolvedCard(card, combo);
            await Task.WhenAll(
                card.View.ScaleTo(0.94, 85, Easing.CubicOut),
                FlashScoreDeltaAsync(combo ? $"x2 +{gained}" : $"+{gained}", combo ? Color.FromArgb("#FFD45A") : Colors.White));
            await card.View.ScaleTo(1, 85, Easing.CubicIn);

            if (_timeLeft <= 0)
            {
                await EndGameAsync();
                return;
            }

            if (_remainingMatches == 0)
            {
                await Task.Delay(120);
                await GenerateBoardAsync(animated: true);
            }

            return;
        }

        _wrongTaps++;
        _currentStreak = 0;
        _lastCorrectTapUtc = DateTime.MinValue;
        UpdateHud();
        await FlashWrongTapAsync(card);
        await FlashScoreDeltaAsync("MISS", Color.FromArgb("#FFD6E0"));
    }

    int CalculatePoints(Difficulty difficulty, bool combo)
    {
        int streakBonus = Math.Min(60, Math.Max(0, _currentStreak - 1) * 6);
        int comboBonus = combo ? difficulty.BasePoints : 0;
        int complexityBonus = _currentTarget?.Kind == TargetRuleKind.ShapeAndColor ? 20 : _currentTarget?.Kind == TargetRuleKind.ColorOnly ? 10 : 0;
        return difficulty.BasePoints + streakBonus + comboBonus + complexityBonus;
    }

    async Task FlashWrongTapAsync(CardState card)
    {
        Color original = card.View.BackgroundColor ?? Color.FromArgb("#FF5D87");
        card.View.BackgroundColor = Color.FromArgb("#D62E5B");
        await card.View.ScaleTo(0.95, 70, Easing.CubicOut);
        await card.View.ScaleTo(1, 90, Easing.CubicIn);
        card.View.BackgroundColor = original;
    }

    async Task FlashScoreDeltaAsync(string text, Color textColor)
    {
        ScoreDeltaLabel.Text = text;
        ScoreDeltaLabel.TextColor = textColor;
        ScoreDeltaLabel.Opacity = 1;
        ScoreDeltaLabel.TranslationY = 0;
        await Task.WhenAll(
            ScoreDeltaLabel.FadeTo(0, 420, Easing.CubicOut),
            ScoreDeltaLabel.TranslateTo(0, -14, 420, Easing.CubicOut));
        ScoreDeltaLabel.TranslationY = 0;
    }

    async Task ShowPaletteUnlockAsync(CardTone tone)
    {
        PaletteUnlockLabel.Text = $"{tone.Name.ToUpperInvariant()} CARDS UNLOCKED";
        PaletteUnlockBorder.Opacity = 0;
        PaletteUnlockBorder.Scale = 0.92;
        await Task.WhenAll(
            PaletteUnlockBorder.FadeTo(1, 160, Easing.CubicOut),
            PaletteUnlockBorder.ScaleTo(1, 160, Easing.CubicOut));
        await Task.Delay(520);
        await PaletteUnlockBorder.FadeTo(0, 200, Easing.CubicIn);
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

        bool isNewBest = _score > _bestScore;
        int bestScore = Math.Max(_bestScore, _score);
        int apexPoints = BrainScoreService.RecordGameScore("tap_trap", BrainSkill.Focus, _score, TapTrapProgress.ExpectedTopScore);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Tap Trap",
                score: _score,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new TapTrapGamePage(),
                accentHex: "#7A60FF",
                secondaryLabel: "Rank",
                secondaryValue: TapTrapProgress.ResolveRank(bestScore)));
    }

    void ApplyObjective(TargetRule target, Difficulty difficulty)
    {
        ObjectiveCaptionLabel.Text = target.Caption;
        TargetPromptLabel.Text = target.Prompt;
        StageLabel.Text = $"STAGE {difficulty.Stage + 1}";

        CardTone accentTone = target.Tone ?? Palette[Math.Min(_paletteSize - 1, Palette.Length - 1)];
        TargetPreviewBadge.BackgroundColor = Color.FromArgb(accentTone.FillHex);
        TargetPreviewBadge.Stroke = Color.FromArgb(accentTone.AccentHex);
        TargetPreviewBadge.StrokeThickness = 1.2;
        TargetPreviewIconLabel.Text = target.Kind == TargetRuleKind.ColorOnly
            ? "\u25CF"
            : GetShapeGlyph(target.Shape ?? ShapeKind.Circle);
    }

    void ApplyPaletteTheme(CardTone accentTone)
    {
        int previousPaletteSize = _paletteSize;
        _paletteSize = GetPaletteSize(GetDifficulty());

        HeaderBar.BackgroundColor = Color.FromArgb(accentTone.DarkHex);
        ObjectiveBorder.BackgroundColor = Color.FromArgb(accentTone.DarkHex);
        ObjectiveBorder.Stroke = Color.FromArgb(accentTone.AccentHex);
        StageBadge.BackgroundColor = Color.FromArgb(accentTone.FillHex);
        BoardShell.BackgroundColor = Color.FromArgb(accentTone.DarkHex);
        BoardShell.Stroke = Color.FromArgb(accentTone.AccentHex);
        HintBorder.BackgroundColor = Color.FromArgb(accentTone.DarkHex);
        StreakBadge.BackgroundColor = Color.FromArgb(accentTone.FillHex);

        if (_paletteSize > previousPaletteSize)
        {
            _ = ShowPaletteUnlockAsync(Palette[_paletteSize - 1]);
        }
    }

    void UpdateHud()
    {
        TimerLabel.Text = $"00:{_timeLeft:00}";
        ScoreLabel.Text = _score.ToString();
        StreakLabel.Text = _currentStreak > 0 ? $"Streak {_currentStreak}" : "Streak 0";
    }

    void UpdateHint()
    {
        HintLabel.Text = _remainingMatches > 1
            ? "Tap both matching cards quickly for the x2 combo."
            : "Tap the card that matches the target before the next target appears.";
    }

    void BuildBoard()
    {
        CardBoard.RowDefinitions.Clear();
        CardBoard.ColumnDefinitions.Clear();
        CardBoard.Children.Clear();
        _cards.Clear();

        for (int row = 0; row < BoardRows; row++)
        {
            CardBoard.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }

        for (int column = 0; column < BoardColumns; column++)
        {
            CardBoard.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int index = 0; index < BoardRows * BoardColumns; index++)
        {
            int row = index / BoardColumns;
            int column = index % BoardColumns;

            var glyph = new Label
            {
                FontAttributes = FontAttributes.Bold,
                FontSize = 40,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White,
                VerticalTextAlignment = TextAlignment.Center
            };

            var border = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                StrokeThickness = 1.2,
                Stroke = Color.FromArgb("#FFFFFF"),
                Padding = 0,
                Content = glyph,
                IsVisible = false
            };

            CardState card = new()
            {
                Index = index,
                View = border,
                GlyphLabel = glyph
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OnCardTappedAsync(card);
            border.GestureRecognizers.Add(tap);

            _cards.Add(card);
            CardBoard.Children.Add(border);
            Grid.SetRow(border, row);
            Grid.SetColumn(border, column);
        }
    }

    void ApplyCard(CardState slot, ShapeKind shape, CardTone tone, bool isMatch, bool isVisible)
    {
        slot.Shape = shape;
        slot.Tone = tone;
        slot.IsMatch = isMatch;
        slot.IsResolved = false;
        slot.View.IsVisible = isVisible;
        slot.View.InputTransparent = !isVisible;
        slot.View.Scale = 1;
        slot.View.Opacity = 1;
        slot.View.BackgroundColor = Color.FromArgb(tone.FillHex);
        slot.View.Stroke = Color.FromArgb("#45FFFFFF");
        slot.View.StrokeThickness = 1.2;
        slot.GlyphLabel.Text = GetShapeGlyph(shape);
    }

    void ApplyResolvedCard(CardState slot, bool combo)
    {
        slot.View.BackgroundColor = combo
            ? Color.FromArgb("#F5B72A")
            : Color.FromArgb("#4AD26B");
        slot.View.Stroke = combo
            ? Color.FromArgb("#FFE8A2")
            : Color.FromArgb("#D6FFE0");
    }

    Difficulty GetDifficulty()
    {
        int elapsed = StartingTimeSeconds - _timeLeft;
        if (elapsed >= 34 || _score >= 3000)
        {
            return Difficulties[3];
        }

        if (elapsed >= 22 || _score >= 1700)
        {
            return Difficulties[2];
        }

        if (elapsed >= 10 || _score >= 700)
        {
            return Difficulties[1];
        }

        return Difficulties[0];
    }

    int GetPaletteSize(Difficulty difficulty)
    {
        int unlockedByScore = _score switch
        {
            >= 3200 => 5,
            >= 1800 => 4,
            >= 800 => 3,
            _ => 2
        };

        return Math.Clamp(Math.Max(difficulty.PaletteSize, unlockedByScore), 2, Palette.Length);
    }

    static ShapeKind[] GetActiveShapes(Difficulty difficulty)
    {
        return difficulty.Stage switch
        {
            0 => new[] { ShapeKind.Star, ShapeKind.Triangle, ShapeKind.Circle },
            1 => new[] { ShapeKind.Star, ShapeKind.Triangle, ShapeKind.Circle, ShapeKind.Diamond },
            _ => new[] { ShapeKind.Star, ShapeKind.Triangle, ShapeKind.Circle, ShapeKind.Diamond, ShapeKind.Square }
        };
    }

    static bool MatchesTarget(ShapeKind shape, CardTone tone, TargetRule target)
    {
        return target.Kind switch
        {
            TargetRuleKind.ShapeOnly => target.Shape == shape,
            TargetRuleKind.ColorOnly => string.Equals(target.Tone?.Name, tone.Name, StringComparison.Ordinal),
            _ => target.Shape == shape && string.Equals(target.Tone?.Name, tone.Name, StringComparison.Ordinal)
        };
    }

    static string GetShapePlural(ShapeKind shape) => shape switch
    {
        ShapeKind.Star => "STARS",
        ShapeKind.Triangle => "TRIANGLES",
        ShapeKind.Circle => "CIRCLES",
        ShapeKind.Diamond => "DIAMONDS",
        _ => "SQUARES"
    };

    static string GetShapeGlyph(ShapeKind shape) => shape switch
    {
        ShapeKind.Star => "\u2606",
        ShapeKind.Triangle => "\u25B3",
        ShapeKind.Circle => "\u25CB",
        ShapeKind.Diamond => "\u25C7",
        _ => "\u25A1"
    };

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isGameOver)
        {
            return;
        }

        _isPaused = true;

        var action = await GamePauseService.ShowAsync(
            this,
            "Tap Trap",
            "Tap the safe targets fast, avoid the traps, and build combos before time runs out.");

        if (action == GamePauseAction.Restart)
        {
            await GamePauseService.RestartCurrentPageAsync(this);
            return;
        }

        if (action == GamePauseAction.Exit)
        {
            await PageTransitionService.PopAsync(Navigation);
            return;
        }

        _isPaused = false;
    }

    void OnResumeClicked(object sender, EventArgs e)
    {
        PauseOverlay.IsVisible = false;
        _isPaused = false;
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(
            Navigation,
            new TapTrapInsightsPage(_score, _correctTaps, _wrongTaps, _comboPairs, _longestStreak));
    }
}
