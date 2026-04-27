namespace Peak;

public partial class DecoderGamePage : ContentPage
{
    sealed record DecoderPuzzle(string EncodedWord, string Answer, string[] Options, int CorrectIndex, string Legend, string Hint);

    const int SecondsPerCard = 20;
    const int TotalCards = 10;
    const string CalmTimerAccentHex = "#7BD8FF";

    static readonly string[] WordPool =
    {
        "PEAK", "FOCUS", "LASER", "MIND", "TRACK", "SWIFT", "BLINK", "FRAME",
        "SHARP", "BRAIN", "PRIME", "FLASH", "QUICK", "SOLVE", "WATCH", "ALIGN",
        "GLOW", "LIGHT", "CLUE", "SHIFT", "CODE", "LEVEL", "TIMER", "LOGIC",
        "SPARK", "VISION", "ALERT", "MATCH", "SCOPE", "TRACE"
    };

    readonly Random _random = new();
    readonly HashSet<string> _usedAnswers = new(StringComparer.Ordinal);

    IDispatcherTimer? _timer;
    DecoderPuzzle? _currentPuzzle;
    int _timeLeft;
    int _roundIndex;
    int _score;
    int _streak;
    bool _started;
    bool _isPaused;
    bool _isGameOver;
    bool _inputLocked;

    public DecoderGamePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("decoder");

        if (_started)
        {
            return;
        }

