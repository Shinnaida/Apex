namespace Peak;

public partial class TrueColorInsightsPage : ContentPage
{
    readonly int _score;
    readonly int _correctAnswers;
    readonly int _wrongAnswers;
    readonly int _fastestReactionMs;
    readonly int _longestStreak;
    int _nextRankScore;

    public TrueColorInsightsPage(int score, int correctAnswers, int wrongAnswers, int fastestReactionMs, int longestStreak)
    {
        InitializeComponent();
        ChartPlot.SizeChanged += (_, _) => UpdateChart();

        _score = score;
        _correctAnswers = correctAnswers;
        _wrongAnswers = wrongAnswers;
        _fastestReactionMs = fastestReactionMs;
        _longestStreak = longestStreak;

        ApplyInsights();
    }

    void ApplyInsights()
    {
        _nextRankScore = _score < TrueColorProgress.NoviceThreshold
            ? TrueColorProgress.NoviceThreshold
            : _score < TrueColorProgress.SkilledThreshold
                ? TrueColorProgress.SkilledThreshold
                : TrueColorProgress.ExpertThreshold;

        ScorePillLabel.Text = _score.ToString();
        RankUpLabel.Text = $"Rank up: {_nextRankScore}";
        StreakValueLabel.Text = _longestStreak.ToString();
        FastestValueLabel.Text = _fastestReactionMs > 0 ? $"{_fastestReactionMs} ms" : "--";

        InsightBubbleLabel.Text = _wrongAnswers == 0
            ? $"Perfect control. You cleared {_correctAnswers} prompts without biting on any false color traps."
            : $"You solved {_correctAnswers} prompts, missed {_wrongAnswers}, and hit a best streak of {_longestStreak}. Keep trusting the ink instead of the word.";

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
        TrendLine.Rotation = -8 - (performanceRatio * 7);

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
        await PageTransitionService.PushAsync(Navigation, () => new TrueColorGamePage());
        Navigation.RemovePage(this);
    }
}
