namespace Peak;

public partial class WordALikeResultPage : ContentPage
{
    public WordALikeResultPage(int finalScore, int bestScore, bool isNewBest)
    {
        InitializeComponent();

        FinalScoreLabel.Text = finalScore.ToString();
        BestScoreLabel.Text = $"Best score: {bestScore}";

        if (!isNewBest)
        {
            BestBannerLabel.Text = "ROUND COMPLETE";
            BestBanner.BackgroundColor = Color.FromArgb("#B8B8B8");
        }
    }

    async void OnContinueClicked(object sender, EventArgs e)
    {
        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }
}
