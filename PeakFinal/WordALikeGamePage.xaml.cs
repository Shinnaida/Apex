namespace Peak;

public partial class WordALikeGamePage : ContentPage
{
    private List<WordALikeRound> _rounds = new();
    private readonly List<AnswerSlot> _answerSlots = new();
    private readonly Random _random = new();

    private int _currentRoundIndex = 0;
    private int _score = 0;
    private int _timeLeftSeconds = 116;
    private bool _isPaused = false;

    private WordALikeRound? _currentRound;

    public WordALikeGamePage()
    {
        InitializeComponent();

        SeedRounds();
        LoadRound();
        StartTimer();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("word_a_like");
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

    private void SeedRounds()
    {
        _rounds = WordALikeRoundDatabase
            .GetRounds()
            .OrderBy(x => _random.Next())
            .ToList();
    }

    private void LoadRound()
    {
        if (_currentRoundIndex >= _rounds.Count)
            _currentRoundIndex = 0;

        _currentRound = _rounds[_currentRoundIndex];

        PromptLabel.Text = _currentRound.Prompt;
        AnswersContainer.Children.Clear();
        _answerSlots.Clear();

        foreach (var answer in _currentRound.Answers)
        {
            var slot = CreateAnswerSlot(answer);
            _answerSlots.Add(slot);
            AnswersContainer.Children.Add(slot.RowLayout);
        }

        BuildLetterPool();
    }

    private AnswerSlot CreateAnswerSlot(string answer)
    {
        var row = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center
        };

        var labels = new List<Label>();
        var frames = new List<Frame>();
        var hiddenIndexes = GetHiddenIndexes(answer.Length);

        for (int i = 0; i < answer.Length; i++)
        {
            bool isHidden = hiddenIndexes.Contains(i);

            var label = new Label
            {
                Text = isHidden ? "_" : answer[i].ToString(),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 26,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            };

            var frame = new Frame
            {
                WidthRequest = 60,
                HeightRequest = 60,
                CornerRadius = 6,
                Padding = 0,
                HasShadow = false,
                BackgroundColor = isHidden
                    ? Color.FromArgb("#28BEEA")
                    : Color.FromArgb("#8AAFC1"),
                BorderColor = Color.FromArgb("#1B1823"),
                Content = label
            };

            row.Children.Add(frame);
            labels.Add(label);
            frames.Add(frame);
        }

        return new AnswerSlot
        {
            Answer = answer,
            RowLayout = row,
            Labels = labels,
            Frames = frames,
            HiddenIndexes = hiddenIndexes,
            CurrentInput = new Dictionary<int, string>()
        };
    }

    private List<int> GetHiddenIndexes(int length)
{
    var indexes = new List<int>();

    int hideCount;

    if (length <= 3)
        hideCount = 1;
    else if (length <= 5)
        hideCount = 2;
    else
        hideCount = 3;

    var available = Enumerable.Range(0, length).ToList();

    for (int i = 0; i < hideCount; i++)
    {
        if (!available.Any())
            break;

        int randomIndex = _random.Next(available.Count);
        indexes.Add(available[randomIndex]);
        available.RemoveAt(randomIndex);
    }

    indexes.Sort();

    return indexes;
}

    private void BuildLetterPool()
    {
        var letters = new List<string>();

        foreach (var slot in _answerSlots)
        {
            foreach (int hiddenIndex in slot.HiddenIndexes)
            {
                letters.Add(slot.Answer[hiddenIndex].ToString());
            }
        }

        while (letters.Count < 12)
        {
            letters.Add(RandomLetter());
        }

        letters = letters
            .OrderBy(_ => _random.Next())
            .Take(12)
            .ToList();

        BuildLetterPoolRow(LetterPoolRow1, letters.Take(6).ToList());
        BuildLetterPoolRow(LetterPoolRow2, letters.Skip(6).Take(6).ToList());
    }

    private void BuildLetterPoolRow(Grid grid, List<string> letters)
    {
        grid.Children.Clear();
        grid.ColumnDefinitions.Clear();

        for (int i = 0; i < letters.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var btn = new Button
            {
                Text = letters[i],
                BackgroundColor = Color.FromArgb("#B689C8"),
                TextColor = Colors.White,
                HeightRequest = 52,
                CornerRadius = 4,
                FontAttributes = FontAttributes.Bold,
                FontSize = 20
            };

            btn.Clicked += OnLetterClicked;
            grid.Add(btn, i, 0);
        }
    }

