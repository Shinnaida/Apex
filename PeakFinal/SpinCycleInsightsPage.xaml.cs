namespace Peak;

public partial class SpinCycleInsightsPage : ContentPage
{
    readonly int _score;
    readonly int _longestStreak;
    readonly int _multiplier;

    public SpinCycleInsightsPage(int score, int longestStreak, int multiplier)
    {
        InitializeComponent();

        _score = score;
        _longestStreak = longestStreak;
        _multiplier = multiplier;

        ApplyInsights();
    }

    void ApplyInsights()
    {
        ChartScoreLabel.Text = _score.ToString();
        RankUpLabel.Text = "Rank up: 560";

        int clampedStreak = Math.Clamp(_longestStreak, 0, 12);
        int averageStreak = 7;

        YouBar.HeightRequest = 22 + (clampedStreak * 10);
        AvgBar.HeightRequest = 22 + (averageStreak * 10);

        int percentile = Math.Clamp(12 + (clampedStreak * 2), 5, 95);
        InsightLabel.Text = $"Your longest streak of correct answers in a row was {clampedStreak}. You're doing better than {percentile}% of users at this rank and you reached x{Math.Max(1, _multiplier)} multiplier.";
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }

    async void OnReplayClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new SpinCycleGamePage());
        Navigation.RemovePage(this);
    }
}

