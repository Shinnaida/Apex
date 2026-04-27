namespace Peak;

public partial class IQResultsPage : ContentPage
{
    readonly IQSession _session;
    readonly bool _requiresAccountPrompt;
    bool _hasLoaded;
    bool _scoresRecorded;
    int _peakScore;

    public IQResultsPage(IQSession session)
    {
        InitializeComponent();
        _session = session;
        _requiresAccountPrompt = !LocalAccountStore.IsSignedIn;
        ApplyActionState();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadResultsAsync();
    }

    void ApplyActionState()
    {
        if (!_requiresAccountPrompt)
        {
            GuestPromptCard.IsVisible = false;
            BackButton.Text = "BACK TO TESTS";
            PlayAgainButton.Text = "PLAY AGAIN";
            PlayAgainButton.BackgroundColor = Color.FromArgb("#58D95E");
            return;
        }

        GuestPromptCard.IsVisible = true;
        BackButton.Text = "LOG IN";
        PlayAgainButton.Text = "SIGN UP";
        PlayAgainButton.BackgroundColor = Color.FromArgb("#2D75F0");
    }

    async Task LoadResultsAsync()
    {
        OverallBar.Progress = 0;
        LogicBar.Progress = 0;
        VerbalBar.Progress = 0;
        AbstractBar.Progress = 0;
        SpatialBar.Progress = 0;
        ScienceBar.Progress = 0;
        HistoryBar.Progress = 0;

        _peakScore = _session.GetPeakScore();
        var overallPercent = _session.GetOverallNormalized();
        var rankInfo = BrainScoreService.GetPeakRankInfo(_peakScore);

        OverallScoreLabel.Text = $"{_peakScore}/1000";
        CorrectCountLabel.Text = $"{_session.CorrectCount}/{_session.Questions.Count}";
        RawPointsLabel.Text = $"{_session.CurrentScore}/{_session.MaximumPossibleScore}";
        TimeUsedLabel.Text = $"{(int)_session.ElapsedTime.TotalMinutes}:{_session.ElapsedTime.Seconds:00}";
        SummaryLabel.Text = $"Finished {_session.Definition.Title.ToLowerInvariant()} with weighted scoring across every category.";
        BestScoreCalloutLabel.Text = _peakScore.ToString();
        RankCalloutLabel.Text = rankInfo.Name;

        if (_requiresAccountPrompt)
        {
            SummaryLabel.Text = "Your score summary is ready. Log in or sign up next to keep this result on your profile.";
            GuestPromptLabel.Text = $"Keep {_peakScore}/1000, {_session.CorrectCount}/{_session.Questions.Count} correct, and your updated brain stats.";
        }

        await OverallBar.ProgressTo(overallPercent, 650, Easing.CubicOut);

        await SetCategoryRowAsync(IQCategory.LogicMath, LogicScore, LogicBar);
        await SetCategoryRowAsync(IQCategory.Verbal, VerbalScore, VerbalBar);
        await SetCategoryRowAsync(IQCategory.Abstract, AbstractScore, AbstractBar);
        await SetCategoryRowAsync(IQCategory.Spatial, SpatialScore, SpatialBar);
        await SetCategoryRowAsync(IQCategory.Science, ScienceScore, ScienceBar);
        await SetCategoryRowAsync(IQCategory.PhilippineHistory, HistoryScore, HistoryBar);

        if (!_scoresRecorded)
        {
            BrainScoreService.RecordIqSnapshot(
                memory: _session.GetMemoryNormalized(),
                problemSolving: _session.GetProblemSolvingNormalized(),
                language: _session.GetLanguageNormalized(),
                focus: _session.GetFocusNormalized());
            _scoresRecorded = true;
        }

        if (_requiresAccountPrompt)
        {
            PendingIqSummaryService.Store(new PendingIqSummary(
                TestTitle: _session.Definition.Title,
                PeakScore: _peakScore,
                CorrectCount: _session.CorrectCount,
                QuestionCount: _session.Questions.Count,
                Memory: _session.GetMemoryNormalized(),
                ProblemSolving: _session.GetProblemSolvingNormalized(),
                Language: _session.GetLanguageNormalized(),
                Focus: _session.GetFocusNormalized()));
        }

        await CelebrationService.RunConfettiAsync(ConfettiHost);
    }

    async Task SetCategoryRowAsync(IQCategory category, Label scoreLabel, ProgressBar bar)
    {
        var normalized = _session.GetCategoryNormalized(category);
        scoreLabel.Text = _session.GetCategoryDisplayScore(category).ToString();
        await bar.ProgressTo(normalized, 500, Easing.CubicOut);
    }

    async void OnBackToTestsClicked(object sender, EventArgs e)
    {
        await InteractionEffects.AnimateTapAsync(BackButton);

        if (_requiresAccountPrompt)
        {
            await PageTransitionService.PushAsync(Navigation, new AccountAccessPage(AccountAccessStartMode.Login));
            return;
        }

        await PageTransitionService.GoToAsync("//tests");
    }

    async void OnPlayAgainClicked(object sender, EventArgs e)
    {
        await InteractionEffects.AnimateTapAsync(PlayAgainButton);

        if (_requiresAccountPrompt)
        {
            await PageTransitionService.PushAsync(Navigation, new AccountAccessPage(AccountAccessStartMode.Signup));
            return;
        }

        await PageTransitionService.PushAsync(Navigation, () => new IQGamePage(IQSession.Create(_session.Definition)));
    }
}
