namespace Peak;

public partial class MatchaMadnessInsightsPage : ContentPage
{
    private readonly int _score;
    private readonly int _bonusAwarded;
    private readonly int _matchCount;
    private int _nextRankScore;

    public MatchaMadnessInsightsPage(int score, int bonusAwarded, int matchCount)
    {
        InitializeComponent();
        ChartPlot.SizeChanged += (_, _) => UpdateChart();
        _score = score;
        _bonusAwarded = bonusAwarded;
        _matchCount = matchCount;
        ApplyInsights();
    }

    private void ApplyInsights()
    {
        _nextRankScore = _score < 1300 ? 1300 : 1600;
        ScorePillLabel.Text = _score.ToString();
        RankUpLabel.Text = $"Rank up: {_nextRankScore}";

        InsightBubbleLabel.Text = _bonusAwarded > 0
            ? $"Smooth finish. You matched {_matchCount} pairs and banked {_bonusAwarded} bonus points."
            : $"You matched {_matchCount} pairs. A quicker clear next time will let you cash in more bonus points.";

        UpdateChart();
    }

    private void UpdateChart()
    {
        if (ChartPlot.Width <= 0 || ChartPlot.Height <= 0)
        {
            return;
        }

        var performanceRatio = Math.Clamp(_score / (double)Math.Max(_nextRankScore, 1), 0.12, 1);
        var availableWidth = Math.Max(ChartPlot.Width - 24, 120);
        TrendLine.WidthRequest = Math.Clamp((availableWidth * 0.64) + (availableWidth * 0.24 * performanceRatio), 110, availableWidth);
        TrendLine.Rotation = -8 - (performanceRatio * 8);

        var endOffset = 16 + ((1 - performanceRatio) * Math.Max(ChartPlot.Height * 0.28, 18));
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
        await PageTransitionService.PushAsync(Navigation, () => new MatchaMadnessGamePage());
        Navigation.RemovePage(this);
    }
}
