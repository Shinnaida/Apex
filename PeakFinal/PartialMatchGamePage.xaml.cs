using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Dispatching;

namespace Peak;

public partial class PartialMatchGamePage : ContentPage
{
    enum AnswerChoice
    {
        No,
        Partly,
        Yes
    }

    sealed class BoardShape
    {
        public HashSet<(int row, int col)> Cells { get; }
        public Color FillColor { get; }

        public BoardShape(IEnumerable<(int row, int col)> cells, Color fillColor)
        {
            Cells = new HashSet<(int row, int col)>(cells);
            FillColor = fillColor;
        }
    }

    const int BoardSize = 5;
    const int StartingTimeSeconds = 45;
    const int BasePoints = 20;
    const int StreakStep = 4;
    const int MaxMultiplier = 4;

    readonly Random _random = new();
    readonly Dictionary<(int row, int col), Border> _boardCells = new();
    readonly Dictionary<(int row, int col), BoxView> _miniCells = new();
    readonly List<(int row, int col)[]> _basePatterns;
    readonly Color[] _palette =
    {
        Color.FromArgb("#F169AE"),
        Color.FromArgb("#1CCAA8"),
        Color.FromArgb("#F1DA58"),
        Color.FromArgb("#69B9FF")
    };

    IDispatcherTimer? _timer;

    BoardShape? _previousShape;
    BoardShape? _currentShape;
    AnswerChoice _expectedChoice;

    int _score;
    int _timeLeft;
    int _streak;
    int _longestStreak;
    int _multiplier = 1;

    bool _inputEnabled;
    bool _isPaused;
    bool _isGameOver;
    bool _started;
    bool _transitioning;

    public PartialMatchGamePage()
    {
        InitializeComponent();
        BuildBoardGrid();
        BuildMiniGrid();

        _basePatterns = new List<(int row, int col)[]>
        {
            // Plus
            new[] { (0,1), (1,0), (1,1), (1,2), (2,1) },
            // U
            new[] { (0,0), (0,1), (0,2), (1,0), (1,2) },
            // L
            new[] { (0,0), (1,0), (2,0), (2,1), (2,2) },
            // J
            new[] { (0,2), (1,2), (2,0), (2,1), (2,2) },
            // Hook
            new[] { (0,0), (1,0), (2,0), (1,1), (1,2) },
            // Open corner
            new[] { (0,0), (0,1), (1,0), (2,0), (2,1) },
            // Step
            new[] { (0,0), (0,1), (1,1), (2,1), (2,2) },
            // Wide T
            new[] { (0,1), (1,0), (1,1), (1,2), (2,2) }
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("partial_match");

        if (_started)
            return;

        _started = true;
        _ = StartGameAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
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
        _timeLeft = StartingTimeSeconds;
        _streak = 0;
        _longestStreak = 0;
        _multiplier = 1;
        _previousShape = null;
        _currentShape = null;

        UpdateHud();
        RenderShape(null);
        RenderMiniShape(null);
        SetButtonsEnabled(false);

        await RunCountdownAsync();

        _previousShape = GenerateRandomShape();
        RenderShape(_previousShape);
        await Task.Delay(950);

        StartTimer();
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
            await Task.Delay(260);
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
            return;

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _timer = null;
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

    async Task ShowNextQuestionAsync()
    {
        if (_isGameOver || _isPaused || _previousShape is null)
            return;

        _expectedChoice = PickNextExpectedChoice();
        _currentShape = GenerateShapeForChoice(_previousShape, _expectedChoice);

        RenderShape(_currentShape);
        _inputEnabled = true;
        SetButtonsEnabled(true);
        await Task.CompletedTask;
    }

    async Task HandleAnswerAsync(AnswerChoice choice)
    {
        if (!_inputEnabled || _isPaused || _isGameOver || _transitioning)
            return;

        if (_currentShape is null || _previousShape is null)
            return;

        _inputEnabled = false;
        SetButtonsEnabled(false);
        _transitioning = true;

        bool correct = choice == _expectedChoice;
        int gained = 0;

        if (correct)
        {
            _streak++;
            _longestStreak = Math.Max(_longestStreak, _streak);
            _multiplier = Math.Min(MaxMultiplier, 1 + ((_streak - 1) / StreakStep));
            gained = BasePoints * _multiplier;
            _score += gained;
        }
        else
        {
            _streak = 0;
            _multiplier = 1;
        }

        UpdateHud();
        await ShowFeedbackAsync(correct, gained, _currentShape);

        if (_timeLeft <= 0)
        {
            await EndGameAsync();
            return;
        }

        _previousShape = _currentShape;
        _transitioning = false;
        await ShowNextQuestionAsync();
    }

    async Task ShowFeedbackAsync(bool correct, int gained, BoardShape shape)
    {
        RenderShape(null);
        RenderMiniShape(shape);

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

        await Task.Delay(280);
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
        StopTimer();

        int best = BrainScoreService.GetGamePerformance("partial_match")?.BestScore ?? 0;
        bool isNewBest = _score > best;

        int apexPoints = BrainScoreService.RecordGameScore(
            sourceId: "partial_match",
            skill: BrainSkill.Focus,
            rawScore: _score,
            expectedTopScore: 1800);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Partial Match",
                score: _score,
                bestScore: Math.Max(best, _score),
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new PartialMatchGamePage(),
                accentHex: "#6C8BFF"));
    }

