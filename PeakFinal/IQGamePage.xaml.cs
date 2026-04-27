namespace Peak;

public partial class IQGamePage : ContentPage
{
    const int QuestionTimeLimitSeconds = 20;

    readonly IQSession _session;
    readonly TimeSpan _autoAdvanceDelay = TimeSpan.FromMilliseconds(1100);

    bool _hasAnsweredCurrentQuestion;
    bool _navigating;
    bool _timerRunning;
    int _questionTimeLeft = QuestionTimeLimitSeconds;

    public IQGamePage(IQSession session)
    {
        InitializeComponent();
        _session = session;

        NavigationPage.SetHasBackButton(this, false);
        Render();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = GameAudioService.StartGameAtmosphereAsync("iq");
        StartTimer();
        UpdateTimerLabel();
    }

    protected override void OnDisappearing()
    {
        _timerRunning = false;
        _ = GameAudioService.StopGameAtmosphereAsync();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        return true;
    }

    void StartTimer()
    {
        if (_timerRunning)
        {
            return;
        }

        _timerRunning = true;

        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (!_timerRunning)
            {
                return false;
            }

            if (_hasAnsweredCurrentQuestion)
            {
                return true;
            }

            _questionTimeLeft = Math.Max(0, _questionTimeLeft - 1);
            UpdateTimerLabel();

            if (_questionTimeLeft <= 0)
            {
                _ = HandleQuestionTimeoutAsync();
            }

            return true;
        });
    }

    void UpdateTimerLabel()
    {
        TimerLabel.Text = $"00:{_questionTimeLeft:00}";
        TimerProgress.Progress = _questionTimeLeft / (double)QuestionTimeLimitSeconds;

        var isUrgent = _questionTimeLeft <= 5;
        TimerBadge.BackgroundColor = isUrgent
            ? Color.FromArgb("#FFF1F2")
            : Color.FromArgb("#E8F0FF");
        TimerCaptionLabel.TextColor = isUrgent
            ? Color.FromArgb("#E11D48")
            : Color.FromArgb("#5D7FD6");
        TimerLabel.TextColor = isUrgent
            ? Color.FromArgb("#BE123C")
            : Color.FromArgb("#214CCF");
        TimerProgress.ProgressColor = isUrgent
            ? Color.FromArgb("#EF476F")
            : Color.FromArgb("#2E77FF");
    }

    void Render()
    {
        _questionTimeLeft = QuestionTimeLimitSeconds;
        _hasAnsweredCurrentQuestion = false;
        FeedbackCard.IsVisible = false;
        UpdateTimerLabel();

        var question = _session.Current;
        ModeLabel.Text = _session.Definition.Title;
        HeaderLabel.Text = $"Question {_session.Index + 1} of {_session.Questions.Count}";
        ScoreLabel.Text = $"{_session.CurrentScore} pts";
        Progress.Progress = _session.Index / (double)_session.Questions.Count;

        CategoryLabel.Text = IQDisplay.GetCategoryLabel(question.Category);
        DifficultyLabel.Text = IQDisplay.GetDifficultyLabel(question.Difficulty);
        QuestionValueLabel.Text = $"{IQDisplay.GetPointValue(question.Difficulty)} pts";
        PromptLabel.Text = question.Prompt;

        if (!string.IsNullOrWhiteSpace(question.ImageSource))
        {
            QuestionImage.Source = question.ImageSource;
            QuestionImage.IsVisible = true;
        }
        else
        {
            QuestionImage.IsVisible = false;
            QuestionImage.Source = null;
        }

        A0.Text = question.Options[0];
        A1.Text = question.Options[1];
        A2.Text = question.Options[2];
        A3.Text = question.Options[3];

        SetOptionsEnabled(true);
        ResetOptionStyles();
    }

    void ResetOptionStyles()
    {
        foreach (var button in Options())
        {
            button.BackgroundColor = Color.FromArgb("#FCFEFF");
            button.TextColor = Color.FromArgb("#10223E");
            button.BorderColor = Color.FromArgb("#D6E2F2");
            button.BorderWidth = 1;
        }
    }

    IEnumerable<Button> Options() => new[] { A0, A1, A2, A3 };

    void SetOptionsEnabled(bool enabled)
    {
        foreach (var button in Options())
        {
            button.IsEnabled = enabled;
        }
    }

    async void OnOptionClicked(object sender, EventArgs e)
    {
        if (_hasAnsweredCurrentQuestion)
        {
            return;
        }

        var clicked = (Button)sender;
        await InteractionEffects.AnimateTapAsync(clicked);
        await GameAudioService.PlayTapAsync();
        var chosenIndex = clicked == A0 ? 0 :
                          clicked == A1 ? 1 :
                          clicked == A2 ? 2 : 3;

        ApplyAnswer(chosenIndex, timedOut: false);
    }

    async void OnPauseClicked(object sender, EventArgs e)
    {
        if (_navigating)
        {
            return;
        }

        if (sender is VisualElement element)
        {
            await InteractionEffects.AnimateTapAsync(element);
        }

        _timerRunning = false;

        var action = await GamePauseService.ShowAsync(
            this,
            "How to play",
            "Answer each IQ question before the timer bar runs out. Correct answers add points based on difficulty, and the quiz advances automatically.");

        switch (action)
        {
            case GamePauseAction.Resume:
                if (!_hasAnsweredCurrentQuestion)
                {
                    StartTimer();
                }
                break;

            case GamePauseAction.Restart:
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    Navigation.InsertPageBefore(new IQGamePage(IQSession.Create(_session.Definition)), this);
                    await Navigation.PopAsync(false);
                });
                break;

            case GamePauseAction.Exit:
                await PageTransitionService.GoToAsync("//tests");
                break;
        }
    }

    async Task AutoAdvanceAsync()
    {
        await Task.Delay(_autoAdvanceDelay);
        await MainThread.InvokeOnMainThreadAsync(GoNextAsync);
    }

    async Task GoNextAsync()
    {
        if (_navigating)
        {
            return;
        }

        _navigating = true;

        try
        {
            Progress.Progress = (_session.Index + 1) / (double)_session.Questions.Count;

            if (_session.IsLast)
            {
                _session.CompleteByTimeout();
                await NavigateToResultsAsync();
                return;
            }

            _session.Next();
            Render();
        }
        finally
        {
            _navigating = false;
        }
    }

    Task NavigateToResultsAsync()
    {
        _timerRunning = false;
        return PageTransitionService.PushAsync(Navigation, new IQResultsPage(_session));
    }

    async Task HandleQuestionTimeoutAsync()
    {
        if (_hasAnsweredCurrentQuestion || _navigating)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => ApplyAnswer(-1, timedOut: true));
    }

    void ApplyAnswer(int chosenIndex, bool timedOut)
    {
        if (_hasAnsweredCurrentQuestion)
        {
            return;
        }

        _hasAnsweredCurrentQuestion = true;
        var result = _session.SubmitAnswer(chosenIndex);

        ResetOptionStyles();
        SetOptionsEnabled(false);

        var correctButton = Options().ElementAt(result.CorrectIndex);
        correctButton.BackgroundColor = Color.FromArgb("#DCFCE7");
        correctButton.TextColor = Color.FromArgb("#166534");
        correctButton.BorderColor = Color.FromArgb("#22C55E");

        if (!timedOut && chosenIndex >= 0)
        {
            var selectedButton = Options().ElementAt(chosenIndex);

            if (result.IsCorrect)
            {
                selectedButton.BackgroundColor = Color.FromArgb("#DCFCE7");
                selectedButton.TextColor = Color.FromArgb("#166534");
                selectedButton.BorderColor = Color.FromArgb("#22C55E");
            }
            else
            {
                selectedButton.BackgroundColor = Color.FromArgb("#FDE2E2");
                selectedButton.TextColor = Color.FromArgb("#991B1B");
                selectedButton.BorderColor = Color.FromArgb("#EF4444");
            }
        }

        FeedbackTitle.Text = timedOut
            ? "Time's up"
            : result.IsCorrect ? "Correct answer" : "Not quite";
        PointsEarnedLabel.Text = result.IsCorrect
            ? $"+{result.EarnedPoints} points"
            : timedOut
                ? "No answer submitted in 20 seconds"
                : $"This question was worth {result.PossiblePoints} points";
        PointsEarnedLabel.TextColor = result.IsCorrect
            ? Color.FromArgb("#188038")
            : Color.FromArgb("#B45309");
        ExplanationLabel.Text = result.Explanation;
        FeedbackCard.IsVisible = true;
        ScoreLabel.Text = $"{_session.CurrentScore} pts";

        _ = AutoAdvanceAsync();
    }
}
