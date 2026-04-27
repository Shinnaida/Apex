using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class MatchaMadnessGamePage : ContentPage
{
    private sealed class MatchaTileState
    {
        public required Border CellBorder { get; init; }
        public required Grid CellHost { get; init; }
        public required List<MatchaLayerDescriptor> Layers { get; init; }
    }

    private const int Rows = 4;
    private const int Columns = 3;
    private const int StartingBonus = 1000;
    private const int BonusTick = 10;
    private const int BaseMatchScore = 20;
    private const int MatchesPerMultiplier = 5;
    private const int MaxMultiplier = 4;

    private readonly Random _random = new();
    private readonly List<MatchaTileState> _tiles = new();
    private readonly List<BoxView> _confettiPieces = new();
    private readonly List<MatchaLayerDescriptor> _descriptorCatalog = new()
    {
        new(MatchaLayerKind.Ring, "teal", 0.88),
        new(MatchaLayerKind.Ring, "coral", 0.88),
        new(MatchaLayerKind.Ring, "sky", 0.68),
        new(MatchaLayerKind.Ring, "orange", 0.58),
        new(MatchaLayerKind.Ring, "yellow", 0.24),
        new(MatchaLayerKind.Ring, "magenta", 0.52),
        new(MatchaLayerKind.Ring, "olive", 0.58),
        new(MatchaLayerKind.Ring, "sky", 0.18),
        new(MatchaLayerKind.Solid, "magenta", 0.84),
        new(MatchaLayerKind.Solid, "magenta", 0.80),
        new(MatchaLayerKind.Solid, "orange", 0.56),
        new(MatchaLayerKind.Solid, "olive", 0.58),
        new(MatchaLayerKind.Solid, "olive", 0.50),
        new(MatchaLayerKind.Solid, "lime", 0.28),
        new(MatchaLayerKind.Solid, "lime", 0.24),
        new(MatchaLayerKind.Solid, "pink", 0.34),
        new(MatchaLayerKind.Solid, "pink", 0.28),
        new(MatchaLayerKind.Solid, "mint", 0.20),
        new(MatchaLayerKind.Ring, "teal", 0.76),
        new(MatchaLayerKind.Ring, "coral", 0.78)
    };

    private IDispatcherTimer? _bonusTimer;
    private int _selectedIndex = -1;
    private int _bonusRemaining;
    private int _score;
    private int _matchCount;
    private int _multiplier = 1;
    private int _comboProgress;
    private int _bestScore;
    private int _boardBonusAwarded;
    private bool _started;
    private bool _isPaused;
    private bool _isGameOver;
    private bool _isBusy;

    public MatchaMadnessGamePage()
    {
        InitializeComponent();
        BuildBoard();
        BonusTrack.SizeChanged += (_, _) => UpdateBonusBar();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("matcha_madness");

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
        StopBonusTimer();
        _ = GameAudioService.StopGameAtmosphereAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    private async Task StartGameAsync()
    {
        ResetState();
        GenerateBoard();
        RenderAllTiles();
        UpdateHud();

        CountdownOverlay.IsVisible = true;
        LoadingSpinner.IsRunning = true;
        CountdownValueLabel.Text = string.Empty;
        await Task.Delay(700);

        LoadingSpinner.IsRunning = false;

        for (var count = 3; count >= 1; count--)
        {
            CountdownValueLabel.Text = count.ToString();
            CountdownValueLabel.Scale = 0.82;
            await CountdownValueLabel.ScaleTo(1, 170, Easing.CubicOut);
            await Task.Delay(260);
        }

        CountdownOverlay.IsVisible = false;
        StartBonusTimer();
    }

    private void ResetState()
    {
        _selectedIndex = -1;
        _score = 0;
        _matchCount = 0;
        _comboProgress = 0;
        _multiplier = 1;
        _bonusRemaining = StartingBonus;
        _boardBonusAwarded = 0;
        _bestScore = BrainScoreService.GetGamePerformance("matcha_madness")?.BestScore ?? 0;
        _isPaused = false;
        _isGameOver = false;
        _isBusy = false;
        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        ConfettiLayer.Children.Clear();
    }

    private void BuildBoard()
    {
        BoardGrid.Children.Clear();
        _tiles.Clear();

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var host = new Grid();
                var border = new Border
                {
                    BackgroundColor = Color.FromArgb("#76AEC1"),
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    Padding = 2,
                    Content = host
                };

                var index = (row * Columns) + column;
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await OnCellTappedAsync(index);
                border.GestureRecognizers.Add(tap);

                Grid.SetRow(border, row);
                Grid.SetColumn(border, column);
                BoardGrid.Children.Add(border);

                _tiles.Add(new MatchaTileState
                {
                    CellBorder = border,
                    CellHost = host,
                    Layers = new List<MatchaLayerDescriptor>()
                });
            }
        }
    }

    private void GenerateBoard()
    {
        var depths = new[] { 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2 };
        var shuffledCatalog = _descriptorCatalog
            .OrderBy(_ => _random.Next())
            .ToList();
        var descriptorIndex = 0;

        foreach (var tile in _tiles)
        {
            tile.Layers.Clear();
        }

        var maxDepth = depths.Max();
        for (var level = 0; level < maxDepth; level++)
        {
            var activeIndices = Enumerable.Range(0, _tiles.Count)
                .Where(index => depths[index] > level)
                .OrderBy(_ => _random.Next())
                .ToList();

            for (var i = 0; i < activeIndices.Count; i += 2)
            {
                if (descriptorIndex >= shuffledCatalog.Count)
                {
                    descriptorIndex = 0;
                }

                var descriptor = shuffledCatalog[descriptorIndex++];
                _tiles[activeIndices[i]].Layers.Add(descriptor);
                _tiles[activeIndices[i + 1]].Layers.Add(descriptor);
            }
        }
    }

    private void RenderAllTiles()
    {
        for (var i = 0; i < _tiles.Count; i++)
        {
            RenderTile(i);
        }
    }

    private void RenderTile(int index)
    {
        var tile = _tiles[index];
        tile.CellHost.Children.Clear();

        if (tile.Layers.Count > 0)
        {
            tile.CellHost.Children.Add(MatchaMadnessVisuals.CreatePatternView(tile.Layers, 86));
        }

        ResetTileState(tile.CellBorder);
    }

    private async Task OnCellTappedAsync(int index)
    {
        if (_isBusy || _isPaused || _isGameOver)
        {
            return;
        }

        if (_tiles[index].Layers.Count == 0)
        {
            return;
        }

        if (_selectedIndex == index)
        {
            ResetTileState(_tiles[index].CellBorder);
            _selectedIndex = -1;
            return;
        }

        if (_selectedIndex < 0)
        {
            _selectedIndex = index;
            ApplySelectedState(_tiles[index].CellBorder);
            return;
        }

        var firstIndex = _selectedIndex;
        var first = _tiles[firstIndex];
        var second = _tiles[index];
        _selectedIndex = -1;

        if (IsSameTopLayer(first.Layers[0], second.Layers[0]))
        {
            await HandleMatchAsync(firstIndex, index);
            return;
        }

        await HandleMismatchAsync(firstIndex, index);
    }

    private async Task HandleMatchAsync(int firstIndex, int secondIndex)
    {
        _isBusy = true;

        var first = _tiles[firstIndex];
        var second = _tiles[secondIndex];
        ApplySelectedState(first.CellBorder);
        ApplySelectedState(second.CellBorder);

        await Task.WhenAll(
            first.CellBorder.ScaleTo(0.94, 90, Easing.CubicOut),
            second.CellBorder.ScaleTo(0.94, 90, Easing.CubicOut));

        first.Layers.RemoveAt(0);
        second.Layers.RemoveAt(0);
        RenderTile(firstIndex);
        RenderTile(secondIndex);

        await SpawnSparklesAsync(first.CellBorder, second.CellBorder);

        var gained = BaseMatchScore * _multiplier;
        _score += gained;
        _matchCount++;
        _comboProgress++;

        if (_comboProgress >= MatchesPerMultiplier)
        {
            _comboProgress = 0;
            _multiplier = Math.Min(MaxMultiplier, _multiplier + 1);
        }

        UpdateHud();

        await Task.WhenAll(
            first.CellBorder.ScaleTo(1, 120, Easing.CubicOut),
            second.CellBorder.ScaleTo(1, 120, Easing.CubicOut));

        if (_tiles.All(tile => tile.Layers.Count == 0))
        {
            _boardBonusAwarded = _bonusRemaining;
            _score += _boardBonusAwarded;
            _bonusRemaining = 0;
            UpdateHud();
            await EndGameAsync(true);
        }

        _isBusy = false;
    }

    private async Task HandleMismatchAsync(int firstIndex, int secondIndex)
    {
        _isBusy = true;

        var first = _tiles[firstIndex].CellBorder;
        var second = _tiles[secondIndex].CellBorder;

        ApplyMismatchState(first);
        ApplyMismatchState(second);

        await Task.WhenAll(
            first.TranslateTo(-6, 0, 50, Easing.Linear),
            second.TranslateTo(6, 0, 50, Easing.Linear));
        await Task.WhenAll(
            first.TranslateTo(0, 0, 60, Easing.CubicOut),
            second.TranslateTo(0, 0, 60, Easing.CubicOut));

        _comboProgress = 0;
        _multiplier = 1;
        UpdateHud();

        ResetTileState(first);
        ResetTileState(second);
        _isBusy = false;
    }

    private void StartBonusTimer()
    {
        StopBonusTimer();

        _bonusTimer = Dispatcher.CreateTimer();
        _bonusTimer.Interval = TimeSpan.FromSeconds(1);
        _bonusTimer.Tick += OnBonusTick;
        _bonusTimer.Start();
    }

    private void StopBonusTimer()
    {
        if (_bonusTimer is null)
        {
            return;
        }

        _bonusTimer.Stop();
        _bonusTimer.Tick -= OnBonusTick;
        _bonusTimer = null;
    }

    private void OnBonusTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _isBusy)
        {
            return;
        }

        _bonusRemaining = Math.Max(0, _bonusRemaining - BonusTick);
        UpdateHud();

        if (_bonusRemaining <= 0)
        {
            _ = EndGameAsync(false);
        }
    }

    private void UpdateHud()
    {
        ScoreLabel.Text = _score.ToString();
        MultiplierLabel.Text = $"x{_multiplier}";
        BonusLabel.Text = _bonusRemaining.ToString();
        UpdateProgressDots();
        UpdateBonusBar();
    }

    private void UpdateProgressDots()
    {
        var activeColor = Color.FromArgb("#F4D22C");
        var idleColor = Color.FromArgb("#546664");

        Dot1.Color = _comboProgress >= 1 ? activeColor : idleColor;
        Dot2.Color = _comboProgress >= 2 ? activeColor : idleColor;
        Dot3.Color = _comboProgress >= 3 ? activeColor : idleColor;
        Dot4.Color = _comboProgress >= 4 ? activeColor : idleColor;
        Dot5.Color = _comboProgress >= 5 ? activeColor : idleColor;
    }

    private void UpdateBonusBar()
    {
        if (BonusTrack.Width <= 0)
        {
            return;
        }

        BonusFill.WidthRequest = Math.Max(0, BonusTrack.Width * (_bonusRemaining / (double)StartingBonus));
    }

    private async Task EndGameAsync(bool clearedBoard)
    {
        if (_isGameOver)
        {
            return;
        }

        _isGameOver = true;
        _isBusy = true;
        StopBonusTimer();

        var isNewBest = _score > _bestScore;
        var bestScore = Math.Max(_bestScore, _score);

        int apexPoints = BrainScoreService.RecordGameScore(
            sourceId: "matcha_madness",
            skill: BrainSkill.ProblemSolving,
            rawScore: _score,
            expectedTopScore: 1400);

        _bestScore = bestScore;
        _isBusy = false;

        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Matcha Madness",
                score: _score,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new MatchaMadnessGamePage(),
                accentHex: "#F0A617"));
    }

    private async Task SpawnSparklesAsync(Border first, Border second)
    {
        await Task.WhenAll(
            AnimateTilePopAsync(first),
            AnimateTilePopAsync(second));
    }

    private static async Task AnimateTilePopAsync(Border tile)
    {
        await tile.ScaleTo(1.06, 90, Easing.CubicOut);
        await tile.ScaleTo(1.0, 120, Easing.CubicIn);
    }

    private async Task SpawnConfettiAsync()
    {
        if (ConfettiLayer.Width <= 0 || ConfettiLayer.Height <= 0)
        {
            return;
        }

        ConfettiLayer.Children.Clear();
        _confettiPieces.Clear();

        var colors = new[]
        {
            Color.FromArgb("#F0A617"),
            Color.FromArgb("#8C5BFF"),
            Color.FromArgb("#4CD35E"),
            Color.FromArgb("#FF7390"),
            Color.FromArgb("#54A8FF")
        };

        for (var i = 0; i < 22; i++)
        {
            var piece = new BoxView
            {
                WidthRequest = _random.Next(6, 12),
                HeightRequest = _random.Next(12, 22),
                Color = colors[_random.Next(colors.Length)],
                Rotation = _random.Next(0, 360),
                Opacity = 0.95
            };

            var x = _random.NextDouble() * Math.Max(20, ConfettiLayer.Width - 20);
            var y = -_random.Next(40, 260);
            piece.TranslationX = x;
            piece.TranslationY = y;
            ConfettiLayer.Children.Add(piece);
            _confettiPieces.Add(piece);
        }

        await Task.WhenAll(_confettiPieces.Select(piece =>
            Task.WhenAll(
                piece.TranslateTo(piece.TranslationX + _random.Next(-30, 31), ConfettiLayer.Height + _random.Next(20, 140), 1200, Easing.CubicIn),
                piece.RotateTo(piece.Rotation + _random.Next(80, 220), 1200, Easing.Linear),
                piece.FadeTo(0.1, 1200, Easing.CubicIn))));
    }

    private static bool IsSameTopLayer(MatchaLayerDescriptor first, MatchaLayerDescriptor second)
    {
        return first.Kind == second.Kind
               && string.Equals(first.ColorKey, second.ColorKey, StringComparison.Ordinal)
               && Math.Abs(first.Scale - second.Scale) < 0.0001;
    }

    private static void ApplySelectedState(Border border)
    {
        border.Stroke = Colors.White;
        border.StrokeThickness = 4;
    }

    private static void ApplyMismatchState(Border border)
    {
        border.Stroke = Color.FromArgb("#FF7B8B");
        border.StrokeThickness = 4;
    }

    private static void ResetTileState(Border border)
    {
        border.StrokeThickness = 0;
        border.Stroke = Colors.Transparent;
        border.TranslationX = 0;
        border.Scale = 1;
    }

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isGameOver || _isBusy)
        {
            return;
        }

        _isPaused = true;

        var action = await GamePauseService.ShowAsync(
            this,
            "Matcha Madness",
            "Study the grid, remember the pattern, and rebuild each board before time runs out.");

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

    async void OnResumeClicked(object sender, EventArgs e)
    {
        await PauseOverlay.FadeTo(0, 120, Easing.CubicIn);
        PauseOverlay.IsVisible = false;
        _isPaused = false;
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new MatchaMadnessInsightsPage(_score, _boardBonusAwarded, _matchCount));
    }
}
