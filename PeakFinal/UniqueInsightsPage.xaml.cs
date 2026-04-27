namespace Peak;

public partial class UniqueInsightsPage : ContentPage
{
    readonly int _score;
    readonly int _boardsSolved;
    readonly int _fastestReactionMs;
    readonly int _longestStreak;
    readonly int _wrongTaps;
    int _nextRankScore;

    public UniqueInsightsPage(int score, int boardsSolved, int fastestReactionMs, int longestStreak, int wrongTaps)
    {
        InitializeComponent();
        ChartPlot.SizeChanged += (_, _) => UpdateChart();

        _score = score;
        _boardsSolved = boardsSolved;
        _fastestReactionMs = fastestReactionMs;
        _wrongTaps = wrongTaps;
        _longestStreak = longestStreak;

        ApplyInsights();
    }

    void ApplyInsights()
    {
        _nextRankScore = _score < UniqueProgress.NoviceThreshold
            ? UniqueProgress.NoviceThreshold
            : _score < UniqueProgress.SkilledThreshold
                ? UniqueProgress.SkilledThreshold
                : UniqueProgress.ExpertThreshold;

        ScorePillLabel.Text = _score.ToString();
        RankUpLabel.Text = $"Rank up: {_nextRankScore}";
        SolvedValueLabel.Text = _boardsSolved.ToString();
        FastestValueLabel.Text = _fastestReactionMs > 0 ? $"{_fastestReactionMs} ms" : "--";

        InsightBubbleLabel.Text = _wrongTaps == 0
            ? $"Clean scan. You cleared {_boardsSolved} boards and hit a fastest find of {FastestValueLabel.Text.ToLowerInvariant()}."
            : $"You cleared {_boardsSolved} boards, reached a best streak of {_longestStreak}, and had {_wrongTaps} misses.";

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
        await PageTransitionService.PushAsync(Navigation, () => new UniqueGamePage());
        Navigation.RemovePage(this);
    }
}
