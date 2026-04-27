namespace Peak;

public partial class BabbleBotsGamePage : ContentPage
{
    sealed record BabbleBotsRound(string SeedWord, string LetterBank, string Hint, string[] TargetWords);

    readonly Random _random = new();
    readonly List<BabbleBotsRound> _rounds = new()
    {
        new("TIRES", "CEIRST", "Build hidden bot words from the TIRES letter bank.", new[] { "SIR", "TIE", "SIT", "SIRE", "STIR", "TRIES" }),
        new("BOUTS", "OBSTUW", "Quick words score more when your combo is alive.", new[] { "BUS", "SUB", "TUB", "BOW", "BOUT", "STOW" }),
        new("LASER", "AELRST", "Try short and medium words before you reshuffle.", new[] { "SEA", "SALE", "STAR", "TEAR", "LATE", "STALE" }),
        new("STONE", "ENOSTR", "Clear the full round to earn a bot bonus.", new[] { "NOTE", "TONE", "ONES", "ROSE", "TONES", "STORE" })
    };

    readonly List<Button> _letterButtons;
    readonly List<int> _selectedIndexes = new();
    readonly HashSet<string> _usedWords = new(StringComparer.OrdinalIgnoreCase);

    BabbleBotsRound? _currentRound;
    IDispatcherTimer? _timer;
    int _currentRoundIndex;
    int _score;
    int _timeLeftSeconds = 90;
    int _comboMultiplier = 1;
    bool _playerBubbleOnLeft = true;
    bool _isPaused;
    bool _isGameOver;
    bool _started;

    public BabbleBotsGamePage()
    {
        InitializeComponent();
        _letterButtons = new() { LetterButton0, LetterButton1, LetterButton2, LetterButton3, LetterButton4, LetterButton5 };
        UpdateScore();
        UpdateTimer();
        UpdateCombo();
        UpdateCurrentWord();
        UpdateSubmitState();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
        {
            return;
        }

        _started = true;
        _ = GameAudioService.StartGameAtmosphereAsync("babble_bots");
        LoadRound();
        StartTimer();
    }

    protected override void OnDisappearing()
    {
        StopTimer();
        _ = GameAudioService.StopGameAtmosphereAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => true;

    void LoadRound()
    {
        _currentRound = _rounds[_currentRoundIndex % _rounds.Count];
        _usedWords.Clear();
        _selectedIndexes.Clear();
        _playerBubbleOnLeft = true;
        SeedWordLabel.Text = _currentRound.SeedWord;
        HintLabel.Text = "Create words of 3 letters or more by tapping letters and pressing Submit.";
        LeftBubbleStack.Children.Clear();
        RightBubbleStack.Children.Clear();
        ApplyLetters(_currentRound.LetterBank.OrderBy(_ => _random.Next()).ToArray());
        UpdateCurrentWord();
        UpdateSubmitState();
    }

    void ApplyLetters(IEnumerable<char> letters)
    {
        int index = 0;
        foreach (var letter in letters)
        {
            var button = _letterButtons[index++];
            button.Text = letter.ToString();
            button.IsEnabled = true;
            button.Opacity = 1;
            button.BackgroundColor = Color.FromArgb("#9B77B1");
        }
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

    void UpdateCombo()
    {
        ComboLabel.Text = $"x{_comboMultiplier}";
    }

    void UpdateCurrentWord()
    {
        CurrentWordLabel.Text = _selectedIndexes.Count == 0
            ? "-"
            : string.Concat(_selectedIndexes.Select(index => _letterButtons[index].Text));
    }

    void UpdateSubmitState()
    {
        bool canSubmit = _selectedIndexes.Count >= 3 && !_isGameOver;
        SubmitButton.IsEnabled = canSubmit;
        SubmitButton.BackgroundColor = canSubmit ? Color.FromArgb("#4A3AB1") : Color.FromArgb("#3B3349");
        SubmitButton.TextColor = canSubmit ? Colors.White : Color.FromArgb("#8B86A0");
    }

    async void OnLetterClicked(object sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || sender is not Button button)
        {
            return;
        }

        int index = _letterButtons.IndexOf(button);
        if (index < 0 || _selectedIndexes.Contains(index))
        {
            return;
        }

        await InteractionEffects.AnimateTapAsync(button);
        await GameAudioService.PlayTapAsync();

        _selectedIndexes.Add(index);
        button.IsEnabled = false;
        button.Opacity = 0.28;
        UpdateCurrentWord();
        UpdateSubmitState();
    }

    void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _selectedIndexes.Count == 0)
        {
            return;
        }

        int lastIndex = _selectedIndexes[^1];
        _selectedIndexes.RemoveAt(_selectedIndexes.Count - 1);

        var button = _letterButtons[lastIndex];
        button.IsEnabled = true;
        button.Opacity = 1;

