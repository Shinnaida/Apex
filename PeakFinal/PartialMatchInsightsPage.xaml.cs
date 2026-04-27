namespace Peak;

public partial class PartialMatchInsightsPage : ContentPage
{
    readonly int _score;
    readonly int _longestStreak;

    public PartialMatchInsightsPage(int score, int longestStreak)
    {
        InitializeComponent();

        _score = score;
        _longestStreak = longestStreak;

        ApplyInsights();
    }

    void ApplyInsights()
    {
        ChartScoreLabel.Text = _score.ToString();

        // Keep this close to the captured app behavior where rank-up target is fixed at this stage.
        RankUpLabel.Text = "Rank up: 770";

        int clampedStreak = Math.Clamp(_longestStreak, 0, 12);
        int averageStreak = 6;

        YouBar.HeightRequest = 22 + (clampedStreak * 10);
        AvgBar.HeightRequest = 22 + (averageStreak * 10);

        int percentile = Math.Clamp(35 + (clampedStreak * 2), 5, 95);
        InsightLabel.Text = $"Your longest streak of correct answers in a row was {clampedStreak}. You're doing better than {percentile}% of users at this rank but there's still plenty of room for improvement.";
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }

    async void OnReplayClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new PartialMatchGamePage());
    }
}

