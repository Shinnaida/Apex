using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class MatchaMadnessPage : ContentPage
{
    bool _isHowToPlayOpen;
    bool _isAnimating;

    public MatchaMadnessPage()
    {
        InitializeComponent();
        BuildPreviewBoard();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var performance = BrainScoreService.GetGamePerformance("matcha_madness");
        var bestScore = performance?.BestScore ?? 0;

        BestScoreLabel.Text = bestScore.ToString();
        TopScoreLabel.Text = bestScore.ToString();

        if (performance?.TopSessions.Count > 0)
        {
            var latest = performance.TopSessions
                .OrderByDescending(session => session.PlayedUtc)
                .First();
            var top = performance.TopSessions[0];

            RecentScoreLabel.Text = latest.Score.ToString();
            RecentTimeLabel.Text = ToRelativeTime(latest.PlayedUtc);
            TopScoreLabel.Text = top.Score.ToString();
            TopTimeLabel.Text = ToRelativeTime(top.PlayedUtc);
        }
        else
        {
            RecentScoreLabel.Text = "0";
            RecentTimeLabel.Text = "No plays yet";
            TopScoreLabel.Text = "0";
            TopTimeLabel.Text = "No plays yet";
        }

        BestRankLabel.Text = ResolveRank(bestScore);
        ChallengeLabel.Text = ResolveChallenge(bestScore);
    }

    private void BuildPreviewBoard()
    {
        PreviewBoard.Children.Clear();

        for (var index = 0; index < 12; index++)
        {
            var cell = new Border
            {
                BackgroundColor = Color.FromArgb("#75C77E"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                Padding = 2,
                Content = MatchaMadnessVisuals.CreatePatternView(MatchaMadnessVisuals.GetPreviewStack(index), 62)
            };

            Grid.SetRow(cell, index / 3);
            Grid.SetColumn(cell, index % 3);
            PreviewBoard.Children.Add(cell);
        }
    }

    private static string ResolveRank(int bestScore)
    {
        if (bestScore >= 1900)
        {
            return "Expert";
        }

        if (bestScore >= 1600)
        {
            return "Skilled";
        }

        if (bestScore >= 1300)
        {
            return "Novice";
        }

        return "Beginner";
    }

    private static string ResolveChallenge(int bestScore)
    {
        if (bestScore >= 1900)
        {
            return "You've reached the top Matcha Madness rank.";
        }

        if (bestScore >= 1600)
        {
            return "Score above 1900 to rank up to Expert";
        }

        if (bestScore >= 1300)
        {
            return "Score above 1600 to rank up to Skilled";
        }

        return "Score above 1300 to rank up to Novice";
    }

    private static string ToRelativeTime(DateTime playedUtc)
    {
        var local = playedUtc.ToLocalTime();
        var diff = DateTime.Now - local;

        if (diff.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (diff.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)diff.TotalMinutes)}m ago";
        }

        if (diff.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)diff.TotalHours)}h ago";
        }

        if (diff.TotalDays < 7)
        {
            return $"{Math.Max(1, (int)diff.TotalDays)}d ago";
        }

        return local.ToString("dd/MM/yyyy");
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        if (_isAnimating)
        {
            return;
        }

        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnPlayClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new MatchaMadnessGamePage());
    }

    async void OnHowToPlayClicked(object sender, EventArgs e)
    {
        if (_isAnimating || _isHowToPlayOpen)
        {
            return;
        }

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
        {
            return;
        }

        _isAnimating = true;

        await Task.WhenAll(
            HowToPlayOverlay.FadeTo(0, 180, Easing.CubicIn),
            HowToPlayPanel.TranslateTo(0, 500, 220, Easing.CubicIn));

        HowToPlayOverlay.IsVisible = false;
        _isHowToPlayOpen = false;
        _isAnimating = false;
    }
}
