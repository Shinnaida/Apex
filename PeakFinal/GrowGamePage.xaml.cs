namespace Peak;

public partial class GrowGamePage : ContentPage
{
    sealed record GrowRound(string Prefix, string Hint, string[] Words);

    readonly List<GrowRound> _rounds = new()
    {
        new("PE", "Longer words earn more points and improve the look of the tree!", new[] { "PEAK", "PEAR", "PEARL", "PEPPER", "PERK", "PERKS", "PERMIT", "PERSON", "PERSPECTIVE" }),
        new("DO", "Keep building on the same prefix to unlock extra blossoms.", new[] { "DOT", "DOES", "DOER", "DOERS", "DOING", "DONATE", "DOCUMENT", "DOCUMENTATION" }),
        new("RE", "Stretch into longer words whenever you can for a bigger finish.", new[] { "READ", "READER", "READING", "REASON", "RESULT", "RESPECT", "REFOCUS", "REIMAGINE" })
    };

    readonly HashSet<string> _foundWordsThisRound = new(StringComparer.OrdinalIgnoreCase);
    readonly List<Button> _keyboardButtons = new();

    IDispatcherTimer? _timer;
    int _currentRoundIndex;
    int _timeLeftSeconds = 60;
    int _score;
    int _growthStage;
    bool _started;
    bool _isPaused;
    bool _isGameOver;
    string _currentSuffix = string.Empty;

    GrowRound CurrentRound => _rounds[_currentRoundIndex];

    public GrowGamePage()
    {
        InitializeComponent();
        BuildKeyboard();
        UpdateTimer();
        UpdateScore();
        LoadRound(resetTree: true);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_started)
        {
            return;
        }

        _started = true;
        StartTimer();
        _ = GameAudioService.StartGameAtmosphereAsync("grow");
    }

    protected override void OnDisappearing()
    {
        StopTimer();
        _ = GameAudioService.StopGameAtmosphereAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => true;

    void BuildKeyboard()
    {
        AddKeyboardRow(KeyboardRow1, "QWERTYUIOP");
        AddKeyboardRow(KeyboardRow2, "ASDFGHJKL");
        AddKeyboardRow(KeyboardRow3, "ZXCVBNM", includeDelete: true);
    }

    void AddKeyboardRow(HorizontalStackLayout host, string keys, bool includeDelete = false)
    {
        foreach (char key in keys)
        {
            var button = CreateKeyButton(key.ToString());
            button.Clicked += OnKeyClicked;
            host.Children.Add(button);
            _keyboardButtons.Add(button);
        }

        if (includeDelete)
        {
            var delete = CreateKeyButton("⌫", width: 66, background: "#BFC2CC", textColor: "#FFFFFF");
            delete.Clicked += OnDeleteClicked;
            host.Children.Add(delete);
        }
    }

    Button CreateKeyButton(string text, double width = 48, string background = "#A27BC1", string textColor = "#FFFFFF")
    {
        return new Button
        {
            Text = text,
            WidthRequest = width,
            HeightRequest = 58,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb(background),
            TextColor = Color.FromArgb(textColor),
            FontAttributes = FontAttributes.Bold,
            FontSize = 20,
            Padding = new Thickness(0)
        };
    }

    void LoadRound(bool resetTree = false)
    {
        _foundWordsThisRound.Clear();
        _currentSuffix = string.Empty;

        if (resetTree)
        {
            _growthStage = 0;
        }

        RoundLabel.Text = $"{_currentRoundIndex + 1}/{_rounds.Count}";
        PromptLabel.Text = $"Grow words from {CurrentRound.Prefix}";
        FoundWordsLabel.Text = string.Empty;
        HintLabel.Text = CurrentRound.Hint;
        UpdateEntry();
        UpdateTree();
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

    void UpdateEntry()
    {
        string candidate = CurrentRound.Prefix + _currentSuffix;
        CurrentEntryLabel.Text = _currentSuffix.Length == 0
            ? $"{CurrentRound.Prefix}__"
            : candidate;
    }

    void UpdateTree()
    {
        Leaf1.IsVisible = _growthStage >= 1;
        Leaf2.IsVisible = _growthStage >= 2;
        Leaf3.IsVisible = _growthStage >= 3;
        Leaf4.IsVisible = _growthStage >= 4;
        Flower1.IsVisible = _growthStage >= 2;
        Flower2.IsVisible = _growthStage >= 3;
        Flower3.IsVisible = _growthStage >= 4;
        Flower4.IsVisible = _growthStage >= 5;
        Flower5.IsVisible = _growthStage >= 6;
    }

    async void OnKeyClicked(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || sender is not Button button)
        {
            return;
        }

        _currentSuffix += button.Text;
        UpdateEntry();
        await GameAudioService.PlayTapAsync();
    }

    async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_isPaused || _isGameOver || _currentSuffix.Length == 0)
        {
            return;
        }

        _currentSuffix = _currentSuffix[..^1];
        UpdateEntry();
        await GameAudioService.PlayTapAsync();
    }

    async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_isPaused || _isGameOver)
        {
            return;
        }

        string candidate = (CurrentRound.Prefix + _currentSuffix).ToUpperInvariant();
        if (candidate.Length <= CurrentRound.Prefix.Length)
        {
            HintLabel.Text = "Add more letters before you submit.";
            return;
        }

        if (_foundWordsThisRound.Contains(candidate))
        {
            HintLabel.Text = "That word is already helping this tree grow.";
            return;
        }

        if (!CurrentRound.Words.Contains(candidate, StringComparer.OrdinalIgnoreCase))
        {
            HintLabel.Text = "That word is not in this round's garden.";
            EntryFrame.BackgroundColor = Color.FromArgb("#5B3046");
            await Task.Delay(180);
            EntryFrame.BackgroundColor = Color.FromArgb("#30263F");
            return;
        }

        _foundWordsThisRound.Add(candidate);
        _growthStage = Math.Min(6, _growthStage + Math.Clamp(candidate.Length - CurrentRound.Prefix.Length, 1, 2));
        _score += candidate.Length * 35;
        UpdateScore();
        PromptLabel.Text = candidate;
        FoundWordsLabel.Text = string.Join(Environment.NewLine, _foundWordsThisRound.OrderBy(word => word.Length).ThenBy(word => word));
        HintLabel.Text = $"{candidate} added. Longer words give your tree more bloom.";
        _currentSuffix = string.Empty;
        UpdateEntry();
        UpdateTree();
        await GameAudioService.PlayTapAsync();

        if (_foundWordsThisRound.Count >= 4 || _foundWordsThisRound.Count == CurrentRound.Words.Length)
        {
            if (_currentRoundIndex < _rounds.Count - 1)
            {
                _currentRoundIndex++;
                await Task.Delay(500);
                LoadRound();
            }
            else
            {
                await EndGameAsync();
            }
        }
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
            "Grow",
            "Use the given prefix to type valid words. Longer words score more and help the tree bloom faster.");

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

        int previousBest = BrainScoreService.GetGamePerformance("grow")?.BestScore ?? 0;
        int best = Math.Max(previousBest, _score);
        var apexPoints = BrainScoreService.RecordGameScore("grow", BrainSkill.Language, _score, 1200);

        await PageTransitionService.PushAsync(
            Navigation,
            () => new GenericGameSummaryPage(
                gameTitle: "Grow",
                score: _score,
                bestScore: best,
                apexPoints: apexPoints,
                isNewBest: _score > previousBest,
                playAgainFactory: () => new GrowGamePage(),
                accentHex: "#6FCC72"));
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
