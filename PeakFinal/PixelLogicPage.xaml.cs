namespace Peak;

public partial class PixelLogicPage : ContentPage
{
    bool _isHowToPlayOpen;
    bool _isAnimating;

    public PixelLogicPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var performance = BrainScoreService.GetGamePerformance("pixel_logic");
        var bestScore = performance?.BestScore ?? 0;

        BestScoreLabel.Text = bestScore.ToString();
        BestRankLabel.Text = ResolveRank(bestScore);
        ChallengeLabel.Text = ResolveChallenge(bestScore);
    }

    static string ResolveRank(int bestScore)
    {
        if (bestScore >= 1200) return "Expert";
        if (bestScore >= 900) return "Skilled";
        if (bestScore >= 660) return "Novice";
        return "Beginner";
    }

    static string ResolveChallenge(int bestScore)
    {
        if (bestScore >= 1200) return "You've reached the top Pixel Logic rank.";
        if (bestScore >= 900) return "Score above 1200 to rank up to Expert";
        if (bestScore >= 660) return "Score above 900 to rank up to Skilled";
        return "Score above 660 to rank up to Novice";
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        if (_isAnimating) return;
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnPlayClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new PixelLogicGamePage());
    }

    async void OnHowToPlayClicked(object sender, EventArgs e)
    {
        if (_isAnimating || _isHowToPlayOpen) return;

        _isAnimating = true;
        _isHowToPlayOpen = true;
        HowToPlayOverlay.IsVisible = true;
        HowToPlayOverlay.Opacity = 0;
        HowToPlayPanel.TranslationY = 500;

        await Task.WhenAll(
            HowToPlayOverlay.FadeTo(1, 220, Easing.CubicOut),
            HowToPlayPanel.TranslateTo(0, 0, 280, Easing.CubicOut));

        _isAnimating = false;
    }

    async void OnCloseHowToPlayClicked(object sender, TappedEventArgs e)
    {
        await CloseHowToPlayAsync();
    }

    async void OnOverlayTapped(object sender, TappedEventArgs e)
    {
        await CloseHowToPlayAsync();
    }

    async Task CloseHowToPlayAsync()
    {
        if (_isAnimating || !_isHowToPlayOpen) return;

        _isAnimating = true;
        await Task.WhenAll(
            HowToPlayOverlay.FadeTo(0, 180, Easing.CubicIn),
            HowToPlayPanel.TranslateTo(0, 500, 220, Easing.CubicIn));

        HowToPlayOverlay.IsVisible = false;
        _isHowToPlayOpen = false;
        _isAnimating = false;
    }
}
