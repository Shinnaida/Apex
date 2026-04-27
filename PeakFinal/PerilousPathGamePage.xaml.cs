namespace Peak;

using Microsoft.Maui.Controls.Shapes;

public partial class PerilousPathGamePage : ContentPage
{
    const int TotalRounds = 12;
    const int BoardSize = 5;

    readonly Random _random = new();
    readonly Dictionary<(int row, int col), Border> _cells = new();
    readonly HashSet<(int row, int col)> _hazards = new();
    readonly HashSet<(int row, int col)> _visited = new();

    (int row, int col) _startCell;
    (int row, int col) _endCell;
    (int row, int col)? _currentCell;
    (int row, int col)? _targetCell;
    (int row, int col)? _lastWrongCell;

    int _round = 1;
    int _score;
    bool _inputEnabled;
    bool _isPaused;
    bool _roundTransitioning;
    bool _isDragging;
    (int row, int col) _dragStartCell;
    (int row, int col) _dragLastCell;
    double _cellSize = 64;

    public PerilousPathGamePage()
    {
        InitializeComponent();
        BuildBoard();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("perilous_path");

        if (_round == 1 && _score == 0 && !_roundTransitioning)
            await StartRoundAsync();
    }

    protected override void OnDisappearing()
    {
        _ = GameAudioService.StopGameAtmosphereAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    void BuildBoard()
    {
        BoardGrid.RowDefinitions.Clear();
        BoardGrid.ColumnDefinitions.Clear();
        BoardGrid.Children.Clear();
        _cells.Clear();

        for (int i = 0; i < BoardSize; i++)
        {
            BoardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            BoardGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (int row = 0; row < BoardSize; row++)
        {
            for (int col = 0; col < BoardSize; col++)
            {
                var label = new Label
                {
                    Text = string.Empty,
                    FontSize = 22,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };

                var cell = new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 2 },
                    StrokeThickness = 0,
                    BackgroundColor = Color.FromArgb("#4D6E90"),
                    Content = label
                };

                int tappedRow = row;
                int tappedCol = col;

                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await HandleCellSelectedAsync(tappedRow, tappedCol);
                cell.GestureRecognizers.Add(tap);

                var pan = new PanGestureRecognizer();
                pan.PanUpdated += async (_, e) => await OnCellPanUpdatedAsync(tappedRow, tappedCol, e);
                cell.GestureRecognizers.Add(pan);

                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                BoardGrid.Children.Add(cell);
                _cells[(row, col)] = cell;
            }
        }
    }

    async Task StartRoundAsync()
    {
        _inputEnabled = false;
        _roundTransitioning = true;
        _isDragging = false;
        _currentCell = null;
        _targetCell = null;
        _lastWrongCell = null;
        _visited.Clear();

        GenerateRoundPattern();
        RenderBoard(previewHazards: true, revealHazards: false, wrongCell: null);

        RoundLabel.Text = $"{_round}/{TotalRounds}";
        ScoreLabel.Text = $"Score {_score}";
        StatusLabel.Text = "Memorize the dangerous tiles.";

        await Task.Delay(1200);

        RenderBoard(previewHazards: false, revealHazards: false, wrongCell: null);
        StatusLabel.Text = "Start from either white circle and reach the other. Avoid hazards.";
        _inputEnabled = true;
        _roundTransitioning = false;
    }

    void GenerateRoundPattern()
    {
        _hazards.Clear();

        int hazardCount = GetHazardCountForRound(_round);

        for (int attempt = 0; attempt < 500; attempt++)
        {
            var start = (_random.Next(BoardSize), _random.Next(BoardSize));
            var end = (_random.Next(BoardSize), _random.Next(BoardSize));
            if (start == end)
                continue;

            int distance = Math.Abs(start.Item1 - end.Item1) + Math.Abs(start.Item2 - end.Item2);
            if (distance < 4)
                continue;

            var candidates = new List<(int row, int col)>();
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    var cell = (row, col);
                    if (cell != start && cell != end)
                        candidates.Add(cell);
                }
            }

            Shuffle(candidates);
            var hazards = new HashSet<(int row, int col)>();
            for (int i = 0; i < Math.Min(hazardCount, candidates.Count); i++)
                hazards.Add(candidates[i]);

            if (!HasSafePath(start, end, hazards))
                continue;

