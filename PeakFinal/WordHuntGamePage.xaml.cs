using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Peak;

public partial class WordHuntGamePage : ContentPage
{
    sealed record WordHuntPuzzle(string Theme, string[] Rows, string[] Words, int BonusScore);

    readonly List<WordHuntPuzzle> _puzzles = new()
    {
        new(
            "Wardrobe",
            new[]
            {
                "TSSERDS",
                "OJEHNOK",
                "OAOSCVN",
                "BCHKUUU",
                "EKSRAIR",
                "TESKIRT",
                "ATAHZGB"
            },
            new[] { "SUIT", "DRESS", "BOOT", "SOCKS", "SHOES", "SKIRT", "TRUNKS", "HAT", "BRA", "JACKET" },
            5000),
        new(
            "Body",
            new[]
            {
                "ESRAELS",
                "RDINLEY",
                "DKCABAT",
                "HCAMOTS",
                "LEGSWEE",
                "DNFACEO",
                "REGNIFT"
            },
            new[] { "TOES", "BACK", "LEGS", "NECK", "FINGER", "FACE", "FEET", "ELBOW", "EARS", "STOMACH" },
            5000)
    };

    readonly List<WordHuntCellViewModel> _selectedCells = new();
    readonly HashSet<string> _foundWords = new(StringComparer.OrdinalIgnoreCase);

    IDispatcherTimer? _timer;
    WordHuntPuzzle? _currentPuzzle;
    int _currentPuzzleIndex;
    int _score;
    int _timeLeftSeconds = 90;
    bool _started;
    bool _isPaused;
    bool _isGameOver;
    bool _isHandlingSelection;
    bool _isDragSelecting;

    public ObservableCollection<WordHuntCellViewModel> Cells { get; } = new();
    public ObservableCollection<WordHuntTargetWordViewModel> TargetWords { get; } = new();

    public WordHuntGamePage()
    {
        InitializeComponent();
        BindingContext = this;
        UpdateScore();
        UpdateTimer();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
        {
            return;
        }

        _started = true;
        LoadPuzzle();
        StartTimer();
        _ = GameAudioService.StartGameAtmosphereAsync("word_hunt");
    }

