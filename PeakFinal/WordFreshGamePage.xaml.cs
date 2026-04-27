using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System.IO;
using System.Linq;

namespace Peak;

public partial class WordFreshGamePage : ContentPage
{
    private readonly Random _random = new();

    private List<WordFreshRound> _rounds = new();
    private List<List<char>> _board = new();

    private readonly List<WordFreshTile> _selectedTiles = new();
    private readonly HashSet<string> _foundWords = new(StringComparer.OrdinalIgnoreCase);

    private WordFreshRound? _currentRound;
    private readonly WordFreshBoardDrawable _boardDrawable = new();

    private int _currentRoundIndex = 0;
    private int _score = 0;
    private int _timeLeftSeconds = 90;

    private bool _dictionaryLoaded = false;
    private bool _timerStarted = false;
    private bool _isPaused = false;
    private bool _isAnimatingCollapse = false;

    private HashSet<string> _dictionary = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _prefixes = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Color AnswerBoxBaseColor = Color.FromArgb("#2B233D");
    private static readonly Color AnswerBoxInvalidPrefixColor = Color.FromArgb("#5A1F2B");
    private static readonly Color AnswerBoxErrorColor = Colors.Red;
    private static readonly Color AnswerBoxSuccessColor = Colors.Green;

    public WordFreshGamePage()
    {
        InitializeComponent();

        _boardDrawable = new WordFreshBoardDrawable();
        BoardView.Drawable = _boardDrawable;

        BoardView.StartInteraction += OnBoardStartInteraction;
        BoardView.DragInteraction += OnBoardDragInteraction;
        BoardView.EndInteraction += OnBoardEndInteraction;

        TimerLabel.Text = FormatTime(_timeLeftSeconds);
        ScoreLabel.Text = _score.ToString();
        AnswerBoxFrame.BackgroundColor = AnswerBoxBaseColor;
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("word_fresh");

        if (!_dictionaryLoaded)
        {
            LoadingOverlay.IsVisible = true;

            await Task.Run(async () =>
            {
                await LoadDictionaryAsync();
                SeedRounds();
            });

            LoadRound();

            _dictionaryLoaded = true;
            LoadingOverlay.IsVisible = false;
        }

        if (!_timerStarted)
        {
            StartTimer();
            _timerStarted = true;
        }
    }

    protected override void OnDisappearing()
    {
        _ = GameAudioService.StopGameAtmosphereAsync();
        base.OnDisappearing();
    }

    private void SeedRounds()
    {
        _rounds.Clear();

        for (int i = 0; i < 3; i++)
        {
            var boardRows = GenerateBoardWithWords(6, 5, 4);

            _rounds.Add(new WordFreshRound
            {
                Rows = boardRows
            });
        }

        _rounds = _rounds.OrderBy(x => _random.Next()).ToList();
    }

    private void LoadRound()
    {
        if (_rounds.Count == 0)
            return;

        if (_currentRoundIndex >= _rounds.Count)
            _currentRoundIndex = 0;

        _currentRound = _rounds[_currentRoundIndex];
        _foundWords.Clear();

        _board = _currentRound.Rows
            .Select(r => r.ToList())
            .ToList();

        ClearSelectionInternal();

        _boardDrawable.SetBoard(_board);
        _boardDrawable.SetSelection(new List<(int row, int col)>(), WordFreshSelectionState.None);
        _boardDrawable.SetFallOffsets(new Dictionary<(int row, int col), float>());

        BoardView.Invalidate();
    }

    private void OnBoardStartInteraction(object? sender, TouchEventArgs e)
    {
        if (_isPaused || _isAnimatingCollapse)
            return;

        if (e.Touches == null || !e.Touches.Any())
            return;

        StartNewDrag(e.Touches.First());
    }

    private void OnBoardDragInteraction(object? sender, TouchEventArgs e)
    {
        if (_isPaused || _isAnimatingCollapse)
            return;

        if (e.Touches == null || !e.Touches.Any())
            return;

        ExtendDrag(e.Touches.First());
    }

    private async void OnBoardEndInteraction(object? sender, TouchEventArgs e)
    {
        if (_isPaused || _isAnimatingCollapse)
            return;

        await EvaluateDraggedWordAsync();
    }