        _started = true;
        StartGame();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTimer();
        _ = GameAudioService.StopGameAtmosphereAsync();
    }

    protected override bool OnBackButtonPressed() => true;

    void StartGame()
    {
        StopTimer();

        _usedAnswers.Clear();
        _score = 0;
        _roundIndex = 0;
        _streak = 0;
        _timeLeft = SecondsPerCard;
        _isPaused = false;
        _isGameOver = false;
        _inputLocked = false;

        PauseOverlay.IsVisible = false;
        ResultOverlay.IsVisible = false;
        FeedbackCard.IsVisible = false;

        UpdateScoreUi();
        LoadNextPuzzle();
        StartTimer();
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
        if (_isPaused || _isGameOver || _inputLocked)
        {
            return;
        }

        _timeLeft = Math.Max(0, _timeLeft - 1);
        UpdateTimerUi();

        if (_timeLeft == 0)
        {
            _ = RevealAnswerAsync(isCorrect: false, timedOut: true, selectedIndex: null);
        }
    }

    void LoadNextPuzzle()
    {
        if (_roundIndex >= TotalCards)
        {
            EndGame();
            return;
        }

        _timeLeft = SecondsPerCard;
        _inputLocked = false;
        FeedbackCard.IsVisible = false;
        _currentPuzzle = BuildPuzzle();

        RoundLabel.Text = $"CARD {_roundIndex + 1} OF {TotalCards}";
        LegendLabel.Text = _currentPuzzle.Legend;
        EncodedWordLabel.Text = _currentPuzzle.EncodedWord;
        HintLabel.Text = _currentPuzzle.Hint;
        FooterHintLabel.Text = "Each new card resets to 20 seconds.";
        StatusCaptionLabel.Text = "DECODE THE CARD";

        A0.Text = _currentPuzzle.Options[0];
        A1.Text = _currentPuzzle.Options[1];
        A2.Text = _currentPuzzle.Options[2];
        A3.Text = _currentPuzzle.Options[3];

        ResetAnswerStyles();
        UpdateTimerUi();
    }

    DecoderPuzzle BuildPuzzle()
    {
        string answer = PickAnswer();
        var substitutions = BuildSubstitutions(answer);

        string encoded = new(answer.Select(letter => substitutions[letter]).ToArray());

        var options = WordPool
            .Where(word => word.Length == answer.Length && !string.Equals(word, answer, StringComparison.Ordinal))
            .OrderBy(_ => _random.Next())
            .Take(3)
            .ToList();

        while (options.Count < 3)
        {
            string filler = WordPool[_random.Next(WordPool.Length)];
            if (!options.Contains(filler, StringComparer.Ordinal) && !string.Equals(filler, answer, StringComparison.Ordinal))
            {
                options.Add(filler);
            }
        }

        options.Add(answer);
        options = options.OrderBy(_ => _random.Next()).ToList();

        string legend = string.Join("   ",
            substitutions
                .OrderBy(pair => pair.Value)
                .Select(pair => $"{pair.Value}->{pair.Key}"));

        return new DecoderPuzzle(
            EncodedWord: encoded,
            Answer: answer,
            Options: options.ToArray(),
            CorrectIndex: options.IndexOf(answer),
            Legend: legend,
            Hint: "Match each coded letter to the plain letter shown in the key.");
    }

    string PickAnswer()
    {
        var available = WordPool.Where(word => !_usedAnswers.Contains(word)).ToList();
        if (available.Count == 0)
        {
            _usedAnswers.Clear();
            available = WordPool.ToList();
        }

        string answer = available[_random.Next(available.Count)];
        _usedAnswers.Add(answer);
        return answer;
    }

    Dictionary<char, char> BuildSubstitutions(string answer)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var substitutions = new Dictionary<char, char>();
        var usedEncodedLetters = new HashSet<char>();

        foreach (char letter in answer.Distinct())
        {
            char encoded;
            do
            {
                encoded = alphabet[_random.Next(alphabet.Length)];
            }
            while (encoded == letter || usedEncodedLetters.Contains(encoded));

            substitutions[letter] = encoded;
            usedEncodedLetters.Add(encoded);
        }

        return substitutions;
    }

    void UpdateTimerUi()
    {
        TimerLabel.Text = $"00:{_timeLeft:00}";
        TimerBar.Progress = _timeLeft / (double)SecondsPerCard;

        bool warning = _timeLeft <= 10;
        TimerLabel.TextColor = warning
            ? Color.FromArgb("#FF8F8F")
            : Color.FromArgb(CalmTimerAccentHex);
        TimerBar.ProgressColor = warning
            ? Color.FromArgb("#FF5B6E")
            : Color.FromArgb(CalmTimerAccentHex);
        StatusCaptionLabel.TextColor = warning
            ? Color.FromArgb("#FFCAD3")
            : Color.FromArgb("#FFD6E3");
    }

    void UpdateScoreUi()
    {
        ScoreLabel.Text = _score.ToString();
        StreakLabel.Text = $"STREAK {_streak}";
    }

    void ResetAnswerStyles()
    {
        foreach (var button in AnswerButtons())
        {
            button.BackgroundColor = Color.FromArgb("#FFF8FA");
            button.BorderColor = Color.FromArgb("#E7C0CF");
            button.BorderWidth = 1;
            button.TextColor = Color.FromArgb("#3A2430");
            button.IsEnabled = true;
        }
    }

    IEnumerable<Button> AnswerButtons() => new[] { A0, A1, A2, A3 };

    async void OnAnswerClicked(object sender, EventArgs e)
    {
        if (_inputLocked || _isPaused || _isGameOver || _currentPuzzle is null)
        {
            return;
        }

        if (sender is Button button)
        {
            await InteractionEffects.AnimateTapAsync(button);
        }

        await GameAudioService.PlayTapAsync();

        int selectedIndex = sender == A0 ? 0 :
                            sender == A1 ? 1 :
                            sender == A2 ? 2 : 3;

        bool isCorrect = selectedIndex == _currentPuzzle.CorrectIndex;
        await RevealAnswerAsync(isCorrect, timedOut: false, selectedIndex);
    }

    async Task RevealAnswerAsync(bool isCorrect, bool timedOut, int? selectedIndex)
    {
        if (_inputLocked || _currentPuzzle is null)
        {
            return;
        }

        _inputLocked = true;

        foreach (var button in AnswerButtons())
        {
            button.IsEnabled = false;
        }

        var correctButton = AnswerButtons().ElementAt(_currentPuzzle.CorrectIndex);
        correctButton.BackgroundColor = Color.FromArgb("#DCFCE7");
        correctButton.BorderColor = Color.FromArgb("#22C55E");
        correctButton.TextColor = Color.FromArgb("#166534");

        if (selectedIndex is int index && index != _currentPuzzle.CorrectIndex)
        {
            var selectedButton = AnswerButtons().ElementAt(index);
            selectedButton.BackgroundColor = Color.FromArgb("#FDE2E2");
            selectedButton.BorderColor = Color.FromArgb("#EF4444");
            selectedButton.TextColor = Color.FromArgb("#991B1B");
        }

        if (isCorrect)
        {
            int earned = 100 + (_timeLeft * 8) + (_streak * 12);
            _score += earned;
            _streak++;
            FeedbackTitleLabel.Text = "Correct";
            FeedbackPointsLabel.Text = $"+{earned} points";
            FeedbackPointsLabel.TextColor = Color.FromArgb("#188038");
            FeedbackDetailLabel.Text = $"Decoded as {_currentPuzzle.Answer}. Fast answers build a bigger streak bonus.";
        }
        else
        {
            _streak = 0;
            FeedbackTitleLabel.Text = timedOut ? "Time's up" : "Wrong answer";
            FeedbackPointsLabel.Text = timedOut ? "No answer in 20 seconds" : $"Correct answer: {_currentPuzzle.Answer}";
            FeedbackPointsLabel.TextColor = Color.FromArgb("#B45309");
            FeedbackDetailLabel.Text = $"The cipher key decodes {_currentPuzzle.EncodedWord} into {_currentPuzzle.Answer}.";
        }

        FeedbackCard.IsVisible = true;
        FooterHintLabel.Text = isCorrect
            ? "Nice decode. Next card coming up."
            : "Stay sharp. A new card is loading.";

        UpdateScoreUi();
        await Task.Delay(1100);

        if (_isGameOver)
        {
            return;
        }

        _roundIndex++;
        LoadNextPuzzle();
    }

    void EndGame()
    {
        if (_isGameOver)
        {
            return;
        }

        _isGameOver = true;
        StopTimer();

        int previousBest = BrainScoreService.GetGamePerformance("decoder")?.BestScore ?? 0;
        bool isNewBest = _score > previousBest;
        int best = Math.Max(previousBest, _score);
        int apexPoints = BrainScoreService.RecordGameScore("decoder", BrainSkill.Focus, _score, DecoderProgress.ExpectedTopScore);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await PageTransitionService.PushAsync(
                Navigation,
                () => new GenericGameSummaryPage(
                    gameTitle: "Decoder",
                    score: _score,
                    bestScore: best,
                    apexPoints: apexPoints,
                    isNewBest: isNewBest,
                    playAgainFactory: () => new DecoderGamePage(),
                    accentHex: "#FF4E79",
                    secondaryLabel: "Rank",
                    secondaryValue: DecoderProgress.ResolveRank(best)));
        });
    }

    async void OnPauseTapped(object sender, TappedEventArgs e)
    {
        if (_isGameOver || _isPaused)
        {
            return;
        }

        _isPaused = true;

        var action = await GamePauseService.ShowAsync(
            this,
            "Decoder",
            "Decode the pattern and choose the matching word before the card timer runs out.");

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

    void OnResumeClicked(object sender, EventArgs e)
    {
        _isPaused = false;
        PauseOverlay.IsVisible = false;
    }

    async void OnQuitClicked(object sender, EventArgs e)
    {
        StopTimer();
        await PageTransitionService.PopAsync(Navigation);
    }

    void OnPlayAgainClicked(object sender, EventArgs e)
    {
        StartGame();
    }

    async void OnDoneClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }
}
