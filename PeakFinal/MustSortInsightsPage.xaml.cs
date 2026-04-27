namespace Peak;

public partial class MustSortInsightsPage : ContentPage
{
    readonly int _score;
    readonly int _greySorted;
    readonly int _stopHeld;
    readonly int _longestStreak;
    readonly int _misses;
    int _nextRankScore;

    public MustSortInsightsPage(int score, int greySorted, int stopHeld, int longestStreak, int misses)
    {
        InitializeComponent();
        ChartPlot.SizeChanged += (_, _) => UpdateChart();

        _score = score;
        _greySorted = greySorted;
        _stopHeld = stopHeld;
        _longestStreak = longestStreak;
        _misses = misses;

        ApplyInsights();
    }

    void ApplyInsights()
    {
        _nextRankScore = _score < MustSortProgress.NoviceThreshold
            ? MustSortProgress.NoviceThreshold
            : _score < MustSortProgress.SkilledThreshold
                ? MustSortProgress.SkilledThreshold
                : MustSortProgress.ExpertThreshold;

        ScorePillLabel.Text = _score.ToString();
        RankUpLabel.Text = $"Rank up: {_nextRankScore}";
        GreyValueLabel.Text = _greySorted.ToString();
        StopValueLabel.Text = _stopHeld.ToString();

        InsightBubbleLabel.Text = _misses == 0
            ? $"Clean run. You handled {_greySorted} grey cards and held {_stopHeld} stop cards with a best streak of {_longestStreak}."
            : $"You held {_stopHeld} stop cards, solved {_greySorted} grey cards, and your best streak was {_longestStreak} with {_misses} misses.";

        UpdateChart();
    }

    void UpdateChart()
    {
        if (ChartPlot.Width <= 0 || ChartPlot.Height <= 0)
        {
            return;
        }

        double performanceRatio = Math.Clamp(_score / (double)Math.Max(_nextRankScore, 1), 0.16, 1);
        double availableWidth = Math.Max(ChartPlot.Width - 24, 120);
        TrendLine.WidthRequest = Math.Clamp((availableWidth * 0.64) + (availableWidth * 0.24 * performanceRatio), 110, availableWidth);
        TrendLine.Rotation = -8 - (performanceRatio * 8);

        double endOffset = 16 + ((1 - performanceRatio) * Math.Max(ChartPlot.Height * 0.28, 18));
        EndPoint.Margin = new Thickness(0, endOffset, 0, 0);
        ScorePill.Margin = new Thickness(0, endOffset + 10, 0, 0);
        RankUpGuide.Opacity = _score >= _nextRankScore ? 0.28 : 0.82;
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }

    async void OnReplayClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new MustSortGamePage());
        Navigation.RemovePage(this);
    }
}