    private void StartNewDrag(PointF point)
    {
        if (_board.Count == 0)
            return;

        ClearSelectionInternal();

        var hit = _boardDrawable.HitTest(point);
        if (hit == null)
            return;

        int row = hit.Value.row;
        int col = hit.Value.col;

        char letter = _board[row][col];
        if (letter == '\0')
            return;

        _selectedTiles.Add(new WordFreshTile(row, col, letter));
        UpdateSelectionUI();
    }

    private void ExtendDrag(PointF point)
    {
        if (_board.Count == 0 || _selectedTiles.Count == 0)
            return;

        var hit = _boardDrawable.HitTest(point);
        if (hit == null)
            return;

        int row = hit.Value.row;
        int col = hit.Value.col;

        if (_selectedTiles.Any(t => t.Row == row && t.Col == col))
            return;

        char letter = _board[row][col];
        if (letter == '\0')
            return;

        var last = _selectedTiles.Last();
        var next = new WordFreshTile(row, col, letter);

        if (!AreAdjacent(last, next))
            return;

        _selectedTiles.Add(next);
        UpdateSelectionUI();
    }

    private void UpdateSelectionUI()
    {
        string word = new string(_selectedTiles.Select(t => t.Letter).ToArray());

        CurrentAnswerLabel.Text = word;

        _boardDrawable.SetSelection(
            _selectedTiles.Select(t => (t.Row, t.Col)).ToList(),
            WordFreshSelectionState.Selected);

        UpdateAnswerBoxPreview(word);
        BoardView.Invalidate();
    }

    private void UpdateAnswerBoxPreview(string currentWord)
    {
        if (string.IsNullOrWhiteSpace(currentWord))
        {
            AnswerBoxFrame.BackgroundColor = AnswerBoxBaseColor;
            return;
        }

        bool isPossiblePrefix = _prefixes.Contains(currentWord.ToUpperInvariant());

        AnswerBoxFrame.BackgroundColor = isPossiblePrefix
            ? AnswerBoxBaseColor
            : AnswerBoxInvalidPrefixColor;
    }

    private async Task EvaluateDraggedWordAsync()
    {
        if (_selectedTiles.Count == 0)
            return;

        string word = new string(_selectedTiles.Select(t => t.Letter).ToArray()).ToUpperInvariant();

        if (word.Length < 3)
        {
            await FlashRedAsync();
            ClearSelectionInternal();
            BoardView.Invalidate();
            return;
        }

        if (_dictionary.Contains(word) && !_foundWords.Contains(word))
        {
            _foundWords.Add(word);

            _boardDrawable.SetSelection(
                _selectedTiles.Select(t => (t.Row, t.Col)).ToList(),
                WordFreshSelectionState.Success);

            BoardView.Invalidate();

            await FlashGreenAsync();

            _score += CalculateScore(word);
            ScoreLabel.Text = _score.ToString();

            await AnimateCollapseAndRefillAsync();

            ClearSelectionInternal();
        }
        else
        {
            _boardDrawable.SetSelection(
                _selectedTiles.Select(t => (t.Row, t.Col)).ToList(),
                WordFreshSelectionState.Error);

            BoardView.Invalidate();

            await FlashRedAsync();

            ClearSelectionInternal();
        }

        BoardView.Invalidate();
    }

    private int CalculateScore(string word)
    {
        return word.Length switch
        {
            3 => 100,
            4 => 150,
            5 => 250,
            6 => 400,
            _ => word.Length * 80
        };
    }

    private bool AreAdjacent(WordFreshTile a, WordFreshTile b)
    {
        int rowDiff = Math.Abs(a.Row - b.Row);
        int colDiff = Math.Abs(a.Col - b.Col);

        return rowDiff <= 1 && colDiff <= 1 && !(rowDiff == 0 && colDiff == 0);
    }

    private async Task FlashRedAsync()
    {
        AnswerBoxFrame.BackgroundColor = AnswerBoxErrorColor;
        await Task.Delay(150);
        AnswerBoxFrame.BackgroundColor = AnswerBoxBaseColor;
    }

    private async Task FlashGreenAsync()
    {
        AnswerBoxFrame.BackgroundColor = AnswerBoxSuccessColor;
        await Task.Delay(150);
        AnswerBoxFrame.BackgroundColor = AnswerBoxBaseColor;
    }

