namespace Peak;

public partial class MustSortGamePage : ContentPage
{
    enum MotifKind { Kite, Bridge, Arrow, Tower, Crest }
    enum CardMode { Color, Grey, Stop }

    sealed record Difficulty(
        int Stage,
        double TravelSeconds,
        int PaletteSize,
        double GreyChance,
        double StopChance,
        int TargetSwapEvery,
        int BasePoints);

    sealed record CardTone(string Name, string FillHex, string AccentHex, string DarkHex);
    sealed record TargetLane(int LaneIndex, MotifKind Motif, CardTone Tone);

    sealed class SortCard
    {
        public required MotifKind Motif { get; init; }
        public required CardTone Tone { get; init; }
        public required CardMode Mode { get; init; }
        public required int CorrectLane { get; init; }
        public required DateTime SpawnedUtc { get; init; }
        public bool IsResolved { get; set; }
    }

    static readonly Difficulty[] Difficulties =
    {
        new(0, 4.0, 2, 0.00, 0.00, 5, 115),
        new(1, 3.35, 2, 0.18, 0.00, 4, 130),
        new(2, 2.8, 3, 0.24, 0.14, 4, 150),
        new(3, 2.35, 4, 0.32, 0.22, 3, 175)
    };

    static readonly CardTone GreyTone = new("Grey", "#B8B7C7", "#D8D7E4", "#565566");

    static readonly CardTone[] Palette =
    {
        new("Pink", "#FF5D87", "#FF91AE", "#71233E"),
        new("Aqua", "#29C6C2", "#72ECE8", "#0F5A58"),
        new("Amber", "#F5B72A", "#FFD766", "#7A5410"),
        new("Lilac", "#B968FF", "#D9A8FF", "#5B2D78"),
        new("Sky", "#46A7FF", "#8FC9FF", "#214C73")
    };

    readonly Random _random = new();

    IDispatcherTimer? _clockTimer;
    IDispatcherTimer? _animationTimer;

    TargetLane? _leftLane;
    TargetLane? _rightLane;
    SortCard? _currentCard;
    SortCard? _nextCard;

    DateTime _cardStartedUtc;
    int _timeLeft;
    int _score;
    int _bestScore;
    int _currentStreak;
    int _longestStreak;
    int _greySorted;
    int _stopHeld;
    int _misses;
    int _resolvedCount;
    double _pausedProgressSeconds;

    bool _started;
    bool _isPaused;
    bool _isGameOver;
    bool _isTransitioning;
    bool _inputLocked;

