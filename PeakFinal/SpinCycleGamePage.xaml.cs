using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class SpinCycleGamePage : ContentPage
{
    enum CompareAttribute
    {
        Color,
        Shape,
        Quantity,
        Size,
        Speed
    }

    enum ShapeKind
    {
        Square,
        Circle
    }

    enum SizeKind
    {
        Small,
        Large
    }

    enum SpeedKind
    {
        Slow,
        Fast
    }

    sealed class PatternModel
    {
        public int ColorIndex { get; set; }
        public ShapeKind Shape { get; set; }
        public int Count { get; set; }
        public SizeKind Size { get; set; }
        public SpeedKind Speed { get; set; }
        public int LayoutVariant { get; set; }

        public PatternModel Clone()
        {
            return new PatternModel
            {
                ColorIndex = ColorIndex,
                Shape = Shape,
                Count = Count,
                Size = Size,
                Speed = Speed,
                LayoutVariant = LayoutVariant
            };
        }
    }

    const int StartingTimeSeconds = 45;
    const int BasePoints = 30;
    const int ComboStep = 4;
    const int MaxMultiplier = 4;

    readonly Random _random = new();
    readonly Color[] _palette =
    {
        Color.FromArgb("#F34A8D"),
        Color.FromArgb("#36C964"),
        Color.FromArgb("#F4CC35"),
        Color.FromArgb("#30A7EE")
    };

    readonly Point[] _ring6 =
    {
        new(0.50, 0.18),
        new(0.74, 0.32),
        new(0.74, 0.68),
        new(0.50, 0.82),
        new(0.26, 0.68),
        new(0.26, 0.32)
    };

    readonly Point[][] _count2Layouts =
    {
        new[] { new Point(0.30, 0.36), new Point(0.70, 0.64) },
        new[] { new Point(0.30, 0.64), new Point(0.70, 0.36) },
        new[] { new Point(0.50, 0.26), new Point(0.50, 0.74) }
    };

    readonly Point[][] _count4Layouts =
    {
        new[] { new Point(0.30, 0.36), new Point(0.70, 0.36), new Point(0.30, 0.64), new Point(0.70, 0.64) },
        new[] { new Point(0.50, 0.22), new Point(0.78, 0.50), new Point(0.50, 0.78), new Point(0.22, 0.50) }
    };

    IDispatcherTimer? _gameTimer;
    IDispatcherTimer? _pulseTimer;

    PatternModel? _previousPattern;
    PatternModel? _currentPattern;

    readonly List<Border> _shapeViews = new();

    bool _expectedTrue;
    bool _inputEnabled;
    bool _isPaused;
    bool _isGameOver;
    bool _started;
    bool _transitioning;
    bool _pulseDown;

    int _score;
    int _finalScore;
    int _timeLeft;
    int _multiplier = 1;
    int _comboProgress;
    int _streak;
    int _longestStreak;

    public SpinCycleGamePage()
    {
        InitializeComponent();
        PatternCanvas.SizeChanged += OnPatternCanvasSizeChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("spin_cycle");

        if (_started)
            return;

        _started = true;
        _ = StartGameAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimers();
        _ = GameAudioService.StopGameAtmosphereAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    async Task StartGameAsync()
    {
        _isGameOver = false;
        _isPaused = false;
        _transitioning = true;
        _inputEnabled = false;
        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        FeedbackOverlay.IsVisible = false;

        _score = 0;
        _finalScore = 0;
        _timeLeft = StartingTimeSeconds;
        _multiplier = 1;
        _comboProgress = 0;
        _streak = 0;
        _longestStreak = 0;
        _pulseDown = false;
        _previousPattern = null;
        _currentPattern = null;

        StatementLabel.Text = "Observe each pattern";
        UpdateHud();
        SetButtonsEnabled(false);

        await RunCountdownAsync();

        _previousPattern = CreateRandomPattern();
        RenderPattern(_previousPattern);
        await Task.Delay(900);

        StartGameTimer();
        _transitioning = false;
        await ShowNextQuestionAsync();
    }

    async Task RunCountdownAsync()
    {
        CountdownOverlay.IsVisible = true;
        for (int i = 3; i >= 1; i--)
        {
            CountdownLabel.Text = i.ToString();
            CountdownLabel.Scale = 1;
            await CountdownLabel.ScaleTo(1.25, 180, Easing.CubicOut);
            await CountdownLabel.ScaleTo(1, 130, Easing.CubicIn);
            await Task.Delay(250);
        }

        CountdownOverlay.IsVisible = false;
    }

    void StartGameTimer()
    {
        _gameTimer = Dispatcher.CreateTimer();
        _gameTimer.Interval = TimeSpan.FromSeconds(1);
        _gameTimer.Tick += OnTimerTick;
        _gameTimer.Start();
    }

    void StartPulseTimer(SpeedKind speed)
    {
        if (_pulseTimer is not null)
        {
            _pulseTimer.Stop();
            _pulseTimer.Tick -= OnPulseTick;
            _pulseTimer = null;
        }

        _pulseTimer = Dispatcher.CreateTimer();
        _pulseTimer.Interval = speed == SpeedKind.Fast ? TimeSpan.FromMilliseconds(280) : TimeSpan.FromMilliseconds(560);
        _pulseTimer.Tick += OnPulseTick;
        _pulseTimer.Start();
    }

    void StopTimers()
    {
        if (_gameTimer is not null)
        {
            _gameTimer.Stop();
            _gameTimer.Tick -= OnTimerTick;
            _gameTimer = null;
        }

        if (_pulseTimer is not null)
        {
            _pulseTimer.Stop();
            _pulseTimer.Tick -= OnPulseTick;
            _pulseTimer = null;
        }
    }

    void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _transitioning)
            return;

        _timeLeft = Math.Max(0, _timeLeft - 1);
        TimerLabel.Text = $"00:{_timeLeft:00}";

        if (_timeLeft <= 0)
            _ = EndGameAsync();
    }

    void OnPulseTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver)
            return;

        _pulseDown = !_pulseDown;
        double scale = _pulseDown ? 0.88 : 1.0;

        foreach (var view in _shapeViews)
            view.Scale = scale;
    }

    async Task ShowNextQuestionAsync()
    {
        if (_isGameOver || _isPaused || _previousPattern is null)
            return;

        _currentPattern = GenerateNextPattern(_previousPattern);
        RenderPattern(_currentPattern);

        BuildPrompt(_previousPattern, _currentPattern, out _expectedTrue, out string prompt);
        StatementLabel.Text = prompt;

        _inputEnabled = true;
        SetButtonsEnabled(true);
        await Task.CompletedTask;
    }

    async Task HandleAnswerAsync(bool answeredTrue)
    {
        if (!_inputEnabled || _isPaused || _isGameOver || _transitioning)
            return;

        if (_previousPattern is null || _currentPattern is null)
            return;

        _inputEnabled = false;
        SetButtonsEnabled(false);
        _transitioning = true;

        bool correct = answeredTrue == _expectedTrue;
        int gained = 0;

        if (correct)
        {
            _streak++;
            _longestStreak = Math.Max(_longestStreak, _streak);

            _comboProgress++;
            if (_comboProgress > ComboStep)
                _comboProgress = 1;

            gained = BasePoints * _multiplier;
            _score += gained;

            if (_comboProgress == ComboStep && _multiplier < MaxMultiplier)
                _multiplier++;
        }
        else
        {
            _streak = 0;
            _comboProgress = 0;
        }

        UpdateHud();
        await ShowFeedbackAsync(correct, gained);

        if (_timeLeft <= 0)
        {
            await EndGameAsync();
            return;
        }

        _previousPattern = _currentPattern;
        _transitioning = false;
        await ShowNextQuestionAsync();
    }

    async Task ShowFeedbackAsync(bool correct, int gained)
    {
        FeedbackBadge.BackgroundColor = Color.FromArgb(correct ? "#19B861" : "#E93B3B");
        FeedbackIconLabel.Text = correct ? "\u2713" : "\u2715";

        FeedbackOverlay.Opacity = 0;
        FeedbackOverlay.Scale = 0.95;
        FeedbackOverlay.IsVisible = true;

        if (gained > 0)
            await FlashScoreDeltaAsync(gained);

        await Task.WhenAll(
            FeedbackOverlay.FadeTo(1, 120, Easing.CubicOut),
            FeedbackOverlay.ScaleTo(1, 140, Easing.CubicOut));

        await Task.Delay(220);
        await FeedbackOverlay.FadeTo(0, 120, Easing.CubicIn);
        FeedbackOverlay.IsVisible = false;
    }

    async Task FlashScoreDeltaAsync(int gained)
    {
        ScoreDeltaLabel.Text = $"+{gained}";
        ScoreDeltaLabel.TranslationY = 0;
        ScoreDeltaLabel.Opacity = 1;
        await Task.WhenAll(
            ScoreDeltaLabel.TranslateTo(0, -10, 320, Easing.CubicOut),
            ScoreDeltaLabel.FadeTo(0, 360, Easing.CubicOut));
    }

    async Task EndGameAsync()
    {
        if (_isGameOver)
            return;

        _isGameOver = true;
        _inputEnabled = false;
        _transitioning = true;
        SetButtonsEnabled(false);
        StopTimers();

        int streakBonus = _longestStreak * 100;
        _finalScore = _score + streakBonus;

        int best = BrainScoreService.GetGamePerformance("spin_cycle")?.BestScore ?? 0;
        bool isNewBest = _finalScore > best;

        int apexPoints = BrainScoreService.RecordGameScore(
            sourceId: "spin_cycle",
            skill: BrainSkill.Memory,
            rawScore: _finalScore,
            expectedTopScore: 2000);
        int bestScore = Math.Max(best, _finalScore);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Spin Cycle",
                score: _finalScore,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new SpinCycleGamePage(),
                accentHex: "#7F6AE6",
                secondaryLabel: "Rank",
                secondaryValue: bestScore >= 560 ? "Novice" : "Beginner"));
    }

    void UpdateHud()
    {
        ScoreLabel.Text = _score.ToString();
        MultiplierLabel.Text = $"x{_multiplier}";
        TimerLabel.Text = $"00:{_timeLeft:00}";

        Color on = Color.FromArgb("#F0D34A");
        Color off = Color.FromArgb("#6D7692");
        Dot1.Color = _comboProgress >= 1 ? on : off;
        Dot2.Color = _comboProgress >= 2 ? on : off;
        Dot3.Color = _comboProgress >= 3 ? on : off;
        Dot4.Color = _comboProgress >= 4 ? on : off;
    }

    void BuildPrompt(PatternModel previous, PatternModel current, out bool expectedTrue, out string prompt)
    {
        CompareAttribute attribute = (CompareAttribute)_random.Next(0, 5);
        bool actualSame = IsAttributeSame(attribute, previous, current);
        bool claimSame = _random.NextDouble() < 0.5;

        expectedTrue = claimSame == actualSame;
        prompt = BuildPromptText(attribute, claimSame);
    }

    static bool IsAttributeSame(CompareAttribute attribute, PatternModel previous, PatternModel current)
    {
        return attribute switch
        {
            CompareAttribute.Color => previous.ColorIndex == current.ColorIndex,
            CompareAttribute.Shape => previous.Shape == current.Shape,
            CompareAttribute.Quantity => previous.Count == current.Count,
            CompareAttribute.Size => previous.Size == current.Size,
            CompareAttribute.Speed => previous.Speed == current.Speed,
            _ => false
        };
    }

    string BuildPromptText(CompareAttribute attribute, bool claimSame)
    {
        if (claimSame)
        {
            return attribute switch
            {
                CompareAttribute.Color => Pick("The color is the same", "The color is identical"),
                CompareAttribute.Shape => Pick("The shape is the same", "The shape is a match"),
                CompareAttribute.Quantity => Pick("The quantity is the same", "The quantity is identical"),
                CompareAttribute.Size => Pick("The size is the same", "The size is identical"),
                CompareAttribute.Speed => Pick("The speed is the same", "The speed is identical"),
                _ => "The pattern is the same"
            };
        }

        return attribute switch
        {
            CompareAttribute.Color => Pick("The color has changed", "The color is different", "The color is a mismatch"),
            CompareAttribute.Shape => Pick("The shape has changed", "The shape is different"),
            CompareAttribute.Quantity => Pick("The quantity has changed", "The quantity is different"),
            CompareAttribute.Size => Pick("The size has changed", "The size is different"),
            CompareAttribute.Speed => Pick("The speed has changed", "The speed is different"),
            _ => "The pattern has changed"
        };
    }

    PatternModel CreateRandomPattern()
    {
        int count = _random.NextDouble() < 0.5 ? 2 : 4;
        return new PatternModel
        {
            ColorIndex = _random.Next(_palette.Length),
            Shape = _random.NextDouble() < 0.5 ? ShapeKind.Square : ShapeKind.Circle,
            Count = count,
            Size = _random.NextDouble() < 0.5 ? SizeKind.Small : SizeKind.Large,
            Speed = _random.NextDouble() < 0.5 ? SpeedKind.Slow : SpeedKind.Fast,
            LayoutVariant = PickLayoutVariant(count)
        };
    }

    PatternModel GenerateNextPattern(PatternModel previous)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            var next = previous.Clone();
            int changeCount = _random.NextDouble() < 0.72 ? 1 : 2;
            var attrs = Shuffle(new[]
            {
                CompareAttribute.Color,
                CompareAttribute.Shape,
                CompareAttribute.Quantity,
                CompareAttribute.Size,
                CompareAttribute.Speed
            });

            foreach (var attribute in attrs.Take(changeCount))
            {
                switch (attribute)
                {
                    case CompareAttribute.Color:
                        next.ColorIndex = PickDifferentIndex(next.ColorIndex, _palette.Length);
                        break;
                    case CompareAttribute.Shape:
                        next.Shape = next.Shape == ShapeKind.Circle ? ShapeKind.Square : ShapeKind.Circle;
                        break;
                    case CompareAttribute.Quantity:
                        next.Count = next.Count == 2 ? 4 : 2;
                        next.LayoutVariant = PickLayoutVariant(next.Count);
                        break;
                    case CompareAttribute.Size:
                        next.Size = next.Size == SizeKind.Small ? SizeKind.Large : SizeKind.Small;
                        break;
                    case CompareAttribute.Speed:
                        next.Speed = next.Speed == SpeedKind.Slow ? SpeedKind.Fast : SpeedKind.Slow;
                        break;
                }
            }

            // Keep movement feeling alive even when quantity is unchanged.
            if (_random.NextDouble() < 0.35)
                next.LayoutVariant = PickLayoutVariant(next.Count);

            if (!PatternsEqual(previous, next))
                return next;
        }

        return CreateRandomPattern();
    }

    static bool PatternsEqual(PatternModel a, PatternModel b)
    {
        return a.ColorIndex == b.ColorIndex
               && a.Shape == b.Shape
               && a.Count == b.Count
               && a.Size == b.Size
               && a.Speed == b.Speed
               && a.LayoutVariant == b.LayoutVariant;
    }

    int PickLayoutVariant(int count)
    {
        return count == 2
            ? _random.Next(_count2Layouts.Length)
            : _random.Next(_count4Layouts.Length);
    }

    int PickDifferentIndex(int current, int maxExclusive)
    {
        if (maxExclusive <= 1)
            return current;

        int candidate = _random.Next(maxExclusive - 1);
        return candidate >= current ? candidate + 1 : candidate;
    }

    IReadOnlyList<Point> GetPatternPoints(PatternModel pattern)
    {
        if (pattern.Count == 2)
            return _count2Layouts[Math.Clamp(pattern.LayoutVariant, 0, _count2Layouts.Length - 1)];

        if (pattern.Count == 4)
            return _count4Layouts[Math.Clamp(pattern.LayoutVariant, 0, _count4Layouts.Length - 1)];

        return _ring6.Take(Math.Clamp(pattern.Count, 1, _ring6.Length)).ToArray();
    }

    void RenderPattern(PatternModel pattern)
    {
        PatternCanvas.Children.Clear();
        _shapeViews.Clear();

        var color = _palette[Math.Clamp(pattern.ColorIndex, 0, _palette.Length - 1)];
        double size = pattern.Size == SizeKind.Large ? 86 : 66;
        double canvasWidth = PatternCanvas.Width > 0 ? PatternCanvas.Width : 320;
        double canvasHeight = PatternCanvas.Height > 0 ? PatternCanvas.Height : 460;

        foreach (var point in GetPatternPoints(pattern))
        {
            float corner = pattern.Shape == ShapeKind.Circle ? (float)(size / 2.0) : 8f;

            var shape = new Border
            {
                WidthRequest = size,
                HeightRequest = size,
                BackgroundColor = color,
                Stroke = color.WithAlpha(0.95f),
                StrokeThickness = 2,
                StrokeShape = new RoundRectangle { CornerRadius = corner },
                Shadow = new Shadow
                {
                    Brush = new SolidColorBrush(color.WithAlpha(0.8f)),
                    Opacity = 1,
                    Radius = 18,
                    Offset = new Point(0, 0)
                }
            };

            double x = (canvasWidth * point.X) - (size / 2);
            double y = (canvasHeight * point.Y) - (size / 2);
            AbsoluteLayout.SetLayoutBounds(shape, new Rect(x, y, size, size));
            PatternCanvas.Children.Add(shape);
            _shapeViews.Add(shape);
        }

        _pulseDown = false;
        StartPulseTimer(pattern.Speed);
    }

    List<T> Shuffle<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    string Pick(params string[] values)
    {
        return values[_random.Next(values.Length)];
    }

    void SetButtonsEnabled(bool enabled)
    {
        FalseButton.IsEnabled = enabled;
        TrueButton.IsEnabled = enabled;
    }

    void OnPatternCanvasSizeChanged(object? sender, EventArgs e)
    {
        if (_currentPattern is not null)
        {
            RenderPattern(_currentPattern);
            return;
        }

        if (_previousPattern is not null && !_inputEnabled)
            RenderPattern(_previousPattern);
    }

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isGameOver || _transitioning)
            return;

        _isPaused = true;
        _inputEnabled = false;
        SetButtonsEnabled(false);

        var action = await GamePauseService.ShowAsync(
            this,
            "Spin Cycle",
            "Compare the two moving patterns and answer whether they match before time runs down.");

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
        _inputEnabled = true;
        SetButtonsEnabled(true);
    }

    void OnResumeClicked(object sender, EventArgs e)
    {
        if (_isGameOver)
            return;

        _isPaused = false;
        _inputEnabled = true;
        SetButtonsEnabled(true);
        PauseOverlay.IsVisible = false;
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        StopTimers();
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnFalseClicked(object sender, EventArgs e)
    {
        await HandleAnswerAsync(false);
    }

    async void OnTrueClicked(object sender, EventArgs e)
    {
        await HandleAnswerAsync(true);
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new SpinCycleInsightsPage(_finalScore, _longestStreak, _multiplier));
        Navigation.RemovePage(this);
    }
}

