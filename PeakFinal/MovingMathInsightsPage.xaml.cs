namespace Peak;

public partial class MovingMathInsightsPage : ContentPage
{
    readonly int _score;
    readonly int _correctAnswers;
    readonly int _wrongAnswers;
    readonly int _longestStreak;
    int _nextRankScore;

    public MovingMathInsightsPage(int score, int correctAnswers, int wrongAnswers, int longestStreak)
    {
        InitializeComponent();
        ChartPlot.SizeChanged += (_, _) => UpdateChart();

        _score = score;
        _correctAnswers = correctAnswers;
        _wrongAnswers = wrongAnswers;
        _longestStreak = longestStreak;

        ApplyInsights();
    }

    void ApplyInsights()
    {
        _nextRankScore = _score < MovingMathProgress.NoviceThreshold
            ? MovingMathProgress.NoviceThreshold
            : _score < MovingMathProgress.SkilledThreshold
                ? MovingMathProgress.SkilledThreshold
                : MovingMathProgress.ExpertThreshold;

        ScorePillLabel.Text = _score.ToString();
        RankUpLabel.Text = $"Rank up: {_nextRankScore}";

        InsightBubbleLabel.Text = _wrongAnswers == 0
            ? $"Clean run. You solved {_correctAnswers} equations with a longest streak of {_longestStreak}."
            : $"You solved {_correctAnswers} equations, missed {_wrongAnswers}, and hit a best streak of {_longestStreak}.";

        UpdateChart();
    }

    void UpdateChart()
    {
        if (ChartPlot.Width <= 0 || ChartPlot.Height <= 0)
        {
            return;
        }

        double performanceRatio = Math.Clamp(_score / (double)Math.Max(_nextRankScore, 1), 0.12, 1);
        double availableWidth = Math.Max(ChartPlot.Width - 24, 120);
        TrendLine.WidthRequest = Math.Clamp((availableWidth * 0.64) + (availableWidth * 0.24 * performanceRatio), 110, availableWidth);
        TrendLine.Rotation = -8 - (performanceRatio * 8);

        double endOffset = 16 + ((1 - performanceRatio) * Math.Max(ChartPlot.Height * 0.28, 18));
        EndPoint.Margin = new Thickness(0, endOffset, 0, 0);
        ScorePill.Margin = new Thickness(0, endOffset + 10, 0, 0);
        RankUpGuide.Opacity = _score >= _nextRankScore ? 0.25 : 0.8;
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }

    async void OnReplayClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new MovingMathGamePage());
        Navigation.RemovePage(this);
    }
}