        UpdateCurrentWord();
        UpdateSubmitState();
    }

    void OnShuffleClicked(object sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _currentRound is null)
        {
            return;
        }

        var enabledIndexes = _letterButtons
            .Select((button, index) => (button, index))
            .Where(x => x.button.IsEnabled)
            .Select(x => x.index)
            .ToList();

        var shuffled = enabledIndexes
            .Select(index => _letterButtons[index].Text.First())
            .OrderBy(_ => _random.Next())
            .ToList();

        for (int i = 0; i < enabledIndexes.Count; i++)
        {
            _letterButtons[enabledIndexes[i]].Text = shuffled[i].ToString();
        }

        HintLabel.Text = "The bot bank has been shuffled.";
    }

    async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _currentRound is null)
        {
            return;
        }

        string candidate = string.Concat(_selectedIndexes.Select(index => _letterButtons[index].Text)).ToUpperInvariant();
        if (candidate.Length < 3)
        {
            return;
        }

        if (_usedWords.Contains(candidate))
        {
            await FlashCurrentWordAsync("#A13D4E", "That word is already in the bot chat. Try a fresh one.");
            ResetCombo();
            ClearSelection();
            return;
        }

        if (_currentRound.TargetWords.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            _usedWords.Add(candidate);
            int points = candidate.Length * 70 * _comboMultiplier;
            _score += points;
            _comboMultiplier = Math.Min(4, _comboMultiplier + 1);
            UpdateScore();
            UpdateCombo();
            AddBubble(candidate, _playerBubbleOnLeft, true);
            await FlashCurrentWordAsync("#35C76F", $"+{points} points");
            await GameAudioService.PlayTapAsync();
            ClearSelection();

            await Task.Delay(220);
            AddBotReply();

            if (_usedWords.Count >= _currentRound.TargetWords.Length)
            {
                _score += 150;
                UpdateScore();
                HintLabel.Text = "Round cleared. Bot bonus +150.";
                await Task.Delay(850);
                _currentRoundIndex++;
                LoadRound();
            }

            return;
        }

        await FlashCurrentWordAsync("#B33453", "That word is not in this bot bank.");
        ResetCombo();
        ClearSelection();
    }

    async Task FlashCurrentWordAsync(string colorHex, string hint)
    {
        var original = CurrentWordFrame.BackgroundColor;
        CurrentWordFrame.BackgroundColor = Color.FromArgb(colorHex);
        HintLabel.Text = hint;
        await Task.Delay(220);
        CurrentWordFrame.BackgroundColor = original;
    }

    void ResetCombo()
    {
        _comboMultiplier = 1;
        UpdateCombo();
    }

    void ClearSelection()
    {
        foreach (int index in _selectedIndexes)
        {
            var button = _letterButtons[index];
            button.IsEnabled = true;
            button.Opacity = 1;
        }

        _selectedIndexes.Clear();
        UpdateCurrentWord();
        UpdateSubmitState();
    }

    void AddBubble(string word, bool onLeft, bool isPlayerWord)
    {
        var bubble = new Frame
        {
            Padding = new Thickness(16, 10),
            CornerRadius = 14,
            HasShadow = false,
            BackgroundColor = isPlayerWord
                ? (onLeft ? Color.FromArgb("#F3D0B8") : Color.FromArgb("#A8EAFF"))
                : (onLeft ? Color.FromArgb("#E7D1FF") : Color.FromArgb("#BDEFCB")),
            Content = new Label
            {
                Text = word,
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#3B2F57")
            }
        };

        if (onLeft)
        {
            LeftBubbleStack.Children.Add(bubble);
        }
        else
        {
            RightBubbleStack.Children.Add(bubble);
        }
    }

    void AddBotReply()
    {
        if (_currentRound is null)
        {
            return;
        }

        var reply = _currentRound.TargetWords
            .FirstOrDefault(word => !_usedWords.Contains(word));

        if (string.IsNullOrWhiteSpace(reply))
        {
            return;
        }

        _usedWords.Add(reply);
        AddBubble(reply, !_playerBubbleOnLeft, false);
        _playerBubbleOnLeft = !_playerBubbleOnLeft;
        HintLabel.Text = "Create words quickly to activate the score multiplier!";
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
            "Babble Bots",
            "Tap letters to build valid bot words from the current letter bank, then submit before time runs out.");

        if (action == GamePauseAction.Restart)
        {
            StopTimer();
            await GamePauseService.RestartCurrentPageAsync(this);
            return;
        }

        if (action == GamePauseAction.Exit)
        {
            StopTimer();
            await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
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

        int previousBest = BrainScoreService.GetGamePerformance("babble_bots")?.BestScore ?? 0;
        int best = Math.Max(previousBest, _score);

        var apexPoints = BrainScoreService.RecordGameScore("babble_bots", BrainSkill.Language, _score, 2600);

        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Babble Bots",
                score: _score,
                bestScore: best,
                apexPoints: apexPoints,
                isNewBest: _score > previousBest,
                playAgainFactory: () => new BabbleBotsGamePage(),
                accentHex: "#6A5BFF"));
    }

    async void OnPlayAgainClicked(object sender, EventArgs e)
    {
        await GamePauseService.RestartCurrentPageAsync(this);
    }

    async void OnDoneClicked(object sender, EventArgs e)
    {
        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }
}
