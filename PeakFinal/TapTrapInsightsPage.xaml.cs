namespace Peak;

public partial class TapTrapInsightsPage : ContentPage
{
    readonly int _score;
    readonly int _correctTaps;
    readonly int _wrongTaps;
    readonly int _comboPairs;
    readonly int _longestStreak;
    int _nextRankScore;

    public TapTrapInsightsPage(int score, int correctTaps, int wrongTaps, int comboPairs, int longestStreak)
    {
        InitializeComponent();
        ChartPlot.SizeChanged += (_, _) => UpdateChart();

        _score = score;
        _correctTaps = correctTaps;
        _wrongTaps = wrongTaps;
        _comboPairs = comboPairs;
        _longestStreak = longestStreak;

        ApplyInsights();
    }

    void ApplyInsights()
    {
        _nextRankScore = _score < TapTrapProgress.NoviceThreshold
            ? TapTrapProgress.NoviceThreshold
            : _score < TapTrapProgress.SkilledThreshold
                ? TapTrapProgress.SkilledThreshold
                : TapTrapProgress.ExpertThreshold;

        ScorePillLabel.Text = _score.ToString();
        RankUpLabel.Text = $"Rank up: {_nextRankScore}";
        StreakValueLabel.Text = _longestStreak.ToString();
        ComboValueLabel.Text = _comboPairs.ToString();

        InsightBubbleLabel.Text = _wrongTaps == 0
            ? $"Clean run. You landed {_correctTaps} correct taps with {_comboPairs} combo bursts."
            : $"You landed {_correctTaps} correct taps, missed {_wrongTaps}, and chained {_comboPairs} combo bursts with a best streak of {_longestStreak}.";

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
        await PageTransitionService.PushAsync(Navigation, () => new TapTrapGamePage());
        Navigation.RemovePage(this);
    }
}