    private string RandomLetter()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return chars[_random.Next(chars.Length)].ToString();
    }

    private async void OnLetterClicked(object? sender, EventArgs e)
    {
        if (_isPaused || _currentRound == null || sender is not Button btn)
            return;

        string letter = btn.Text ?? "";
        if (string.IsNullOrWhiteSpace(letter))
            return;

        var targetSlot = GetActiveSlot();
        if (targetSlot == null)
            return;

        int? nextHiddenIndex = GetNextHiddenIndex(targetSlot);
        if (nextHiddenIndex == null)
            return;

        int index = nextHiddenIndex.Value;
        string expectedLetter = targetSlot.Answer[index].ToString();

        if (letter.Equals(expectedLetter, StringComparison.OrdinalIgnoreCase))
        {
            targetSlot.Labels[index].Text = expectedLetter;
            targetSlot.Frames[index].BackgroundColor = Color.FromArgb("#8AAFC1");
            targetSlot.CurrentInput[index] = expectedLetter;

            await AnimateBox(targetSlot.Frames[index]);

            btn.IsEnabled = false;
            btn.Opacity = 0.35;

            if (IsSlotSolved(targetSlot))
            {
                targetSlot.IsSolved = true;
                await MarkAnswerCorrect(targetSlot);

                _score += 100;
                ScoreLabel.Text = _score.ToString();

                if (_answerSlots.All(x => x.IsSolved))
                {
                    await Task.Delay(500);
                    _currentRoundIndex++;
                    LoadRound();
                }
            }
        }
        else
        {
            var originalColor = btn.BackgroundColor;

            btn.BackgroundColor = Color.FromArgb("#FF2957");

            await btn.TranslateTo(-4, 0, 35);
            await btn.TranslateTo(4, 0, 35);
            await btn.TranslateTo(-3, 0, 30);
            await btn.TranslateTo(3, 0, 30);
            await btn.TranslateTo(0, 0, 30);

            btn.BackgroundColor = originalColor;
        }
    }

    private AnswerSlot? GetActiveSlot()
    {
        return _answerSlots.FirstOrDefault(x => !x.IsSolved && GetNextHiddenIndex(x) != null);
    }

    private int? GetNextHiddenIndex(AnswerSlot slot)
    {
        foreach (var index in slot.HiddenIndexes)
        {
            if (!slot.CurrentInput.ContainsKey(index))
                return index;
        }

        return null;
    }

    private bool IsSlotSolved(AnswerSlot slot)
    {
        return slot.HiddenIndexes.All(i => slot.CurrentInput.ContainsKey(i));
    }

    private async Task AnimateBox(Frame frame)
    {
        await frame.ScaleTo(1.08, 80, Easing.CubicOut);
        await frame.ScaleTo(1.0, 80, Easing.CubicIn);
    }

    private async Task MarkAnswerCorrect(AnswerSlot slot)
    {
        foreach (var frame in slot.Frames)
        {
            frame.BackgroundColor = Color.FromArgb("#35C76F");
            await frame.ScaleTo(1.05, 70, Easing.CubicOut);
            await frame.ScaleTo(1.0, 70, Easing.CubicIn);
        }
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        if (_isPaused)
            return;

        var targetSlot = _answerSlots.FirstOrDefault(x => !x.IsSolved && x.CurrentInput.Any());
        if (targetSlot == null)
            return;

        foreach (var hiddenIndex in targetSlot.HiddenIndexes)
        {
            targetSlot.Labels[hiddenIndex].Text = "_";
            targetSlot.Frames[hiddenIndex].BackgroundColor = Color.FromArgb("#28BEEA");
        }

        targetSlot.CurrentInput.Clear();
        BuildLetterPool();
    }

    private void OnNextClicked(object sender, EventArgs e)
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
            "Word-A-Like",
            "Build the target word from the letter pool before the round timer runs out.");

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
                    int previousBest = BrainScoreService.GetGamePerformance("word_a_like")?.BestScore ?? 0;
                    bool isNewBest = _score > previousBest;
                    int bestScore = Math.Max(previousBest, _score);

                    int apexPoints = BrainScoreService.RecordGameScore(
                        sourceId: "word_a_like",
                        skill: BrainSkill.Language,
                        rawScore: _score,
                        expectedTopScore: 2500);

                    await PageTransitionService.PushAsync(
                        Navigation,
                        () => new GenericGameSummaryPage(
                            gameTitle: "Word-A-Like",
                            score: _score,
                            bestScore: bestScore,
                            apexPoints: apexPoints,
                            isNewBest: isNewBest,
                            playAgainFactory: () => new WordALikeGamePage(),
                            accentHex: "#7F64FF"));
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
}

public class WordALikeRound
{
    public string Prompt { get; set; } = "";
    public List<string> Answers { get; set; } = new();
}

public class AnswerSlot
{
    public string Answer { get; set; } = "";
    public bool IsSolved { get; set; }

    public HorizontalStackLayout RowLayout { get; set; } = new();
    public List<Label> Labels { get; set; } = new();
    public List<Frame> Frames { get; set; } = new();
    public List<int> HiddenIndexes { get; set; } = new();
    public Dictionary<int, string> CurrentInput { get; set; } = new();
}

