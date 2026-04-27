using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class MovingMathGamePage : ContentPage
{
    enum SlotKind { Number, Operator }
    enum MathOp { Add, Subtract, Multiply, Divide }
    enum SlotPos { Left, Operator, Right }

    sealed record Difficulty(int Stage, int MaxOperand, bool AllowMultiply, bool AllowDivide, double SpawnSeconds, double TravelSeconds, int BasePoints);
    sealed record Equation(MathOp Op, int Left, int Right, int Result, SlotKind Kind, SlotPos Pos, string Answer);

    sealed class RowState
    {
        public required Grid View { get; init; }
        public required Border LeftToken { get; init; }
        public required Border OpToken { get; init; }
        public required Border RightToken { get; init; }
        public required Border ResultToken { get; init; }
        public required Border MissingToken { get; init; }
        public required Label MissingLabel { get; init; }
        public required Label EqualLabel { get; init; }
        public required SlotKind Kind { get; init; }
        public required string Answer { get; init; }
        public required string Hint { get; init; }
        public required DateTime SpawnedUtc { get; init; }
        public required int Stage { get; init; }
        public double Speed { get; set; }
        public double Y { get; set; }
        public bool IsResolved { get; set; }
    }

    static readonly Difficulty[] Difficulties =
    {
        new(0, 10, false, false, 2.6, 17.0, 100),
        new(1, 13, true, false, 2.2, 14.0, 120),
        new(2, 15, true, true, 1.85, 11.5, 145),
        new(3, 16, true, true, 1.55, 9.2, 170)
    };

    const int StartingTimeSeconds = 90;
    const double DefaultArenaHeight = 540;
    const double RowHeight = 86;
    const double ArenaHorizontalInset = 12;
    const double TopLaneInset = 18;
    const double BottomLaneInset = 18;
    const double InitialRowOffset = 146;
    const string PendingSquareColor = "#8995C7";
    const string PendingOperatorColor = "#A9B7E2";
    const string BlankColor = "#173B31";
    const string CorrectColor = "#59DD66";
    const string WrongColor = "#D91461";
    const string SelectedStroke = "#F9C84E";

    readonly Random _random = new();
    readonly List<RowState> _rows = new();
    readonly List<BoxView> _confettiPieces = new();
    IDispatcherTimer? _gameTimer;
    IDispatcherTimer? _spawnTimer;
    IDispatcherTimer? _animationTimer;
    DateTime _lastAnimationTickUtc;
    RowState? _selectedRow;
    int _timeLeft;
    int _score;
    int _bestScore;
    int _correctAnswers;
    int _wrongAnswers;
    int _currentStreak;
    int _longestStreak;
    bool _started;
    bool _isPaused;
    bool _isGameOver;

    public MovingMathGamePage()
    {
        InitializeComponent();
        EquationArena.SizeChanged += (_, _) => LayoutRows();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("moving_math");
        if (_started) return;
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
        await EnsureInitialRowsAsync();
    }

    void ResetState()
    {
        StopTimers();
        _timeLeft = StartingTimeSeconds;
        _score = 0;
        _correctAnswers = 0;
        _wrongAnswers = 0;
        _currentStreak = 0;
        _longestStreak = 0;
        _selectedRow = null;
        _isPaused = false;
        _isGameOver = false;
        _bestScore = BrainScoreService.GetGamePerformance("moving_math")?.BestScore ?? 0;
        EquationArena.Children.Clear();
        _rows.Clear();
        ConfettiLayer.Children.Clear();
        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        ScoreDeltaLabel.Opacity = 0;
        CountdownOverlay.IsVisible = true;
        UpdateHud();
        UpdateHintLabel();
    }

    async Task RunCountdownAsync()
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
            await Task.Delay(240);
        }
        CountdownOverlay.IsVisible = false;
    }

    void StartTimers()
    {
        StopTimers();
        _gameTimer = Dispatcher.CreateTimer();
        _gameTimer.Interval = TimeSpan.FromSeconds(1);
        _gameTimer.Tick += OnGameTick;
        _gameTimer.Start();
        _spawnTimer = Dispatcher.CreateTimer();
        _spawnTimer.Interval = TimeSpan.FromSeconds(GetDifficulty().SpawnSeconds);
        _spawnTimer.Tick += OnSpawnTick;
        _spawnTimer.Start();
        _animationTimer = Dispatcher.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _animationTimer.Tick += OnAnimationTick;
        _lastAnimationTickUtc = DateTime.UtcNow;
        _animationTimer.Start();
    }

    void StopTimers()
    {
        if (_gameTimer is not null) { _gameTimer.Stop(); _gameTimer.Tick -= OnGameTick; _gameTimer = null; }
        if (_spawnTimer is not null) { _spawnTimer.Stop(); _spawnTimer.Tick -= OnSpawnTick; _spawnTimer = null; }
        if (_animationTimer is not null) { _animationTimer.Stop(); _animationTimer.Tick -= OnAnimationTick; _animationTimer = null; }
    }

    async Task EnsureInitialRowsAsync()
    {
        await WaitForArenaLayoutAsync();
        for (int index = 0; index < 3; index++) SpawnRow(index * InitialRowOffset);
    }

    async Task WaitForArenaLayoutAsync()
    {
        if (EquationArena.Width > 0 && EquationArena.Height > 0) return;
        for (int attempts = 0; attempts < 20; attempts++)
        {
            await Task.Delay(40);
            if (EquationArena.Width > 0 && EquationArena.Height > 0) return;
        }
    }

    void OnGameTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver) return;
        _timeLeft = Math.Max(0, _timeLeft - 1);
        UpdateHud();
        if (_timeLeft == 0) _ = EndGameAsync();
    }

    void OnSpawnTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver) return;
        SpawnRow();
        if (_spawnTimer is not null) _spawnTimer.Interval = TimeSpan.FromSeconds(GetDifficulty().SpawnSeconds);
    }

    void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver)
        {
            _lastAnimationTickUtc = DateTime.UtcNow;
            return;
        }

        DateTime now = DateTime.UtcNow;
        double deltaSeconds = Math.Clamp((now - _lastAnimationTickUtc).TotalSeconds, 0.001, 0.05);
        _lastAnimationTickUtc = now;
        double rowWidth = GetRowWidth();
        double left = GetRowLeft(rowWidth);
        double removeThreshold = -RowHeight - 20;

        for (int index = _rows.Count - 1; index >= 0; index--)
        {
            RowState row = _rows[index];
            row.Y -= row.Speed * deltaSeconds;
            AbsoluteLayout.SetLayoutBounds(row.View, new Rect(left, GetDisplayY(row.Y), rowWidth, RowHeight));
            if (row.Y > removeThreshold) continue;
            if (!row.IsResolved) { _wrongAnswers++; _currentStreak = 0; _ = FlashScoreDeltaAsync("MISS", Colors.White); }
            if (ReferenceEquals(_selectedRow, row)) _selectedRow = null;
            EquationArena.Children.Remove(row.View);
            _rows.RemoveAt(index);
        }

        UpdateHintLabel();
    }

    void SpawnRow(double offset = 0)
    {
        Difficulty difficulty = GetDifficulty();
        Equation equation = CreateEquation(difficulty);
        RowState row = BuildRow(equation, difficulty.Stage);
        double rowWidth = GetRowWidth();
        double left = GetRowLeft(rowWidth);
        row.Y = GetPlayableHeight() + offset;
        row.Speed = (GetPlayableHeight() + RowHeight + 40) / difficulty.TravelSeconds;
        EquationArena.Children.Add(row.View);
        AbsoluteLayout.SetLayoutFlags(row.View, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(row.View, new Rect(left, GetDisplayY(row.Y), rowWidth, RowHeight));
        _rows.Add(row);
        UpdateHintLabel();
    }

    RowState BuildRow(Equation equation, int stage)
    {
        (Border leftToken, Label leftLabel) = CreateSquareToken(equation.Pos == SlotPos.Left ? "\u25AA" : equation.Left.ToString(), equation.Pos == SlotPos.Left);
        (Border opToken, Label opLabel) = CreateOperatorToken(equation.Pos == SlotPos.Operator ? "\u2022" : ToDisplayOperator(equation.Op), equation.Pos == SlotPos.Operator);
        (Border rightToken, Label rightLabel) = CreateSquareToken(equation.Pos == SlotPos.Right ? "\u25AA" : equation.Right.ToString(), equation.Pos == SlotPos.Right);
        (Border resultToken, _) = CreateSquareToken(equation.Result.ToString(), false);
        Border missingToken = equation.Pos switch { SlotPos.Left => leftToken, SlotPos.Operator => opToken, _ => rightToken };
        Label missingLabel = equation.Pos switch { SlotPos.Left => leftLabel, SlotPos.Operator => opLabel, _ => rightLabel };
        Label equalLabel = new() { Text = "=", FontSize = 36, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#ECF8EF"), VerticalTextAlignment = TextAlignment.Center, HorizontalTextAlignment = TextAlignment.Center, WidthRequest = 36 };
        var stack = new HorizontalStackLayout { Spacing = 16, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { leftToken, opToken, rightToken, equalLabel, resultToken } };
        var root = new Grid { WidthRequest = 330, HeightRequest = RowHeight, Children = { stack } };
        var row = new RowState
        {
            View = root,
            LeftToken = leftToken,
            OpToken = opToken,
            RightToken = rightToken,
            ResultToken = resultToken,
            MissingToken = missingToken,
            MissingLabel = missingLabel,
            EqualLabel = equalLabel,
            Kind = equation.Kind,
            Answer = equation.Answer,
            Hint = BuildHint(equation),
            SpawnedUtc = DateTime.UtcNow,
            Stage = stage
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => SelectRow(row);
        missingToken.GestureRecognizers.Add(tap);
        return row;
    }

    static (Border Border, Label Label) CreateSquareToken(string text, bool isBlank)
    {
        var label = new Label { Text = text, FontSize = 30, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
        var border = new Border { WidthRequest = 60, HeightRequest = 60, BackgroundColor = Color.FromArgb(isBlank ? BlankColor : PendingSquareColor), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Padding = 0, Content = label };
        return (border, label);
    }

    static (Border Border, Label Label) CreateOperatorToken(string text, bool isBlank)
    {
        var label = new Label { Text = text, FontSize = 30, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = isBlank ? Color.FromArgb("#F2C449") : Colors.White };
        var border = new Border { WidthRequest = 60, HeightRequest = 60, BackgroundColor = Color.FromArgb(isBlank ? BlankColor : PendingOperatorColor), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 30 }, Padding = 0, Content = label };
        return (border, label);
    }

    Equation CreateEquation(Difficulty difficulty)
    {
        while (true)
        {
            bool operatorBlank = _random.NextDouble() < 0.42;
            if (operatorBlank && TryCreateOperatorBlank(difficulty, out Equation? opEq)) return opEq!;
            if (!operatorBlank && TryCreateNumberBlank(difficulty, out Equation? numEq)) return numEq!;
        }
    }

    bool TryCreateOperatorBlank(Difficulty difficulty, out Equation? equation)
    {
        var ops = new List<MathOp> { MathOp.Add, MathOp.Subtract };
        if (difficulty.AllowMultiply) ops.Add(MathOp.Multiply);
        if (difficulty.AllowDivide) ops.Add(MathOp.Divide);
        MathOp op = ops[_random.Next(ops.Count)];
        switch (op)
        {
            case MathOp.Add:
            {
                int left = _random.Next(2, difficulty.MaxOperand + 1), right = _random.Next(2, difficulty.MaxOperand + 1), result = left + right;
                if (result <= 18) { equation = new Equation(op, left, right, result, SlotKind.Operator, SlotPos.Operator, "+"); return true; }
                break;
            }
            case MathOp.Subtract:
            {
                int right = _random.Next(2, Math.Min(9, difficulty.MaxOperand) + 1), result = _random.Next(2, difficulty.MaxOperand - 1), left = right + result;
                if (left <= difficulty.MaxOperand + 4) { equation = new Equation(op, left, right, result, SlotKind.Operator, SlotPos.Operator, "-"); return true; }
                break;
            }
            case MathOp.Multiply:
            {
                int left = _random.Next(2, 6), right = _random.Next(2, 6), result = left * right;
                if (result <= 18) { equation = new Equation(op, left, right, result, SlotKind.Operator, SlotPos.Operator, "*"); return true; }
                break;
            }
            case MathOp.Divide:
            {
                int right = _random.Next(2, 6), result = _random.Next(2, 10), left = right * result;
                if (left <= difficulty.MaxOperand + 4) { equation = new Equation(op, left, right, result, SlotKind.Operator, SlotPos.Operator, "/"); return true; }
                break;
            }
        }
        equation = null;
        return false;
    }

    bool TryCreateNumberBlank(Difficulty difficulty, out Equation? equation)
    {
        var ops = new List<MathOp> { MathOp.Add, MathOp.Subtract };
        if (difficulty.AllowMultiply) ops.Add(MathOp.Multiply);
        if (difficulty.AllowDivide) ops.Add(MathOp.Divide);
        MathOp op = ops[_random.Next(ops.Count)];
        SlotPos pos = _random.NextDouble() < 0.5 ? SlotPos.Left : SlotPos.Right;
        int answer = _random.Next(2, 10);
        switch (op)
        {
            case MathOp.Add:
            {
                int visible = _random.Next(2, difficulty.MaxOperand + 1), result = answer + visible;
                if (result <= 18) { equation = new Equation(op, pos == SlotPos.Left ? answer : visible, pos == SlotPos.Left ? visible : answer, result, SlotKind.Number, pos, answer.ToString()); return true; }
                break;
            }
            case MathOp.Subtract:
            {
                int left, right, result;
                if (pos == SlotPos.Right) { right = answer; result = _random.Next(2, 10); left = result + right; }
                else { right = _random.Next(2, 9); left = answer; result = left - right; }
                if (left >= 2 && result >= 2 && left <= difficulty.MaxOperand + 4 && result <= 18) { equation = new Equation(op, left, right, result, SlotKind.Number, pos, answer.ToString()); return true; }
                break;
            }
            case MathOp.Multiply:
            {
                int visible = _random.Next(2, 6), result = answer * visible;
                if (result <= 18) { equation = new Equation(op, pos == SlotPos.Left ? answer : visible, pos == SlotPos.Left ? visible : answer, result, SlotKind.Number, pos, answer.ToString()); return true; }
                break;
            }
            case MathOp.Divide:
            {
                if (pos == SlotPos.Right)
                {
                    int result = _random.Next(2, 10), left = answer * result;
                    if (left <= difficulty.MaxOperand + 6) { equation = new Equation(op, left, answer, result, SlotKind.Number, pos, answer.ToString()); return true; }
                }
                else
                {
                    int divisor = _random.Next(2, 6);
                    if (answer % divisor == 0)
                    {
                        int result = answer / divisor;
                        if (result >= 2) { equation = new Equation(op, answer, divisor, result, SlotKind.Number, pos, answer.ToString()); return true; }
                    }
                }
                break;
            }
        }
        equation = null;
        return false;
    }

    async Task HandleInputAsync(string value, SlotKind kind)
    {
        if (_isPaused || _isGameOver) return;
        RowState? target = FindTargetRow(kind);
        if (target is null) { await FlashScoreDeltaAsync("WAIT", Color.FromArgb("#F5C24A")); return; }
        SelectRow(target);
        target.IsResolved = true;
        target.MissingLabel.TextColor = Colors.White;
        target.MissingLabel.Text = kind == SlotKind.Operator ? ToDisplayOperator(value) : value;
        await target.MissingToken.ScaleTo(1.08, 80, Easing.CubicOut);
        await target.MissingToken.ScaleTo(1.0, 90, Easing.CubicIn);
        bool isCorrect = string.Equals(target.Answer, value, StringComparison.Ordinal);
        ApplyResolvedStyle(target, isCorrect);
        ClearSelection();
        if (isCorrect)
        {
            int gained = CalculateScore(target);
            _score += gained;
            _correctAnswers++;
            _currentStreak++;
            _longestStreak = Math.Max(_longestStreak, _currentStreak);
            UpdateHud();
            await FlashScoreDeltaAsync($"+{gained}", Color.FromArgb("#E5FFE8"));
            return;
        }
        _wrongAnswers++;
        _currentStreak = 0;
        UpdateHud();
        await FlashScoreDeltaAsync("MISS", Colors.White);
    }

    RowState? FindTargetRow(SlotKind kind)
        => _selectedRow is not null && !_selectedRow.IsResolved && _selectedRow.Kind == kind
            ? _selectedRow
            : _rows.Where(row => !row.IsResolved && row.Kind == kind).OrderByDescending(row => row.Y).FirstOrDefault();

    int CalculateScore(RowState row)
    {
        double responseSeconds = Math.Clamp((DateTime.UtcNow - row.SpawnedUtc).TotalSeconds, 0, 12);
        int speedBonus = responseSeconds < 1.5 ? 45 : responseSeconds < 3 ? 25 : responseSeconds < 4.5 ? 10 : 0;
        int streakBonus = Math.Min(40, (_currentStreak / 3) * 10);
        return Difficulties[row.Stage].BasePoints + speedBonus + streakBonus;
    }

    void ApplyResolvedStyle(RowState row, bool isCorrect)
    {
        Color resolved = Color.FromArgb(isCorrect ? CorrectColor : WrongColor);
        foreach (Border token in new[] { row.LeftToken, row.OpToken, row.RightToken, row.ResultToken })
        {
            token.BackgroundColor = resolved;
            token.StrokeThickness = 0;
        }
        row.EqualLabel.TextColor = Colors.White;
        row.MissingLabel.TextColor = Colors.White;
    }

    void SelectRow(RowState row)
    {
        if (row.IsResolved) return;
        if (_selectedRow is not null && !ReferenceEquals(_selectedRow, row)) ApplySelection(_selectedRow, false);
        _selectedRow = row;
        ApplySelection(row, true);
        UpdateHintLabel();
    }

    void ClearSelection()
    {
        if (_selectedRow is not null) ApplySelection(_selectedRow, false);
        _selectedRow = null;
        UpdateHintLabel();
    }

    static void ApplySelection(RowState row, bool selected)
    {
        if (row.IsResolved) { row.MissingToken.StrokeThickness = 0; row.MissingToken.Stroke = Colors.Transparent; return; }
        row.MissingToken.StrokeThickness = selected ? 3 : 0;
        row.MissingToken.Stroke = selected ? Color.FromArgb(SelectedStroke) : Colors.Transparent;
    }

    void LayoutRows()
    {
        if (_rows.Count == 0 || EquationArena.Width <= 0) return;
        double rowWidth = GetRowWidth();
        double left = GetRowLeft(rowWidth);
        foreach (RowState row in _rows) AbsoluteLayout.SetLayoutBounds(row.View, new Rect(left, GetDisplayY(row.Y), rowWidth, RowHeight));
    }

    void UpdateHud()
    {
        TimerLabel.Text = _timeLeft < 60 ? $"00:{_timeLeft:00}" : $"01:{_timeLeft - 60:00}";
        ScoreLabel.Text = _score.ToString();
    }

    void UpdateHintLabel()
    {
        if (_selectedRow is not null && !_selectedRow.IsResolved) { SelectionHintLabel.Text = $"Selected: {_selectedRow.Hint}"; return; }
        SelectionHintLabel.Text = _rows.Any(row => !row.IsResolved) ? "Tap a blank slot or press a key to solve the lowest matching equation." : "New equations are on the way.";
    }

    Difficulty GetDifficulty()
    {
        int elapsed = StartingTimeSeconds - _timeLeft;
        if (elapsed >= 72) return Difficulties[3];
        if (elapsed >= 50) return Difficulties[2];
        if (elapsed >= 24) return Difficulties[1];
        return Difficulties[0];
    }

    double GetPlayableHeight() => Math.Max(220, GetArenaHeight() - TopLaneInset - BottomLaneInset);
    double GetRowWidth() => Math.Max(248, GetArenaWidth() - (ArenaHorizontalInset * 2));
    double GetRowLeft(double rowWidth) => (GetArenaWidth() - rowWidth) / 2;
    static double GetDisplayY(double laneY) => TopLaneInset + laneY;

    double GetArenaWidth() => EquationArena.Width > 0 ? EquationArena.Width : 360;
    double GetArenaHeight() => EquationArena.Height > 0 ? EquationArena.Height : DefaultArenaHeight;

    static string BuildHint(Equation equation)
    {
        string left = equation.Pos == SlotPos.Left ? "?" : equation.Left.ToString();
        string op = equation.Pos == SlotPos.Operator ? "?" : ToDisplayOperator(equation.Op);
        string right = equation.Pos == SlotPos.Right ? "?" : equation.Right.ToString();
        return $"{left} {op} {right} = {equation.Result}";
    }

    static string ToDisplayOperator(MathOp op) => op switch
    {
        MathOp.Add => "+",
        MathOp.Subtract => "\u2212",
        MathOp.Multiply => "\u00D7",
        _ => "\u00F7"
    };

    static string ToDisplayOperator(string raw) => raw switch
    {
        "-" => "\u2212",
        "*" => "\u00D7",
        "/" => "\u00F7",
        _ => raw
    };

    async Task FlashScoreDeltaAsync(string text, Color textColor)
    {
        ScoreDeltaLabel.Text = text;
        ScoreDeltaLabel.TextColor = textColor;
        ScoreDeltaLabel.Opacity = 1;
        ScoreDeltaLabel.TranslationY = 0;
        await Task.WhenAll(ScoreDeltaLabel.FadeTo(0, 460, Easing.CubicOut), ScoreDeltaLabel.TranslateTo(0, -12, 460, Easing.CubicOut));
        ScoreDeltaLabel.TranslationY = 0;
    }

    async Task EndGameAsync()
    {
        if (_isGameOver) return;
        _isGameOver = true;
        StopTimers();
        ClearSelection();
        bool isNewBest = _score > _bestScore;
        int bestScore = Math.Max(_bestScore, _score);
        int apexPoints = BrainScoreService.RecordGameScore("moving_math", BrainSkill.ProblemSolving, _score, MovingMathProgress.ExpectedTopScore);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Moving Math",
                score: _score,
                bestScore: bestScore,
                apexPoints: apexPoints,
                isNewBest: isNewBest,
                playAgainFactory: () => new MovingMathGamePage(),
                accentHex: "#5592F2",
                secondaryLabel: "Rank",
                secondaryValue: MovingMathProgress.ResolveRank(bestScore)));
    }

    async Task SpawnConfettiAsync()
    {
        ConfettiLayer.Children.Clear();
        _confettiPieces.Clear();
        string[] colors = { "#60D66E", "#F5C84B", "#5592F2", "#E56F9C", "#7F6AE6", "#F07E43" };
        double width = Width > 0 ? Width : 360;
        double height = Height > 0 ? Height : 760;

        for (int index = 0; index < 24; index++)
        {
            var piece = new BoxView { Color = Color.FromArgb(colors[_random.Next(colors.Length)]), WidthRequest = _random.Next(6, 12), HeightRequest = _random.Next(8, 16), Rotation = _random.Next(0, 180) };
            double startX = _random.NextDouble() * width;
            double startY = -(_random.Next(20, 260));
            AbsoluteLayout.SetLayoutFlags(piece, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(piece, new Rect(startX, startY, piece.WidthRequest, piece.HeightRequest));
            ConfettiLayer.Children.Add(piece);
            _confettiPieces.Add(piece);
        }

        await Task.WhenAll(_confettiPieces.Select(async piece =>
        {
            double drop = height * (0.55 + _random.NextDouble() * 0.35);
            uint duration = (uint)(1500 + _random.Next(0, 450));
            await Task.WhenAll(piece.TranslateTo(0, drop, duration, Easing.CubicIn), piece.RotateTo(piece.Rotation + _random.Next(90, 260), 1500u, Easing.Linear));
        }));
    }

    async void OnNumberTapped(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string value) await HandleInputAsync(value, SlotKind.Number);
    }

    async void OnOperatorTapped(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string value) await HandleInputAsync(value, SlotKind.Operator);
    }

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isGameOver) return;
        _isPaused = true;

        var action = await GamePauseService.ShowAsync(
            this,
            "Moving Math",
            "Fill the missing number or operator before each equation row drifts away.");

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
        _lastAnimationTickUtc = DateTime.UtcNow;
    }

    void OnResumeClicked(object sender, EventArgs e)
    {
        PauseOverlay.IsVisible = false;
        _isPaused = false;
        _lastAnimationTickUtc = DateTime.UtcNow;
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new MovingMathInsightsPage(_score, _correctAnswers, _wrongAnswers, _longestStreak));
    }
}
