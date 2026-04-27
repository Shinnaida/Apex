namespace Peak;

public partial class PerilousPathPage : ContentPage
{
    bool _isHowToPlayOpen;
    bool _isAnimating;

    public PerilousPathPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        int bestScore = BrainScoreService.GetGamePerformance("perilous_path")?.BestScore ?? 0;
        BestScoreValueLabel.Text = bestScore.ToString();
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        if (_isAnimating)
            return;

        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnPlayClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new PerilousPathGamePage());
    }

    async void OnHowToPlayClicked(object sender, EventArgs e)
    {
        if (_isAnimating || _isHowToPlayOpen)
            return;

        _isAnimating = true;
        _isHowToPlayOpen = true;

        HowToPlayOverlay.IsVisible = true;
        HowToPlayOverlay.Opacity = 0;
        HowToPlayPanel.TranslationY = 500;

        await Task.WhenAll(
            HowToPlayOverlay.FadeTo(1, 220, Easing.CubicOut),
            HowToPlayPanel.TranslateTo(0, 0, 280, Easing.CubicOut)
        );

        _isAnimating = false;
    }

    async void OnCloseHowToPlayClicked(object sender, EventArgs e)
    {
        await CloseHowToPlayAsync();
    }

    async void OnOverlayTapped(object sender, TappedEventArgs e)
    {
        await CloseHowToPlayAsync();
    }

    async Task CloseHowToPlayAsync()
    {
        if (_isAnimating || !_isHowToPlayOpen)
            return;

        _isAnimating = true;

        await Task.WhenAll(
            HowToPlayOverlay.FadeTo(0, 180, Easing.CubicIn),
            HowToPlayPanel.TranslateTo(0, 500, 220, Easing.CubicIn)
        );

        HowToPlayOverlay.IsVisible = false;
        _isHowToPlayOpen = false;
        _isAnimating = false;
    }
}