    protected override void OnDisappearing()
    {
        StopTimer();
        _ = GameAudioService.StopGameAtmosphereAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => true;

    void LoadPuzzle()
    {
        _currentPuzzle = _puzzles[_currentPuzzleIndex % _puzzles.Count];
        _foundWords.Clear();
        Cells.Clear();
        TargetWords.Clear();
        _selectedCells.Clear();

        for (int row = 0; row < _currentPuzzle.Rows.Length; row++)
        {
            var letters = _currentPuzzle.Rows[row];
            for (int col = 0; col < letters.Length; col++)
            {
                Cells.Add(new WordHuntCellViewModel(row, col, letters[col].ToString()));
            }
        }

        foreach (var word in _currentPuzzle.Words)
        {
            TargetWords.Add(new WordHuntTargetWordViewModel(word));
        }

        HintLabel.Text = $"Find every {_currentPuzzle.Theme.ToLowerInvariant()} word in the board.";
        UpdateProgressBars();
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
        if (_isPaused || _isGameOver)
        {
            return;
        }

        _timeLeftSeconds = Math.Max(0, _timeLeftSeconds - 1);
        UpdateTimer();

        if (_timeLeftSeconds == 0)
        {
            _ = EndGameAsync();
        }
    }

    void UpdateTimer()
    {
        TimerLabel.Text = $"{_timeLeftSeconds / 60:00}:{_timeLeftSeconds % 60:00}";
    }

    void UpdateScore()
    {
        ScoreLabel.Text = _score.ToString();
    }

    void UpdateProgressBars()
    {
        if (_currentPuzzle is null)
        {
            return;
        }

        double completion = TargetWords.Count == 0 ? 0 : (double)_foundWords.Count / TargetWords.Count;
        double remainingRatio = 1d - completion;
        double bonusRemaining = _currentPuzzle.BonusScore * remainingRatio;
        BonusValueLabel.Text = Math.Max(0, (int)Math.Round(bonusRemaining)).ToString();

        if (BonusTrack.Width > 0)
        {
            BonusFill.WidthRequest = BonusTrack.Width * remainingRatio;
        }

        if (ProgressTrack.Width > 0)
        {
            ProgressFill.WidthRequest = ProgressTrack.Width * completion;
        }
    }

    void OnTrackSizeChanged(object? sender, EventArgs e)
    {
        UpdateProgressBars();
    }

    void OnCellPointerPressed(object sender, PointerEventArgs e)
    {
        if (_isPaused || _isGameOver || !TryGetCell(sender, out var cell))
        {
            return;
        }

        _isDragSelecting = true;
        StartSelection(cell);
    }

    async void OnCellPointerEntered(object sender, PointerEventArgs e)
    {
        if (!_isDragSelecting || _isPaused || _isGameOver || !TryGetCell(sender, out var cell))
        {
            return;
        }

        if (cell.IsFound || _selectedCells.Contains(cell))
        {
            return;
        }

        if (!TryAppendCell(cell))
        {
            HintLabel.Text = "Keep sliding in one straight line.";
            return;
        }

        await GameAudioService.PlayTapAsync();
    }

    async void OnCellPointerReleased(object sender, PointerEventArgs e)
    {
        if (!_isDragSelecting)
        {
            return;
        }

        _isDragSelecting = false;
        await FinalizeSelectionAsync();
    }

    void StartSelection(WordHuntCellViewModel cell)
    {
        if (_isPaused || _isGameOver || cell.IsFound)
        {
            return;
        }

        ClearSelection();
        _selectedCells.Add(cell);
        cell.IsSelected = true;
        HintLabel.Text = "Keep swiping across neighboring letters.";
        _ = GameAudioService.PlayTapAsync();
    }

    async Task FinalizeSelectionAsync()
    {
        if (_isPaused || _isGameOver || _isHandlingSelection || _selectedCells.Count == 0)
        {
            return;
        }

        _isHandlingSelection = true;

        string candidate = GetSelectedWord();
        string reversed = new string(candidate.Reverse().ToArray());

        if (TryGetMatchingWord(candidate, reversed, out var matchedWord))
        {
            foreach (var selected in _selectedCells)
            {
                selected.IsFound = true;
                selected.IsSelected = false;
            }

            _foundWords.Add(matchedWord);
            var target = TargetWords.FirstOrDefault(item => string.Equals(item.Word, matchedWord, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
            {
                target.IsFound = true;
            }

            int points = matchedWord.Length * 80 + 40;
            _score += points;
            UpdateScore();
            HintLabel.Text = $"{matchedWord} found. +{points}";
            UpdateProgressBars();
            _selectedCells.Clear();

            if (_foundWords.Count == TargetWords.Count)
            {
                _score += 220;
                UpdateScore();
                HintLabel.Text = "Puzzle cleared. Completion bonus +220.";
                UpdateProgressBars();
                await Task.Delay(700);

                if (_currentPuzzleIndex < _puzzles.Count - 1)
                {
                    _currentPuzzleIndex++;
                    LoadPuzzle();
                }
                else
                {
                    await EndGameAsync();
                }
            }

            _isHandlingSelection = false;
            return;
        }

        if (!HasRemainingPrefix(candidate))
        {
            HintLabel.Text = "That path does not match any remaining word.";
            await Task.Delay(180);
            ClearSelection();
        }

        _isHandlingSelection = false;
    }

    static bool TryGetCell(object sender, out WordHuntCellViewModel cell)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is WordHuntCellViewModel found)
        {
            cell = found;
            return true;
        }

        cell = null!;
        return false;
    }

    bool TryAppendCell(WordHuntCellViewModel cell)
    {
        if (_selectedCells.Count == 0)
        {
            _selectedCells.Add(cell);
            cell.IsSelected = true;
            return true;
        }

        var last = _selectedCells[^1];
        int dr = cell.Row - last.Row;
        int dc = cell.Column - last.Column;

        if (Math.Abs(dr) > 1 || Math.Abs(dc) > 1 || (dr == 0 && dc == 0))
        {
            return false;
        }

        _selectedCells.Add(cell);
        cell.IsSelected = true;
        return true;
    }

    string GetSelectedWord()
    {
        return string.Concat(_selectedCells.Select(item => item.Letter)).ToUpperInvariant();
    }

    bool TryGetMatchingWord(string candidate, string reversed, out string word)
    {
        foreach (var target in TargetWords.Where(item => !item.IsFound))
        {
            if (string.Equals(target.Word, candidate, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target.Word, reversed, StringComparison.OrdinalIgnoreCase))
            {
                word = target.Word;
                return true;
            }
        }

        word = string.Empty;
        return false;
    }

    bool HasRemainingPrefix(string candidate)
    {
        string reversed = new string(candidate.Reverse().ToArray());

        return TargetWords.Where(item => !item.IsFound)
            .Any(item =>
                item.Word.StartsWith(candidate, StringComparison.OrdinalIgnoreCase) ||
                item.Word.StartsWith(reversed, StringComparison.OrdinalIgnoreCase) ||
                new string(item.Word.Reverse().ToArray()).StartsWith(candidate, StringComparison.OrdinalIgnoreCase));
    }

    void ClearSelection()
    {
        foreach (var cell in _selectedCells)
        {
            cell.IsSelected = false;
        }

        _selectedCells.Clear();
        _isDragSelecting = false;
    }

    void OnPauseClicked(object sender, TappedEventArgs e)
    {
        OnPauseClicked(sender, (EventArgs)e);
    }

    async void OnPauseClicked(object sender, EventArgs e)
    {
        if (_isPaused || _isGameOver)
        {
            return;
        }

        _isPaused = true;
        var action = await GamePauseService.ShowAsync(
            this,
            "Word Hunt",
            "Swipe through neighboring letters to uncover every hidden word before time runs out.");

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

    async Task EndGameAsync()
    {
        if (_isGameOver)
        {
            return;
        }

        _isGameOver = true;
        StopTimer();
        ClearSelection();

        int previousBest = BrainScoreService.GetGamePerformance("word_hunt")?.BestScore ?? 0;
        int best = Math.Max(previousBest, _score);

        int apexPoints = BrainScoreService.RecordGameScore("word_hunt", BrainSkill.Language, _score, 4300);
        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Word Hunt",
                score: _score,
                bestScore: best,
                apexPoints: apexPoints,
                isNewBest: _score > previousBest,
                playAgainFactory: () => new WordHuntGamePage(),
                accentHex: "#5A67FF"));
    }

