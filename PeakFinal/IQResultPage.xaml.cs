namespace Peak;

public partial class IQResultsPage : ContentPage
{
    readonly IQSession _session;
    bool _scoresRecorded;
    int _peakScore;

    public IQResultsPage(IQSession session)
    {
        InitializeComponent();
        _session = session;

        LoadResults();
    }

    async void LoadResults()
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
        await PageTransitionService.GoToAsync("//tests");
    }

    async void OnPlayAgainClicked(object sender, EventArgs e)
    {
        await InteractionEffects.AnimateTapAsync(PlayAgainButton);
        await PageTransitionService.PushAsync(Navigation, () => new IQGamePage(IQSession.Create(_session.Definition)));
    }
}
