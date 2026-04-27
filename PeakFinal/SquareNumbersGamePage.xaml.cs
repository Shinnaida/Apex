using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class SquareNumbersGamePage : ContentPage
{
    private sealed class TileState
    {
        public required int Value { get; init; }
        public required Border View { get; init; }
        public required Label Label { get; init; }
        public required string BaseColor { get; init; }
    }

    private sealed record RoundDefinition(int Target, int FirstValue, int SecondValue, int DistractorValue);

    private static readonly string[] BaseTileColors =
    {
        "#1788D7",
        "#5968D2",
        "#9CB73D"
    };

    private static readonly Rect[][] LayoutVariants =
    {
        new[]
        {
            new Rect(0.62, 0.22, 0.26, 0.13),
            new Rect(0.22, 0.54, 0.26, 0.13),
            new Rect(0.72, 0.76, 0.26, 0.13)
        },
        new[]
        {
            new Rect(0.24, 0.28, 0.26, 0.13),
            new Rect(0.70, 0.30, 0.26, 0.13),
            new Rect(0.34, 0.72, 0.26, 0.13)
        },
        new[]
        {
            new Rect(0.70, 0.26, 0.26, 0.13),
            new Rect(0.30, 0.58, 0.26, 0.13),
            new Rect(0.70, 0.58, 0.26, 0.13)
        }
    };

    private const int StartingTimeSeconds = 66;
    private const int MaxRoundScore = 750;
    private const int MinRoundScore = 500;

    private readonly Random _random = new();
    private readonly List<TileState> _tiles = new();
    private readonly List<BoxView> _confettiPieces = new();
    private IDispatcherTimer? _gameTimer;
    private DateTime _roundStartedUtc;
    private int _timeLeft;
    private int _score;
    private int _bestScore;
    private int _selectedIndex = -1;
    private int _target;
    private int _currentStreak;
    private int _longestStreak;
    private bool _started;
    private bool _isPaused;
    private bool _isGameOver;
    private bool _isTransitioning;
    private bool _timeExpired;

    public SquareNumbersGamePage()
    {
        InitializeComponent();
        BuildTiles();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("square_numbers");

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

    private void BuildTiles()
    {
        TileArena.Children.Clear();
        _tiles.Clear();

        for (int index = 0; index < 3; index++)
        {
            var label = new Label
            {
                FontSize = 34,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.White
            };

            var border = new Border
            {
                BackgroundColor = Color.FromArgb(BaseTileColors[index]),
                Stroke = Colors.White,
                StrokeThickness = 4,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = label
            };

            int tileIndex = index;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OnTileTappedAsync(tileIndex);
            border.GestureRecognizers.Add(tap);

            AbsoluteLayout.SetLayoutFlags(border, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
            TileArena.Children.Add(border);

            _tiles.Add(new TileState
            {
                Value = 0,
                View = border,
                Label = label,
                BaseColor = BaseTileColors[index]
            });
        }
    }

    private async Task StartGameAsync()
    {
        ResetState();
        await RunCountdownAsync();
        StartTimer();
        await LoadNextRoundAsync();
    }

    private void ResetState()
    {
        _timeLeft = StartingTimeSeconds;
        _score = 0;
        _selectedIndex = -1;
        _target = 0;
        _currentStreak = 0;
        _longestStreak = 0;
        _isPaused = false;
        _isGameOver = false;
        _isTransitioning = false;
        _timeExpired = false;
        _bestScore = BrainScoreService.GetGamePerformance("square_numbers")?.BestScore ?? 0;

        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        FeedbackOverlay.IsVisible = false;
        ScoreDeltaLabel.Opacity = 0;
        CountdownOverlay.IsVisible = true;
        ConfettiLayer.Children.Clear();

        UpdateHud();
        TargetValueLabel.Text = string.Empty;
    }

    private async Task RunCountdownAsync()
    {
        LoadingSpinner.IsRunning = true;
        CountdownValueLabel.Text = string.Empty;
        CountdownCaptionLabel.Text = string.Empty;
        await Task.Delay(650);

        LoadingSpinner.IsRunning = false;
        CountdownCaptionLabel.Text = "GET READY";

        for (int count = 3; count >= 1; count--)
        {
            CountdownValueLabel.Text = count.ToString();
            CountdownValueLabel.Scale = 0.8;
            await CountdownValueLabel.ScaleTo(1, 170, Easing.CubicOut);
            await Task.Delay(260);
        }

        CountdownOverlay.IsVisible = false;
    }

    private void StartTimer()
    {
        StopTimer();

        _gameTimer = Dispatcher.CreateTimer();
        _gameTimer.Interval = TimeSpan.FromSeconds(1);
        _gameTimer.Tick += OnTimerTick;
        _gameTimer.Start();
    }

    private void StopTimer()
    {
        if (_gameTimer is null)
        {
            return;
        }

        _gameTimer.Stop();
        _gameTimer.Tick -= OnTimerTick;
        _gameTimer = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver)
        {
            return;
        }

        _timeLeft = Math.Max(0, _timeLeft - 1);
        UpdateHud();

        if (_timeLeft > 0)
        {
            return;
        }

        _timeExpired = true;

        if (_selectedIndex < 0 && !_isTransitioning)
        {
            _ = EndGameAsync();
        }
    }

    private async Task LoadNextRoundAsync()
    {
        if (_isGameOver)
        {
            return;
        }

        if (_timeExpired && _timeLeft <= 0)
        {
            await EndGameAsync();
            return;
        }

        _isTransitioning = true;
        _selectedIndex = -1;
        TargetValueLabel.Text = string.Empty;

        await Task.Delay(110);

        var round = CreateRound();
        _target = round.Target;

        var values = new[] { round.FirstValue, round.SecondValue, round.DistractorValue }
            .OrderBy(_ => _random.Next())
            .ToArray();
        var colors = BaseTileColors
            .OrderBy(_ => _random.Next())
            .ToArray();
        var layout = LayoutVariants[_random.Next(LayoutVariants.Length)];

        for (int index = 0; index < _tiles.Count; index++)
        {
            var tile = _tiles[index];
            _tiles[index] = new TileState
            {
                Value = values[index],
                View = tile.View,
                Label = tile.Label,
                BaseColor = colors[index]
            };

            var updatedTile = _tiles[index];
            updatedTile.Label.Text = values[index].ToString();
            updatedTile.View.BackgroundColor = Color.FromArgb(colors[index]);
            updatedTile.View.Stroke = Colors.White;
            updatedTile.View.Scale = 1;
            updatedTile.View.Rotation = 0;
            updatedTile.View.TranslationX = 0;
            updatedTile.View.TranslationY = 0;
            AbsoluteLayout.SetLayoutBounds(updatedTile.View, layout[index]);
        }

        TargetValueLabel.Text = _target.ToString();
        _roundStartedUtc = DateTime.UtcNow;
        _isTransitioning = false;
    }

    private RoundDefinition CreateRound()
    {
        while (true)
        {
            int first = _random.Next(1, 8);
            int second = _random.Next(1, 8);
            int target = first + second;

            int distractor = _random.Next(1, 8);
            bool distractorCreatesAnotherPair = distractor != first
                && distractor != second
                && (first + distractor == target || second + distractor == target);

            if (!distractorCreatesAnotherPair)
            {
                return new RoundDefinition(target, first, second, distractor);
            }
        }
    }

    private async Task OnTileTappedAsync(int index)
    {
        if (_isPaused || _isGameOver || _isTransitioning)
        {
            return;
        }

        if (_selectedIndex == index)
        {
            ResetTileAppearance(index);
            _selectedIndex = -1;
            return;
        }

        if (_selectedIndex < 0)
        {
            _selectedIndex = index;
            ApplySelectedAppearance(index);
            await PulseTileAsync(index);
            return;
        }

        int firstIndex = _selectedIndex;
        _selectedIndex = -1;
        await ResolveAttemptAsync(firstIndex, index);
    }

    private async Task ResolveAttemptAsync(int firstIndex, int secondIndex)
    {
        int total = _tiles[firstIndex].Value + _tiles[secondIndex].Value;

        if (total == _target)
        {
            ApplySelectedAppearance(firstIndex);
            ApplySelectedAppearance(secondIndex);
            int gained = CalculateRoundScore();
            _score += gained;
            _currentStreak++;
            _longestStreak = Math.Max(_longestStreak, _currentStreak);
            UpdateHud();

            await Task.WhenAll(
                ShowFeedbackAsync(isCorrect: true),
                FlashScoreDeltaAsync(gained));

            if (_timeExpired && _timeLeft <= 0)
            {
                await EndGameAsync();
                return;
            }

            await LoadNextRoundAsync();
            return;
        }

        ApplySelectedAppearance(firstIndex);
        ApplyWrongAppearance(secondIndex);
        _currentStreak = 0;

        await ShowFeedbackAsync(isCorrect: false);

        if (_timeExpired && _timeLeft <= 0)
        {
            await EndGameAsync();
            return;
        }

        await LoadNextRoundAsync();
    }

    private int CalculateRoundScore()
    {
        int elapsedSeconds = (int)Math.Floor((DateTime.UtcNow - _roundStartedUtc).TotalSeconds);
        int gained = MaxRoundScore - (elapsedSeconds * 5);
        return Math.Max(MinRoundScore, gained);
    }

    private async Task PulseTileAsync(int index)
    {
        await _tiles[index].View.ScaleTo(0.96, 60, Easing.CubicOut);
        await _tiles[index].View.ScaleTo(1, 80, Easing.CubicOut);
    }

    private async Task ShowFeedbackAsync(bool isCorrect)
    {
        FeedbackBadge.BackgroundColor = isCorrect
            ? Color.FromArgb("#19B861")
            : Color.FromArgb("#E53A3A");
        FeedbackIconLabel.Text = isCorrect ? "\u2713" : "\u2715";

        FeedbackOverlay.Opacity = 0;
        FeedbackOverlay.Scale = 0.9;
        FeedbackOverlay.IsVisible = true;

        await Task.WhenAll(
            FeedbackOverlay.FadeTo(1, 120, Easing.CubicOut),
            FeedbackOverlay.ScaleTo(1, 140, Easing.CubicOut));
        await Task.Delay(220);
        await FeedbackOverlay.FadeTo(0, 120, Easing.CubicIn);

        FeedbackOverlay.IsVisible = false;
    }

    private async Task FlashScoreDeltaAsync(int gained)
    {
        ScoreDeltaLabel.Text = $"+{gained}";
        ScoreDeltaLabel.Opacity = 1;
        ScoreDeltaLabel.TranslationY = 0;

        await Task.WhenAll(
            ScoreDeltaLabel.FadeTo(0, 520, Easing.CubicOut),
            ScoreDeltaLabel.TranslateTo(0, -14, 520, Easing.CubicOut));

        ScoreDeltaLabel.TranslationY = 0;
    }

    private async Task EndGameAsync()
    {
        if (_isGameOver)
        {
            return;
        }

        _isGameOver = true;
        _isTransitioning = true;
        StopTimer();

        bool isNewBest = _score > _bestScore;
        int bestScore = Math.Max(_bestScore, _score);
        int apexPoints = BrainScoreService.RecordGameScore(
            sourceId: "square_numbers",
            skill: BrainSkill.ProblemSolving,
            rawScore: _score,
            expectedTopScore: SquareNumbersProgress.ExpectedTopScore);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Square Numbers",
                score: _score,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new SquareNumbersGamePage(),
                accentHex: "#60D66E",
                secondaryLabel: "Rank",
                secondaryValue: SquareNumbersProgress.ResolveRank(bestScore)));
    }

    private async Task SpawnConfettiAsync()
    {
        ConfettiLayer.Children.Clear();
        _confettiPieces.Clear();

        string[] colors =
        {
            "#60D66E",
            "#F5C84B",
            "#5592F2",
            "#E56F9C",
            "#7F6AE6",
            "#F07E43"
        };

        double width = Width > 0 ? Width : 576;
        double height = Height > 0 ? Height : 1264;

        for (int index = 0; index < 28; index++)
        {
            var piece = new BoxView
            {
                Color = Color.FromArgb(colors[_random.Next(colors.Length)]),
                WidthRequest = _random.Next(6, 12),
                HeightRequest = _random.Next(8, 16),
                Rotation = _random.Next(0, 180)
            };

            double startX = _random.NextDouble() * width;
            double startY = -(_random.Next(20, 280));
            AbsoluteLayout.SetLayoutFlags(piece, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(piece, new Rect(startX, startY, piece.WidthRequest, piece.HeightRequest));
            ConfettiLayer.Children.Add(piece);
            _confettiPieces.Add(piece);
        }

        var tasks = _confettiPieces.Select(async piece =>
        {
            double drop = height * (0.55 + _random.NextDouble() * 0.35);
            uint duration = (uint)(1600 + _random.Next(0, 500));
            await Task.WhenAll(
                piece.TranslateTo(0, drop, duration, Easing.CubicIn),
                piece.RotateTo(piece.Rotation + _random.Next(90, 260), 1600u, Easing.Linear));
        });

        await Task.WhenAll(tasks);
    }

    private void UpdateHud()
    {
        TimerLabel.Text = $"00:{_timeLeft:00}";
        ScoreLabel.Text = _score.ToString();
    }

    private void ApplySelectedAppearance(int index)
    {
        _tiles[index].View.BackgroundColor = Color.FromArgb("#F6C73A");
        _tiles[index].View.Stroke = Colors.White;
    }

    private void ApplyWrongAppearance(int index)
    {
        _tiles[index].View.BackgroundColor = Color.FromArgb("#E54842");
        _tiles[index].View.Stroke = Colors.White;
    }

    private void ResetTileAppearance(int index)
    {
        _tiles[index].View.BackgroundColor = Color.FromArgb(_tiles[index].BaseColor);
        _tiles[index].View.Stroke = Colors.White;
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
            "Square Numbers",
            "Pick the correct number tiles quickly and keep the streak going before the clock runs out.");

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
        await PageTransitionService.PopAsync(Navigation);
    }
}