    void UpdateHud()
    {
        ScoreLabel.Text = _score.ToString();
        MultiplierLabel.Text = $"x{_multiplier}";
        TimerLabel.Text = $"00:{_timeLeft:00}";

        int filled = _streak == 0 ? 0 : ((_streak - 1) % StreakStep) + 1;
        Color on = Color.FromArgb("#F0D34A");
        Color off = Color.FromArgb("#6D7692");

        Dot1.Color = filled >= 1 ? on : off;
        Dot2.Color = filled >= 2 ? on : off;
        Dot3.Color = filled >= 3 ? on : off;
        Dot4.Color = filled >= 4 ? on : off;
    }

    static AnswerChoice CompareShapes(BoardShape previous, BoardShape current)
    {
        if (previous.Cells.SetEquals(current.Cells))
            return AnswerChoice.Yes;

        bool hasOverlap = previous.Cells.Overlaps(current.Cells);
        return hasOverlap ? AnswerChoice.Partly : AnswerChoice.No;
    }

    AnswerChoice PickNextExpectedChoice()
    {
        int roll = _random.Next(100);
        if (roll < 30)
            return AnswerChoice.Yes;
        if (roll < 65)
            return AnswerChoice.Partly;
        return AnswerChoice.No;
    }

    BoardShape GenerateShapeForChoice(BoardShape previous, AnswerChoice target)
    {
        if (target == AnswerChoice.Yes)
            return new BoardShape(previous.Cells, PickDifferentColor(previous.FillColor));

        for (int i = 0; i < 220; i++)
        {
            var candidate = GenerateRandomShape();
            if (CompareShapes(previous, candidate) == target)
                return candidate;
        }

        // Robust fallback.
        return GenerateRandomShape();
    }

    BoardShape GenerateRandomShape()
    {
        var pattern = _basePatterns[_random.Next(_basePatterns.Count)];
        var transformed = TransformPattern(pattern, _random.Next(4), _random.NextDouble() < 0.35);

        int minRow = transformed.Min(p => p.row);
        int maxRow = transformed.Max(p => p.row);
        int minCol = transformed.Min(p => p.col);
        int maxCol = transformed.Max(p => p.col);

        int height = maxRow - minRow + 1;
        int width = maxCol - minCol + 1;

        int centerRow = (BoardSize - height) / 2;
        int centerCol = (BoardSize - width) / 2;

        int rowOffset = Math.Clamp(centerRow + _random.Next(-1, 2), 0, BoardSize - height);
        int colOffset = Math.Clamp(centerCol + _random.Next(-1, 2), 0, BoardSize - width);

        var cells = transformed.Select(p => (p.row - minRow + rowOffset, p.col - minCol + colOffset));
        return new BoardShape(cells, _palette[_random.Next(_palette.Length)]);
    }