    async void OnPlayAgainClicked(object sender, EventArgs e)
    {
        await GamePauseService.RestartCurrentPageAsync(this);
    }

    async void OnDoneClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }
}

public sealed class WordHuntCellViewModel : INotifyPropertyChanged
{
    bool _isSelected;
    bool _isFound;

    public int Row { get; }
    public int Column { get; }
    public string Letter { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundHex));
            OnPropertyChanged(nameof(TextHex));
        }
    }

    public bool IsFound
    {
        get => _isFound;
        set
        {
            if (_isFound == value)
            {
                return;
            }

            _isFound = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BackgroundHex));
            OnPropertyChanged(nameof(TextHex));
        }
    }

    public string BackgroundHex => IsFound ? "#50B6FF" : IsSelected ? "#53A7FF" : "#00000000";
    public string TextHex => IsFound || IsSelected ? "#F8FCFF" : "#E7E2F1";

    public WordHuntCellViewModel(int row, int column, string letter)
    {
        Row = row;
        Column = column;
        Letter = letter;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class WordHuntTargetWordViewModel : INotifyPropertyChanged
{
    bool _isFound;

    public string Word { get; }

    public bool IsFound
    {
        get => _isFound;
        set
        {
            if (_isFound == value)
            {
                return;
            }

            _isFound = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextHex));
            OnPropertyChanged(nameof(Opacity));
        }
    }

    public string TextHex => IsFound ? "#FFFFFF" : "#D6D0E6";
    public double Opacity => IsFound ? 1 : 0.66;

    public WordHuntTargetWordViewModel(string word)
    {
        Word = word;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