    private void ClearSelectionInternal()
    {
        _selectedTiles.Clear();
        CurrentAnswerLabel.Text = "";
        AnswerBoxFrame.BackgroundColor = AnswerBoxBaseColor;

        _boardDrawable.SetSelection(new List<(int row, int col)>(), WordFreshSelectionState.None);
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        if (_isPaused)
            return;

        ClearSelectionInternal();
        BoardView.Invalidate();
    }

    private void OnSkipClicked(object sender, EventArgs e)
    {
        if (_isPaused)
            return;

        _currentRoundIndex++;
        LoadRound();
    }

    private void OnPauseClicked(object sender, TappedEventArgs e)
    {
        OnPauseClicked(sender, (EventArgs)e);
    }

    private async void OnPauseClicked(object sender, EventArgs e)
    {
        if (_isPaused)
            return;

        _isPaused = true;
        var action = await GamePauseService.ShowAsync(
            this,
            "Word Fresh",
            "Swipe through connected letters to make words and score before time expires.");

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

    private void StartTimer()
    {
        TimerLabel.Text = FormatTime(_timeLeftSeconds);

        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (_isPaused)
                return true;

            _timeLeftSeconds--;
            TimerLabel.Text = FormatTime(_timeLeftSeconds);

            if (_timeLeftSeconds <= 0)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var previousBest = BrainScoreService.GetGamePerformance("word_fresh")?.BestScore ?? 0;
                    var bestScore = Math.Max(previousBest, _score);
                    var isNewBest = _score > previousBest;

                    var apexPoints = BrainScoreService.RecordGameScore(
                        sourceId: "word_fresh",
                        skill: BrainSkill.Language,
                        rawScore: _score,
                        expectedTopScore: 2200);

                    await PageTransitionService.PushAsync(
                        Navigation,
                        () => new GenericGameSummaryPage(
                            gameTitle: "Word Fresh",
                            score: _score,
                            bestScore: bestScore,
                            apexPoints: apexPoints,
                            isNewBest: isNewBest,
                            playAgainFactory: () => new WordFreshGamePage(),
                            accentHex: "#89B8FF"));
                });