    static List<(int row, int col)> TransformPattern((int row, int col)[] pattern, int rotations, bool flip)
    {
        var points = pattern.Select(p => (p.row, p.col)).ToList();

        if (flip)
            points = points.Select(p => (p.row, -p.col)).ToList();

        for (int i = 0; i < rotations; i++)
            points = points.Select(p => (p.col, -p.row)).ToList();

        int minRow = points.Min(p => p.row);
        int minCol = points.Min(p => p.col);

        return points.Select(p => (p.row - minRow, p.col - minCol)).ToList();
    }

    Color PickDifferentColor(Color previous)
    {
        var options = _palette.Where(c => c != previous).ToArray();
        return options.Length == 0 ? previous : options[_random.Next(options.Length)];
    }

    void BuildBoardGrid()
    {
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();
        BoardGrid.Children.Clear();
        _boardCells.Clear();

        for (int i = 0; i < BoardSize; i++)
        {
            BoardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int row = 0; row < BoardSize; row++)
        {
            for (int col = 0; col < BoardSize; col++)
            {
                var cell = new Border
                {
                    BackgroundColor = Color.FromArgb("#061634"),
                    Stroke = Color.FromArgb("#113060"),
                    StrokeShape = new RoundRectangle { CornerRadius = 1.5f },
                    StrokeThickness = 1
                };

                BoardGrid.Children.Add(cell);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                _boardCells[(row, col)] = cell;
            }
        }
    }

    void BuildMiniGrid()
    {
        MiniShapeGrid.RowDefinitions.Clear();
        MiniShapeGrid.ColumnDefinitions.Clear();
        MiniShapeGrid.Children.Clear();
        _miniCells.Clear();

        for (int i = 0; i < BoardSize; i++)
        {
            MiniShapeGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            MiniShapeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int row = 0; row < BoardSize; row++)
        {
            for (int col = 0; col < BoardSize; col++)
            {
                var block = new BoxView
                {
                    Color = Colors.Transparent,
                    CornerRadius = 1.5f
                };

                MiniShapeGrid.Children.Add(block);
                Grid.SetRow(block, row);
                Grid.SetColumn(block, col);
                _miniCells[(row, col)] = block;
            }
        }
    }

    void RenderShape(BoardShape? shape)
    {
        foreach (var cell in _boardCells.Values)
        {
            cell.BackgroundColor = Color.FromArgb("#061634");
            cell.Stroke = Color.FromArgb("#113060");
        }

        if (shape is null)
            return;

        foreach (var pos in shape.Cells)
        {
            if (!_boardCells.TryGetValue(pos, out var cell))
                continue;

            cell.BackgroundColor = shape.FillColor;
            cell.Stroke = shape.FillColor.WithAlpha(0.9f);
        }
    }

    void RenderMiniShape(BoardShape? shape)
    {
        foreach (var cell in _miniCells.Values)
            cell.Color = Colors.Transparent;

        if (shape is null)
            return;

        foreach (var pos in shape.Cells)
        {
            if (_miniCells.TryGetValue(pos, out var cell))
                cell.Color = shape.FillColor;
        }
    }

    void SetButtonsEnabled(bool enabled)
    {
        NoButton.IsEnabled = enabled;
        PartlyButton.IsEnabled = enabled;
        YesButton.IsEnabled = enabled;
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
            "Partial Match",
            "Judge whether the second pattern matches none, part, or all of the first pattern.");

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
        _inputEnabled = true;
        SetButtonsEnabled(true);
    }

    void OnResumeClicked(object sender, EventArgs e)
    {
        if (_isGameOver)
            return;

        PauseOverlay.IsVisible = false;
        _isPaused = false;
        _inputEnabled = true;
        SetButtonsEnabled(true);
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        StopTimer();
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new PartialMatchInsightsPage(_score, _longestStreak));
        Navigation.RemovePage(this);
    }

    async void OnNoClicked(object sender, EventArgs e)
    {
        await HandleAnswerAsync(AnswerChoice.No);
    }

    async void OnPartlyClicked(object sender, EventArgs e)
    {
        await HandleAnswerAsync(AnswerChoice.Partly);
    }

    async void OnYesClicked(object sender, EventArgs e)
    {
        await HandleAnswerAsync(AnswerChoice.Yes);
    }
}

