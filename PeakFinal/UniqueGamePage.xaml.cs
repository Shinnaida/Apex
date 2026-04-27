using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class UniqueGamePage : ContentPage
{
    enum ShapeKind { Cross, Circle, Triangle }
    enum StyleKind { Solid, Outline, Split }
    enum DifferenceKind { Shape, Style, Color, Rotation, Scale }

    sealed record Difficulty(
        int Stage,
        int TileCount,
        double TileSize,
        double SizeVariance,
        double MinimumGap,
        DifferenceKind[] Differences,
        int PaletteSize,
        int BasePoints,
        string Hint);

    sealed record CardTone(string Name, string FillHex, string AccentHex);
    sealed record Appearance(ShapeKind Shape, StyleKind Style, CardTone Tone, double Rotation, double Scale);

    sealed class TileState
    {
        public required int Index { get; init; }
        public required Border View { get; init; }
        public required Grid Host { get; init; }
    }

    static readonly Difficulty[] Difficulties =
    {
        new(0, 6, 96, 10, 20, new[] { DifferenceKind.Shape }, 1, 90, "The unique shape stands apart. Difficulty rises with your score."),
        new(1, 7, 88, 10, 18, new[] { DifferenceKind.Shape, DifferenceKind.Style }, 2, 108, "More shapes scatter across the field as your score climbs."),
        new(2, 8, 82, 9, 16, new[] { DifferenceKind.Shape, DifferenceKind.Style, DifferenceKind.Color }, 3, 126, "Now color can be the only difference, so scan the whole screen."),
        new(3, 9, 76, 8, 14, new[] { DifferenceKind.Shape, DifferenceKind.Style, DifferenceKind.Color, DifferenceKind.Rotation }, 4, 148, "Rotation differences join the mix once the score heats up."),
        new(4, 10, 70, 8, 12, new[] { DifferenceKind.Shape, DifferenceKind.Style, DifferenceKind.Color, DifferenceKind.Rotation, DifferenceKind.Scale }, 4, 170, "At higher scores, smaller size shifts hide the unique shape."),
        new(5, 11, 64, 6, 10, new[] { DifferenceKind.Shape, DifferenceKind.Style, DifferenceKind.Color, DifferenceKind.Rotation, DifferenceKind.Scale }, 4, 190, "Late rounds pack the field with tighter spacing and more visual noise.")
    };

    static readonly CardTone[] Palette =
    {
        new("Rose", "#FF5D87", "#FFC7D6"),
        new("Coral", "#FF7B62", "#FFD4C7"),
        new("Sun", "#F2B72E", "#FFE8A7"),
        new("Blush", "#FF87B7", "#FFDDEC")
    };

    const int StartingTimeSeconds = 45;
    const int ScoreTierStep = 450;

    readonly Random _random = new();
    readonly List<TileState> _tiles = new();

    IDispatcherTimer? _timer;
    DateTime _boardStartedUtc = DateTime.UtcNow;
    int _uniqueIndex = -1;
    int _timeLeft;
    int _score;
    int _bestScore;
    int _boardsSolved;
    int _wrongTaps;
    int _currentStreak;
    int _longestStreak;
    int _fastestReactionMs = int.MaxValue;
    bool _started;
    bool _isPaused;
    bool _isGameOver;
    bool _inputLocked;
    bool _isTransitioning;

    public UniqueGamePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("unique");
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
        _boardsSolved = 0;
        _wrongTaps = 0;
        _currentStreak = 0;
        _longestStreak = 0;
        _fastestReactionMs = int.MaxValue;
        _bestScore = BrainScoreService.GetGamePerformance("unique")?.BestScore ?? 0;
        _isPaused = false;
        _isGameOver = false;
        _inputLocked = true;
        _isTransitioning = false;

        CountdownOverlay.IsVisible = true;
        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
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

    Difficulty GetDifficulty()
    {
        int tier = Math.Min(Difficulties.Length - 1, Math.Max(0, _score / ScoreTierStep));
        return Difficulties[tier];
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
                BoardShell.ScaleTo(0.985, 80, Easing.CubicIn),
                BoardShell.FadeTo(0.9, 80, Easing.CubicIn));
        }

        await WaitForBoardAsync();

        Difficulty difficulty = GetDifficulty();
        EnsureTileCount(difficulty.TileCount);

        Appearance dominant = CreateDominantAppearance(difficulty);
        DifferenceKind difference = difficulty.Differences[_random.Next(difficulty.Differences.Length)];
        Appearance unique = CreateUniqueAppearance(difficulty, dominant, difference);
        _uniqueIndex = _random.Next(_tiles.Count);

        List<Rect> placements = GeneratePlacements(difficulty, PuzzleField.Width, PuzzleField.Height);
        for (int i = 0; i < _tiles.Count; i++)
        {
            Rect placement = placements[i];
            ApplyPlacement(_tiles[i], placement);
            ApplyAppearance(_tiles[i], i == _uniqueIndex ? unique : dominant, placement.Width, i == _uniqueIndex);
        }

        StageLabel.Text = $"LEVEL {difficulty.Stage + 1}";
        ObjectiveCaptionLabel.Text = "ONE SHAPE DOES NOT MATCH";
        TargetPromptLabel.Text = "Tap the unique shape";
        HintLabel.Text = difficulty.Hint;
        _boardStartedUtc = DateTime.UtcNow;

        if (animated)
        {
            BoardShell.Scale = 1;
            await BoardShell.FadeTo(1, 120, Easing.CubicOut);
        }

        _isTransitioning = false;
        _inputLocked = false;
    }

    async Task WaitForBoardAsync()
    {
        for (int attempt = 0; attempt < 24; attempt++)
        {
            if (PuzzleField.Width > 120 && PuzzleField.Height > 180)
            {
                return;
            }

            await Task.Delay(16);
        }
    }

    void EnsureTileCount(int total)
    {
        if (_tiles.Count == total)
        {
            return;
        }

        _tiles.Clear();
        PuzzleField.Children.Clear();

        for (int index = 0; index < total; index++)
        {
            var host = new Grid
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };

            var border = new Border
            {
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                Padding = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 }
            };

            border.Content = host;

            var tile = new TileState
            {
                Index = index,
                View = border,
                Host = host
            };

            border.BindingContext = tile;
            var tap = new TapGestureRecognizer();
            tap.Tapped += OnTileTapped;
            border.GestureRecognizers.Add(tap);

            PuzzleField.Children.Add(border);
            _tiles.Add(tile);
        }
    }

    List<Rect> GeneratePlacements(Difficulty difficulty, double fieldWidth, double fieldHeight)
    {
        double width = Math.Max(260, fieldWidth);
        double height = Math.Max(320, fieldHeight);
        var placements = new List<Rect>(difficulty.TileCount);

        for (int i = 0; i < difficulty.TileCount; i++)
        {
            Rect best = new(0, 0, difficulty.TileSize, difficulty.TileSize);
            double bestSpacing = double.MinValue;

            for (int attempt = 0; attempt < 140; attempt++)
            {
                double size = Math.Clamp(
                    difficulty.TileSize + ((_random.NextDouble() * 2) - 1) * difficulty.SizeVariance,
                    52,
                    106);
                double x = _random.NextDouble() * Math.Max(1, width - size);
                double y = _random.NextDouble() * Math.Max(1, height - size);
                var candidate = new Rect(x, y, size, size);
                double spacing = placements.Count == 0
                    ? double.MaxValue
                    : placements.Min(existing => CenterDistance(candidate, existing) - ((candidate.Width + existing.Width) / 2));

                if (spacing >= difficulty.MinimumGap)
                {
                    best = candidate;
                    break;
                }

                if (spacing > bestSpacing)
                {
                    bestSpacing = spacing;
                    best = candidate;
                }
            }

            placements.Add(best);
        }

        return placements;
    }

    static double CenterDistance(Rect left, Rect right)
    {
        double leftCenterX = left.X + (left.Width / 2);
        double leftCenterY = left.Y + (left.Height / 2);
        double rightCenterX = right.X + (right.Width / 2);
        double rightCenterY = right.Y + (right.Height / 2);
        return Math.Sqrt(Math.Pow(leftCenterX - rightCenterX, 2) + Math.Pow(leftCenterY - rightCenterY, 2));
    }

    void ApplyPlacement(TileState tile, Rect placement)
    {
        tile.View.WidthRequest = placement.Width;
        tile.View.HeightRequest = placement.Height;
        tile.View.Scale = 1;
        tile.View.Rotation = 0;
        tile.View.TranslationX = 0;
        tile.View.TranslationY = 0;
        tile.View.Opacity = 1;
        tile.View.IsVisible = true;

        AbsoluteLayout.SetLayoutFlags(tile.View, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(tile.View, placement);
    }

    Appearance CreateDominantAppearance(Difficulty difficulty)
    {
        CardTone tone = Palette[_random.Next(Math.Min(difficulty.PaletteSize, Palette.Length))];
        ShapeKind shape = (ShapeKind)_random.Next(Enum.GetValues<ShapeKind>().Length);
        StyleKind style = difficulty.Stage == 0
            ? StyleKind.Solid
            : (StyleKind)_random.Next(Enum.GetValues<StyleKind>().Length);

        return new Appearance(shape, style, tone, 0, 1);
    }

    Appearance CreateUniqueAppearance(Difficulty difficulty, Appearance dominant, DifferenceKind difference)
    {
        return difference switch
        {
            DifferenceKind.Shape => dominant with
            {
                Shape = Enum.GetValues<ShapeKind>().First(shape => shape != dominant.Shape)
            },
            DifferenceKind.Style => dominant with
            {
                Style = Enum.GetValues<StyleKind>().First(style => style != dominant.Style)
            },
            DifferenceKind.Color => dominant with
            {
                Tone = Palette.Take(Math.Min(difficulty.PaletteSize, Palette.Length)).First(tone => tone != dominant.Tone)
            },
            DifferenceKind.Rotation when dominant.Shape != ShapeKind.Circle => dominant with
            {
                Rotation = _random.Next(0, 2) == 0 ? -20 : 20
            },
            DifferenceKind.Scale => dominant with
            {
                Scale = 0.8
            },
            _ => dominant with
            {
                Shape = Enum.GetValues<ShapeKind>().First(shape => shape != dominant.Shape)
            }
        };
    }

    void ApplyAppearance(TileState tile, Appearance appearance, double tileSize, bool isUnique)
    {
        tile.Host.Children.Clear();

        double haloSize = tileSize * 0.92;
        double symbolSize = tileSize * 0.74;
        Color toneFill = Color.FromArgb(appearance.Tone.FillHex);
        Color accent = Color.FromArgb(appearance.Tone.AccentHex);

        tile.Host.Children.Add(new Ellipse
        {
            WidthRequest = haloSize,
            HeightRequest = haloSize,
            Fill = new SolidColorBrush(toneFill.WithAlpha(appearance.Style == StyleKind.Outline ? 0.12f : 0.2f)),
            Stroke = new SolidColorBrush(accent.WithAlpha(appearance.Style == StyleKind.Outline ? 0.85f : 0.2f)),
            StrokeThickness = appearance.Style == StyleKind.Outline ? 2.2 : 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        });

        if (appearance.Style == StyleKind.Split)
        {
            tile.Host.Children.Add(new Border
            {
                BackgroundColor = accent.WithAlpha(0.22f),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                WidthRequest = haloSize * 0.34,
                HeightRequest = haloSize * 0.96,
                Rotation = -34,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            });
        }

        if (isUnique)
        {
            tile.Host.Children.Add(new Ellipse
            {
                WidthRequest = tileSize,
                HeightRequest = tileSize,
                Fill = new SolidColorBrush(Colors.Transparent),
                Stroke = new SolidColorBrush(Colors.White.WithAlpha(0.18f)),
                StrokeThickness = 2,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            });
        }

        tile.Host.Children.Add(BuildSymbolView(appearance, symbolSize));

        tile.View.Shadow = isUnique
            ? new Shadow
            {
                Brush = toneFill.WithAlpha(0.35f),
                Offset = new Point(0, 0),
                Radius = 18
            }
            : new Shadow
            {
                Brush = Colors.Transparent,
                Offset = new Point(0, 0),
                Radius = 0
            };
    }

    View BuildSymbolView(Appearance appearance, double symbolSize)
    {
        Color glyphColor = appearance.Style == StyleKind.Outline
            ? Color.FromArgb(appearance.Tone.AccentHex)
            : Colors.White;

        var host = new Grid
        {
            WidthRequest = symbolSize,
            HeightRequest = symbolSize,
            Scale = appearance.Scale,
            Rotation = appearance.Rotation,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        host.Children.Add(new Label
        {
            Text = appearance.Shape switch
            {
                ShapeKind.Circle => appearance.Style == StyleKind.Outline ? "\u25CB" : "\u25CF",
                ShapeKind.Triangle => appearance.Style == StyleKind.Outline ? "\u25B3" : "\u25B2",
                _ => "\u2715"
            },
            FontSize = appearance.Shape == ShapeKind.Cross ? symbolSize * 0.82 : symbolSize * 0.88,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = glyphColor
        });

        if (appearance.Style == StyleKind.Split)
        {
            host.Children.Add(new Border
            {
                BackgroundColor = Colors.White.WithAlpha(0.16f),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                WidthRequest = symbolSize * 0.18,
                HeightRequest = symbolSize * 0.92,
                Rotation = 35,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            });
        }

        return host;
    }

    async void OnTileTapped(object? sender, TappedEventArgs e)
    {
        if (_inputLocked || _isPaused || _isGameOver || _isTransitioning)
        {
            return;
        }

        if (sender is not Border border || border.BindingContext is not TileState tile)
        {
            return;
        }

        if (tile.Index == _uniqueIndex)
        {
            await HandleCorrectTapAsync(tile);
        }
        else
        {
            await HandleWrongTapAsync(tile);
        }
    }

    async Task HandleCorrectTapAsync(TileState tile)
    {
        _inputLocked = true;

        int reactionMs = Math.Max(180, (int)(DateTime.UtcNow - _boardStartedUtc).TotalMilliseconds);
        _fastestReactionMs = Math.Min(_fastestReactionMs, reactionMs);
        _boardsSolved++;
        _currentStreak++;
        _longestStreak = Math.Max(_longestStreak, _currentStreak);

        Difficulty difficulty = GetDifficulty();
        int speedBonus = (int)Math.Round(Math.Clamp((2200 - reactionMs) / 28.0, 0, 82));
        int streakBonus = Math.Min(72, (_currentStreak - 1) * 8);
        int gain = difficulty.BasePoints + speedBonus + streakBonus;
        _score += gain;

        UpdateHud();
        UpdateHint();
        _ = AnimateScoreDeltaAsync($"+{gain}", true);

        await Task.WhenAll(
            tile.View.ScaleTo(1.18, 110, Easing.CubicOut),
            tile.View.FadeTo(0.2, 110, Easing.CubicOut));

        await GenerateBoardAsync(animated: true);
    }

    async Task HandleWrongTapAsync(TileState tile)
    {
        _inputLocked = true;
        _wrongTaps++;
        _currentStreak = 0;
        _score = Math.Max(0, _score - 30);
        UpdateHud();
        UpdateHint();
        _ = AnimateScoreDeltaAsync("-30", false);

        tile.View.Rotation = -6;
        await tile.View.TranslateTo(-10, 0, 45, Easing.CubicIn);
        await tile.View.TranslateTo(10, 0, 65, Easing.CubicOut);
        await tile.View.TranslateTo(0, 0, 45, Easing.CubicIn);
        tile.View.Rotation = 0;

        _inputLocked = false;
    }

    async Task AnimateScoreDeltaAsync(string text, bool positive)
    {
        ScoreDeltaLabel.Text = text;
        ScoreDeltaLabel.TextColor = positive ? Colors.White : Color.FromArgb("#FFD8E2");
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

    void UpdateHint()
    {
        Difficulty difficulty = GetDifficulty();
        HintLabel.Text = $"{difficulty.Hint} Tier {difficulty.Stage + 1} unlocks every {ScoreTierStep} points.";
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

        int apexPoints = BrainScoreService.RecordGameScore("unique", BrainSkill.Focus, _score, UniqueProgress.ExpectedTopScore);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Unique",
                score: _score,
                bestScore: bestAfterRun,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new UniqueGamePage(),
                accentHex: "#6A6AFF",
                secondaryLabel: "Rank",
                secondaryValue: UniqueProgress.ResolveRank(bestAfterRun)));
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
            "Unique",
            "Spot the one symbol that breaks the pattern before the board changes again.");

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
            new UniqueInsightsPage(_score, _boardsSolved, fastest, _longestStreak, _wrongTaps));
        Navigation.RemovePage(this);
    }
}