                return false;
            }

            return true;
        });
    }

    private string FormatTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    private async Task LoadDictionaryAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("wordfresh_dictionary.txt");
        using var reader = new StreamReader(stream);

        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            string word = line.Trim().ToUpperInvariant();

            if (word.Length < 3 || word.Length > 8)
                continue;

            if (!word.All(char.IsLetter))
                continue;

            words.Add(word);

            for (int i = 1; i <= word.Length; i++)
            {
                prefixes.Add(word.Substring(0, i));
            }
        }

        _dictionary = words;
        _prefixes = prefixes;
    }

    private async Task AnimateCollapseAndRefillAsync()
    {
        if (_board.Count == 0)
            return;

        _isAnimatingCollapse = true;

        // Stage 1: Fade out the matched tiles slowly
        const int fadeFrames = 10;
        for (int frame = 0; frame < fadeFrames; frame++)
        {
            float progress = (frame + 1f) / fadeFrames;
            float currentAlpha = 1f - progress;

            var opacities = new Dictionary<(int row, int col), float>();
            foreach (var tile in _selectedTiles)
            {
                opacities[(tile.Row, tile.Col)] = currentAlpha;
            }

            _boardDrawable.SetFadeOpacities(opacities);
            BoardView.Invalidate();
            await Task.Delay(20);
        }

        // Empty the selected tiles from board array
        foreach (var tile in _selectedTiles)
        {
            _board[tile.Row][tile.Col] = '\0';
        }

        // Refill empty spots in-place
        RefillBoard();
        _boardDrawable.SetBoard(_board);

        // Stage 2: Fade the new tiles back in
        for (int frame = 0; frame < fadeFrames; frame++)
        {
            float progress = (frame + 1f) / fadeFrames;
            float currentAlpha = progress;

            var opacities = new Dictionary<(int row, int col), float>();
            foreach (var tile in _selectedTiles)
            {
                opacities[(tile.Row, tile.Col)] = currentAlpha;
            }

            _boardDrawable.SetFadeOpacities(opacities);
            BoardView.Invalidate();
            await Task.Delay(20);
        }

        // Reset opacities and offsets to clean state
        _boardDrawable.SetFadeOpacities(new Dictionary<(int row, int col), float>());
        _boardDrawable.SetFallOffsets(new Dictionary<(int row, int col), float>());
        BoardView.Invalidate();

        _isAnimatingCollapse = false;
    }

    private void CollapseBoard()
    {
        if (_board.Count == 0)
            return;

        int rows = _board.Count;
        int cols = _board[0].Count;

        for (int col = 0; col < cols; col++)
        {
            List<char> remaining = new();

            for (int row = rows - 1; row >= 0; row--)
            {
                if (_board[row][col] != '\0')
                    remaining.Add(_board[row][col]);
            }

            int writeRow = rows - 1;

            foreach (char ch in remaining)
            {
                _board[writeRow][col] = ch;
                writeRow--;
            }

            while (writeRow >= 0)
            {
                _board[writeRow][col] = '\0';
                writeRow--;
            }
        }
    }

    private void RefillBoard()
    {
        for (int row = 0; row < _board.Count; row++)
        {
            for (int col = 0; col < _board[row].Count; col++)
            {
                if (_board[row][col] == '\0')
                    _board[row][col] = GetRandomLetter();
            }
        }
    }

    private char GetRandomLetter()
    {
        const string letters =
            "EEEEEEEEEEEEAAAAAAAIIIIIIIOOOOOONNNNNNRRRRRRTTTTTLLLLSSSSUDGBCMPFHVWYJKXQZ";

        return letters[_random.Next(letters.Length)];
    }

    private List<string> GenerateBoardWithWords(int rows, int cols, int wordsToPlace)
    {
        char[,] grid = new char[rows, cols];

        var candidateWords = _dictionary
            .Where(w => w.Length >= 3 && w.Length <= 6)
            .OrderBy(_ => _random.Next())
            .Take(200)
            .ToList();

        int placed = 0;

        foreach (var word in candidateWords)
        {
            if (placed >= wordsToPlace)
                break;

            if (TryPlaceWord(grid, rows, cols, word))
                placed++;
        }

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (grid[r, c] == '\0')
                    grid[r, c] = GetRandomLetter();
            }
        }

        var result = new List<string>();

        for (int r = 0; r < rows; r++)
        {
            char[] row = new char[cols];

            for (int c = 0; c < cols; c++)
                row[c] = grid[r, c];

            result.Add(new string(row));
        }

        return result;
    }

    private bool TryPlaceWord(char[,] grid, int rows, int cols, string word)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            int startRow = _random.Next(rows);
            int startCol = _random.Next(cols);

            var path = new List<(int row, int col)>
            {
                (startRow, startCol)
            };

            if (TryBuildPath(grid, rows, cols, word, 0, path, new HashSet<(int row, int col)>()))
            {
                for (int i = 0; i < word.Length; i++)
                {
                    var cell = path[i];
                    grid[cell.row, cell.col] = word[i];
                }

                return true;
            }
        }

        return false;
    }

    private bool TryBuildPath(
        char[,] grid,
        int rows,
        int cols,
        string word,
        int index,
        List<(int row, int col)> path,
        HashSet<(int row, int col)> used)
    {
        var current = path[index];
        used.Add(current);

        if (index == word.Length - 1)
            return true;

        var neighbors = GetNeighbors(current.row, current.col, rows, cols)
            .Where(n => !used.Contains(n))
            .OrderBy(_ => _random.Next())
            .ToList();

        foreach (var next in neighbors)
        {
            char existing = grid[next.row, next.col];
            char needed = word[index + 1];

            if (existing != '\0' && existing != needed)
                continue;

            path.Add(next);

            if (TryBuildPath(grid, rows, cols, word, index + 1, path, used))
                return true;

            path.RemoveAt(path.Count - 1);
        }

        used.Remove(current);
        return false;
    }

    private List<(int row, int col)> GetNeighbors(int row, int col, int rows, int cols)
    {
        var result = new List<(int row, int col)>();

        for (int dr = -1; dr <= 1; dr++)
        {
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0)
                    continue;

                int nr = row + dr;
                int nc = col + dc;

                if (nr >= 0 && nr < rows && nc >= 0 && nc < cols)
                    result.Add((nr, nc));
            }
        }

        return result;
    }
}