    public MustSortGamePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("must_sort");
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
        StopTimers();
        _ = GameAudioService.StopGameAtmosphereAsync();
    }

    protected override bool OnBackButtonPressed() => true;

    async Task StartGameAsync()
    {
        ResetState();
        await RunCountdownAsync();
        StartTimers();
        SetupTargets(forceSwap: true);
        _nextCard = CreateCard();
        await SpawnNextCardAsync(animated: false);
    }

    void ResetState()
    {
        StopTimers();
        _timeLeft = 45;
        _score = 0;
        _currentStreak = 0;
        _longestStreak = 0;
        _greySorted = 0;
        _stopHeld = 0;
        _misses = 0;
        _resolvedCount = 0;
        _bestScore = BrainScoreService.GetGamePerformance("must_sort")?.BestScore ?? 0;
        _isPaused = false;
        _isGameOver = false;
        _isTransitioning = false;
        _inputLocked = true;
        _currentCard = null;
        _nextCard = null;
        _pausedProgressSeconds = 0;

        CountdownOverlay.IsVisible = true;
        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        ScoreDeltaLabel.Opacity = 0;
        ActiveCardContainer.Opacity = 0;
        ActiveCardContainer.TranslationX = 0;
        ActiveCardContainer.TranslationY = 0;

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

    void StartTimers()
    {
        StopTimers();

        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();

        _animationTimer = Dispatcher.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    void StopTimers()
    {
        if (_clockTimer is not null)
        {
            _clockTimer.Stop();
            _clockTimer.Tick -= OnClockTick;
            _clockTimer = null;
        }

        if (_animationTimer is not null)
        {
            _animationTimer.Stop();
            _animationTimer.Tick -= OnAnimationTick;
            _animationTimer = null;
        }
    }

    void OnClockTick(object? sender, EventArgs e)
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

    void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _isTransitioning || _currentCard is null)
        {
            return;
        }

        double progress = Math.Clamp((DateTime.UtcNow - _cardStartedUtc).TotalSeconds / GetDifficulty().TravelSeconds, 0, 1);
        double travel = GetTravelDistance();
        ActiveCardContainer.TranslationY = -(travel * progress);

        if (_currentCard.Mode == CardMode.Stop && progress >= 0.72 && !_currentCard.IsResolved)
        {
            _ = ResolveCardAsync(_currentCard.CorrectLane, true);
            return;
        }

        if (progress >= 1 && !_currentCard.IsResolved)
        {
            _ = HandleMissAsync();
        }
    }

    async Task SpawnNextCardAsync(bool animated)
    {
        if (_isGameOver)
        {
            return;
        }

        if (_leftLane is null || _rightLane is null)
        {
            SetupTargets(forceSwap: true);
        }

        if (_nextCard is null)
        {
            _nextCard = CreateCard();
        }

        _currentCard = _nextCard;
        _currentCard.IsResolved = false;
        _nextCard = CreateCard();
        ApplyCardVisuals(_currentCard, _nextCard);

        _cardStartedUtc = DateTime.UtcNow;
        _inputLocked = false;
        ActiveCardContainer.TranslationX = 0;
        ActiveCardContainer.TranslationY = 0;

        if (animated)
        {
            ActiveCardContainer.Scale = 0.92;
            ActiveCardContainer.Opacity = 0;
            await Task.WhenAll(
                ActiveCardContainer.FadeTo(1, 130, Easing.CubicOut),
                ActiveCardContainer.ScaleTo(1, 130, Easing.CubicOut));
        }
        else
        {
            ActiveCardContainer.Scale = 1;
            ActiveCardContainer.Opacity = 1;
        }
    }

    async Task OnSortAsync(int laneIndex)
    {
        if (_inputLocked || _isPaused || _isGameOver || _isTransitioning || _currentCard is null)
        {
            return;
        }

        await ResolveCardAsync(laneIndex, false);
    }

    async Task ResolveCardAsync(int laneIndex, bool autoResolved)
    {
        if (_currentCard is null || _currentCard.IsResolved || _isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        _inputLocked = true;
        _currentCard.IsResolved = true;

        bool isCorrect = autoResolved
            ? _currentCard.Mode == CardMode.Stop
            : _currentCard.Mode != CardMode.Stop && laneIndex == _currentCard.CorrectLane;

        if (isCorrect)
        {
            Difficulty difficulty = GetDifficulty();
            int points = CalculatePoints(difficulty, _currentCard, autoResolved);
            _score += points;
            _currentStreak++;
            _longestStreak = Math.Max(_longestStreak, _currentStreak);
            _resolvedCount++;

            if (_currentCard.Mode == CardMode.Grey)
            {
                _greySorted++;
            }
            else if (_currentCard.Mode == CardMode.Stop)
            {
                _stopHeld++;
            }

            UpdateHud();
            await Task.WhenAll(
                FlashScoreDeltaAsync($"+{points}", autoResolved ? Color.FromArgb("#FFD45A") : Colors.White),
                AnimateCardToLaneAsync(_currentCard.CorrectLane, true));

            if (_resolvedCount % GetDifficulty().TargetSwapEvery == 0)
            {
                SetupTargets(forceSwap: true);
            }
        }
        else
        {
            _misses++;
            _currentStreak = 0;
            UpdateHud();
            await Task.WhenAll(
                FlashScoreDeltaAsync("MISS", Color.FromArgb("#FFD6E0")),
                AnimateWrongSortAsync(laneIndex));
        }

        if (_timeLeft <= 0)
        {
            await EndGameAsync();
            return;
        }

        await SpawnNextCardAsync(animated: true);
        _isTransitioning = false;
    }

    async Task HandleMissAsync()
    {
        if (_currentCard is null || _currentCard.IsResolved || _isTransitioning)
        {
            return;
        }

        _currentCard.IsResolved = true;
        _isTransitioning = true;
        _inputLocked = true;
        _misses++;
        _currentStreak = 0;
        UpdateHud();

        await Task.WhenAll(
            FlashScoreDeltaAsync("MISS", Color.FromArgb("#FFD6E0")),
            ActiveCardContainer.FadeTo(0.25, 120, Easing.CubicIn));

        if (_timeLeft <= 0)
        {
            await EndGameAsync();
            return;
        }

        await SpawnNextCardAsync(animated: true);
        _isTransitioning = false;
    }

    void SetupTargets(bool forceSwap)
    {
        Difficulty difficulty = GetDifficulty();
        CardTone[] tones = Palette.Take(difficulty.PaletteSize).ToArray();
        MotifKind[] motifs = GetActiveMotifs(difficulty.Stage);

        if (!forceSwap && _leftLane is not null && _rightLane is not null)
        {
            return;
        }

        CardTone leftTone = tones[_random.Next(tones.Length)];
        CardTone rightTone = tones.Where(t => !ReferenceEquals(t, leftTone)).OrderBy(_ => _random.Next()).FirstOrDefault() ?? tones[0];

        MotifKind leftMotif = motifs[_random.Next(motifs.Length)];
        MotifKind rightMotif = motifs.Where(m => m != leftMotif).OrderBy(_ => _random.Next()).FirstOrDefault();

        _leftLane = new TargetLane(0, leftMotif, leftTone);
        _rightLane = new TargetLane(1, rightMotif, rightTone);

        ApplyTargetVisual(LeftTargetBorder, LeftTargetGlyphLabel, LeftTargetCaptionLabel, _leftLane);
        ApplyTargetVisual(RightTargetBorder, RightTargetGlyphLabel, RightTargetCaptionLabel, _rightLane);

        LeftSortButton.BackgroundColor = Color.FromArgb(leftTone.FillHex);
        RightSortButton.BackgroundColor = Color.FromArgb(rightTone.FillHex);
        BoardShell.Stroke = Color.FromArgb(leftTone.AccentHex);
        HintBorder.BackgroundColor = Color.FromArgb(_rightLane.Tone.DarkHex);
    }

    SortCard CreateCard()
    {
        if (_leftLane is null || _rightLane is null)
        {
            throw new InvalidOperationException("Targets must be created before cards.");
        }

        Difficulty difficulty = GetDifficulty();
        TargetLane target = _random.NextDouble() < 0.5 ? _leftLane : _rightLane;
        double roll = _random.NextDouble();

        if (difficulty.StopChance > 0 && roll < difficulty.StopChance)
        {
            return new SortCard
            {
                Motif = target.Motif,
                Tone = target.Tone,
                Mode = CardMode.Stop,
                CorrectLane = target.LaneIndex,
                SpawnedUtc = DateTime.UtcNow
            };
        }

        if (difficulty.GreyChance > 0 && roll < difficulty.StopChance + difficulty.GreyChance)
        {
            return new SortCard
            {
                Motif = target.Motif,
                Tone = GreyTone,
                Mode = CardMode.Grey,
                CorrectLane = target.LaneIndex,
                SpawnedUtc = DateTime.UtcNow
            };
        }

        TargetLane opposite = target.LaneIndex == 0 ? _rightLane : _leftLane;
        MotifKind motif = difficulty.Stage >= 2 && _random.NextDouble() < 0.55
            ? opposite.Motif
            : target.Motif;

        return new SortCard
        {
            Motif = motif,
            Tone = target.Tone,
            Mode = CardMode.Color,
            CorrectLane = target.LaneIndex,
            SpawnedUtc = DateTime.UtcNow
        };
    }

    void ApplyCardVisuals(SortCard currentCard, SortCard nextCard)
    {
        ActiveCardShell.BackgroundColor = Color.FromArgb(currentCard.Tone.FillHex);
        ActiveCardShell.Stroke = currentCard.Mode == CardMode.Grey
            ? Color.FromArgb("#E4E4F0")
            : Color.FromArgb(currentCard.Tone.AccentHex);
        ActiveCardGlyphLabel.Text = GetMotifGlyph(currentCard.Motif);
        ActiveCardCaptionLabel.Text = currentCard.Mode switch
        {
            CardMode.Grey => "Sort by image",
            CardMode.Stop => "Do not tap",
            _ => currentCard.Tone.Name
        };

        StopBadge.IsVisible = currentCard.Mode == CardMode.Stop;

        NextCardShell.BackgroundColor = Color.FromArgb(nextCard.Mode == CardMode.Grey ? GreyTone.DarkHex : nextCard.Tone.DarkHex);
        NextCardGlyphLabel.Text = nextCard.Mode == CardMode.Stop ? "STOP" : GetMotifGlyph(nextCard.Motif);
    }

    void ApplyTargetVisual(Border border, Label glyphLabel, Label captionLabel, TargetLane lane)
    {
        border.BackgroundColor = Color.FromArgb(lane.Tone.DarkHex);
        border.Stroke = Color.FromArgb(lane.Tone.AccentHex);
        glyphLabel.Text = GetMotifGlyph(lane.Motif);
        captionLabel.Text = $"{lane.Tone.Name} {GetMotifName(lane.Motif)}";
    }

    int CalculatePoints(Difficulty difficulty, SortCard card, bool autoResolved)
    {
        double elapsed = Math.Clamp((DateTime.UtcNow - _cardStartedUtc).TotalSeconds, 0, difficulty.TravelSeconds);
        int speedBonus = (int)Math.Round(((difficulty.TravelSeconds - elapsed) / difficulty.TravelSeconds) * 36);
        int streakBonus = Math.Min(70, Math.Max(0, _currentStreak) * 8);
        int specialBonus = card.Mode switch
        {
            CardMode.Grey => 24,
            CardMode.Stop when autoResolved => 34,
            _ => 0
        };

        return difficulty.BasePoints + speedBonus + streakBonus + specialBonus;
    }

    async Task AnimateCardToLaneAsync(int laneIndex, bool correct)
    {
        double laneOffset = GetLaneOffset(laneIndex);
        double topOffset = -(GetTravelDistance() + 56);

        await Task.WhenAll(
            ActiveCardContainer.TranslateTo(laneOffset, topOffset, 220, Easing.CubicInOut),
            ActiveCardContainer.ScaleTo(correct ? 0.9 : 0.95, 220, Easing.CubicInOut),
            ActiveCardContainer.FadeTo(0.18, 220, Easing.CubicOut));

        ActiveCardContainer.Opacity = 0;
        ActiveCardContainer.Scale = 1;
        ActiveCardContainer.TranslationX = 0;
        ActiveCardContainer.TranslationY = 0;
    }

    async Task AnimateWrongSortAsync(int laneIndex)
    {
        double laneOffset = laneIndex == 0 ? -48 : 48;
        Color original = ActiveCardShell.BackgroundColor ?? Color.FromArgb("#FF5D87");
        ActiveCardShell.BackgroundColor = Color.FromArgb("#D62E5B");

        await Task.WhenAll(
            ActiveCardContainer.TranslateTo(laneOffset, ActiveCardContainer.TranslationY - 24, 120, Easing.CubicOut),
            ActiveCardContainer.ScaleTo(0.95, 120, Easing.CubicOut));
        await Task.WhenAll(
            ActiveCardContainer.TranslateTo(0, ActiveCardContainer.TranslationY + 12, 110, Easing.CubicIn),
            ActiveCardContainer.ScaleTo(1, 110, Easing.CubicIn));

        ActiveCardShell.BackgroundColor = original;
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

    void UpdateHud()
    {
        TimerLabel.Text = $"00:{_timeLeft:00}";
        ScoreLabel.Text = _score.ToString();
    }

    void UpdateHint()
    {
        HintLabel.Text = GetDifficulty().Stage switch
        {
            0 => "Match the card color to the correct column.",
            1 => "Grey cards sort by image. Colored cards sort by color.",
            _ => "Grey cards sort by image. Stop cards should be left alone."
        };
    }

    Difficulty GetDifficulty()
    {
        int elapsed = 45 - _timeLeft;
        if (elapsed >= 34 || _score >= 3400)
        {
            return Difficulties[3];
        }

        if (elapsed >= 22 || _score >= 1700)
        {
            return Difficulties[2];
        }

        if (elapsed >= 10 || _score >= 750)
        {
            return Difficulties[1];
        }

        return Difficulties[0];
    }

    static MotifKind[] GetActiveMotifs(int stage) => stage switch
    {
        0 => new[] { MotifKind.Kite, MotifKind.Bridge, MotifKind.Arrow },
        1 => new[] { MotifKind.Kite, MotifKind.Bridge, MotifKind.Arrow, MotifKind.Tower },
        _ => new[] { MotifKind.Kite, MotifKind.Bridge, MotifKind.Arrow, MotifKind.Tower, MotifKind.Crest }
    };

    static string GetMotifGlyph(MotifKind motif) => motif switch
    {
        MotifKind.Kite => "◢◆",
        MotifKind.Bridge => "◇◣",
        MotifKind.Arrow => "△◆",
        MotifKind.Tower => "◥◢",
        _ => "◆△"
    };

    static string GetMotifName(MotifKind motif) => motif switch
    {
        MotifKind.Kite => "Kite",
        MotifKind.Bridge => "Bridge",
        MotifKind.Arrow => "Arrow",
        MotifKind.Tower => "Tower",
        _ => "Crest"
    };

    double GetTravelDistance()
    {
        double boardHeight = BoardShell.Height > 0 ? BoardShell.Height : 430;
        return Math.Max(170, boardHeight * 0.52);
    }

    double GetLaneOffset(int laneIndex)
    {
        double boardWidth = BoardShell.Width > 0 ? BoardShell.Width : 320;
        double offset = Math.Max(72, boardWidth * 0.24);
        return laneIndex == 0 ? -offset : offset;
    }

    async Task EndGameAsync()
    {
        if (_isGameOver)
        {
            return;
        }

        _isGameOver = true;
        _inputLocked = true;
        StopTimers();

        bool isNewBest = _score > _bestScore;
        int bestScore = Math.Max(_bestScore, _score);
        int apexPoints = BrainScoreService.RecordGameScore("must_sort", BrainSkill.Focus, _score, MustSortProgress.ExpectedTopScore);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Must Sort",
                score: _score,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new MustSortGamePage(),
                accentHex: "#FF8B42",
                secondaryLabel: "Rank",
                secondaryValue: MustSortProgress.ResolveRank(bestScore)));
    }

    async void OnLeftSortClicked(object sender, EventArgs e)
    {
        await OnSortAsync(0);
    }

    async void OnRightSortClicked(object sender, EventArgs e)
    {
        await OnSortAsync(1);
    }

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isGameOver)
        {
            return;
        }

        _pausedProgressSeconds = GetProgressSeconds();
        _isPaused = true;

        var action = await GamePauseService.ShowAsync(
            this,
            "Must Sort",
            "Sort each moving card into the correct lane, and watch out for grey and stop cards.");

        if (action == GamePauseAction.Restart)
        {
            StopTimers();
            await GamePauseService.RestartCurrentPageAsync(this);
            return;
        }

        if (action == GamePauseAction.Exit)
        {
            StopTimers();
            await PageTransitionService.PopAsync(Navigation);
            return;
        }

        _isPaused = false;
        _cardStartedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(_pausedProgressSeconds);
    }

    void OnResumeClicked(object sender, EventArgs e)
    {
        PauseOverlay.IsVisible = false;
        _isPaused = false;
        _cardStartedUtc = DateTime.UtcNow - TimeSpan.FromSeconds(_pausedProgressSeconds);
    }

    double GetProgressSeconds()
    {
        if (_currentCard is null)
        {
            return 0;
        }

        return Math.Clamp((DateTime.UtcNow - _cardStartedUtc).TotalSeconds, 0, GetDifficulty().TravelSeconds);
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(
            Navigation,
            new MustSortInsightsPage(_score, _greySorted, _stopHeld, _longestStreak, _misses));
    }
}