            _startCell = start;
            _endCell = end;
            foreach (var h in hazards)
                _hazards.Add(h);
            return;
        }

        // Fallback.
        _startCell = (4, 0);
        _endCell = (0, 4);
        _hazards.Clear();
        _hazards.Add((1, 1));
        _hazards.Add((2, 1));
        _hazards.Add((2, 2));
        _hazards.Add((3, 2));
    }

    bool HasSafePath((int row, int col) start, (int row, int col) end, HashSet<(int row, int col)> hazards)
    {
        var q = new Queue<(int row, int col)>();
        var seen = new HashSet<(int row, int col)>();
        q.Enqueue(start);
        seen.Add(start);

        while (q.Count > 0)
        {
            var current = q.Dequeue();
            if (current == end)
                return true;

            foreach (var next in GetNeighbors(current.row, current.col))
            {
                if (hazards.Contains(next) || seen.Contains(next))
                    continue;

                seen.Add(next);
                q.Enqueue(next);
            }
        }

        return false;
    }

    IEnumerable<(int row, int col)> GetNeighbors(int row, int col)
    {
        var offsets = new (int dr, int dc)[]
        {
            (-1, 0), (1, 0), (0, -1), (0, 1)
        };

        foreach (var (dr, dc) in offsets)
        {
            int nr = row + dr;
            int nc = col + dc;
            if (nr >= 0 && nr < BoardSize && nc >= 0 && nc < BoardSize)
                yield return (nr, nc);
        }
    }

    async Task OnCellPanUpdatedAsync(int row, int col, PanUpdatedEventArgs e)
    {
        if (_roundTransitioning)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                if (!_inputEnabled || _isPaused)
                    return;

                _isDragging = true;
                _dragStartCell = (row, col);
                _dragLastCell = (row, col);
                _cellSize = Math.Max(1, BoardGrid.Width > 0 ? BoardGrid.Width / BoardSize : 64);
                await HandleCellSelectedAsync(row, col);
                break;

            case GestureStatus.Running:
                if (!_isDragging || !_inputEnabled || _isPaused)
                    return;

                int rowOffset = e.TotalY >= 0
                    ? (int)Math.Floor(e.TotalY / _cellSize)
                    : (int)Math.Ceiling(e.TotalY / _cellSize);

                int colOffset = e.TotalX >= 0
                    ? (int)Math.Floor(e.TotalX / _cellSize)
                    : (int)Math.Ceiling(e.TotalX / _cellSize);

                int targetRow = Math.Clamp(_dragStartCell.row + rowOffset, 0, BoardSize - 1);
                int targetCol = Math.Clamp(_dragStartCell.col + colOffset, 0, BoardSize - 1);
                var target = (targetRow, targetCol);

                if (target == _dragLastCell)
                    return;

                foreach (var step in TraceCells(_dragLastCell, target))
                {
                    _dragLastCell = step;
                    await HandleCellSelectedAsync(step.row, step.col);
                    if (!_inputEnabled || _roundTransitioning)
                        break;
                }
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                _isDragging = false;
                break;
        }
    }

    IEnumerable<(int row, int col)> TraceCells((int row, int col) from, (int row, int col) to)
    {
        var current = from;
        while (current != to)
        {
            if (current.row != to.row)
            {
                current.row += Math.Sign(to.row - current.row);
                yield return current;
            }

            if (current.col != to.col)
            {
                current.col += Math.Sign(to.col - current.col);
                yield return current;
            }
        }
    }

    async Task HandleCellSelectedAsync(int row, int col)
    {
        if (!_inputEnabled || _roundTransitioning || _isPaused)
            return;

        var tapped = (row, col);

        if (_currentCell is null)
        {
            // Allow the player to start from either endpoint.
            if (tapped != _startCell && tapped != _endCell)
                return;

            _currentCell = tapped;
            _targetCell = tapped == _startCell ? _endCell : _startCell;
            _visited.Add(tapped);
            RenderBoard(previewHazards: false, revealHazards: false, wrongCell: null);
            return;
        }

        if (tapped == _currentCell.Value)
            return;

        // Ignore non-adjacent jitter/jumps instead of punishing valid drags.
        if (!AreAdjacent(_currentCell.Value, tapped))
            return;

        if (_hazards.Contains(tapped))
        {
            _lastWrongCell = tapped;
            await CompleteRoundAsync(success: false, pointsAwarded: 0);
            return;
        }

        _currentCell = tapped;
        _visited.Add(tapped);
        RenderBoard(previewHazards: false, revealHazards: false, wrongCell: null);

        if (_targetCell.HasValue && tapped == _targetCell.Value)
        {
            int points = CalculatePointsFromShadedTiles(_visited.Count);
            await CompleteRoundAsync(success: true, pointsAwarded: points);
        }
    }

    static bool AreAdjacent((int row, int col) a, (int row, int col) b)
    {
        int dr = Math.Abs(a.row - b.row);
        int dc = Math.Abs(a.col - b.col);
        return dr + dc == 1;
    }

    async Task CompleteRoundAsync(bool success, int pointsAwarded)
    {
        _inputEnabled = false;
        _roundTransitioning = true;
        _isDragging = false;

        if (success)
        {
            _score += pointsAwarded;
            ScoreLabel.Text = $"Score {_score}";
            StatusLabel.Text = $"+{pointsAwarded}";
            await ShowRoundBadgeAsync("\u2713", "#2DBE60");
        }
        else
        {
            RenderBoard(previewHazards: false, revealHazards: true, wrongCell: _lastWrongCell);
            StatusLabel.Text = "Wrong tile";
            await ShowRoundBadgeAsync("\u2715", "#D63B3B");
        }

        await Task.Delay(450);

        _round++;
        if (_round > TotalRounds)
        {
            await FinishGameAsync();
            return;
        }

        await StartRoundAsync();
    }

    async Task ShowRoundBadgeAsync(string symbol, string colorHex)
    {
        RoundResultLabel.Text = symbol;
        RoundResultBadge.BackgroundColor = Color.FromArgb(colorHex);
        RoundResultBadge.Opacity = 0;
        RoundResultBadge.Scale = 0.75;
        RoundResultBadge.IsVisible = true;

        await Task.WhenAll(
            RoundResultBadge.FadeTo(1, 150, Easing.CubicOut),
            RoundResultBadge.ScaleTo(1, 180, Easing.CubicOut));

        await Task.Delay(250);
        await RoundResultBadge.FadeTo(0, 150, Easing.CubicIn);
        RoundResultBadge.IsVisible = false;
    }

    async Task FinishGameAsync()
    {
        int previousBest = BrainScoreService.GetGamePerformance("perilous_path")?.BestScore ?? 0;
        bool isNewBest = _score > previousBest;
        int bestScore = Math.Max(previousBest, _score);

        var apexPoints = BrainScoreService.RecordGameScore(
            sourceId: "perilous_path",
            skill: BrainSkill.ProblemSolving,
            rawScore: _score,
            expectedTopScore: 4200);

        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Perilous Path",
                score: _score,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new PerilousPathGamePage(),
                accentHex: "#6FC7FF"));
    }

    void RenderBoard(bool previewHazards, bool revealHazards, (int row, int col)? wrongCell)
    {
        for (int row = 0; row < BoardSize; row++)
        {
            for (int col = 0; col < BoardSize; col++)
            {
                var key = (row, col);
                var cell = _cells[key];
                if (cell.Content is not Label label)
                    continue;

                label.Text = string.Empty;
                label.TextColor = Colors.White;
                cell.BackgroundColor = Color.FromArgb("#4D6E90");

                if (wrongCell.HasValue && key == wrongCell.Value)
                {
                    cell.BackgroundColor = Color.FromArgb("#C83455");
                    label.Text = "X";
                    continue;
                }

                if ((previewHazards || revealHazards) && _hazards.Contains(key))
                {
                    cell.BackgroundColor = Color.FromArgb("#D7356A");
                    label.Text = "\u2739";
                    continue;
                }

                if (_visited.Contains(key))
                {
                    cell.BackgroundColor = Color.FromArgb("#1FC7FF");
                    label.Text = "\u2022";
                }

                if (key == _startCell || key == _endCell)
                {
                    label.Text = "\u25CF";
                    label.TextColor = Colors.White;
                    if (_visited.Contains(key))
                        cell.BackgroundColor = Color.FromArgb("#1FC7FF");
                }
            }
        }
    }

    static int GetHazardCountForRound(int round)
    {
        if (round <= 4)
            return 4;
        if (round <= 8)
            return 5;
        if (round <= 11)
            return 6;
        return 7;
    }

    static int CalculatePointsFromShadedTiles(int shadedTileCount)
    {
        // Score depends directly on how many unique tiles were shaded by the player.
        // Examples: 5 tiles => 250, 6 => 360, 7 => 490.
        return shadedTileCount * shadedTileCount * 10;
    }

    void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isPaused)
        {
            return;
        }

        _isPaused = true;
        var action = await GamePauseService.ShowAsync(
            this,
            "Perilous Path",
            "Trace the safe route, remember the path, and make each move without hitting a trap.");

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
}

