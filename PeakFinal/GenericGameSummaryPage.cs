using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public sealed class GenericGameSummaryPage : ContentPage
{
    readonly Func<Page?>? _playAgainFactory;
    readonly Grid _rootLayout;
    bool _didCelebrate;

    public GenericGameSummaryPage(
        string gameTitle,
        int score,
        int bestScore,
        int apexPoints,
        bool isNewBest,
        Func<Page?>? playAgainFactory = null,
        string? accentHex = null,
        string? secondaryLabel = null,
        string? secondaryValue = null)
    {
        _playAgainFactory = playAgainFactory;

        var accent = Color.FromArgb(string.IsNullOrWhiteSpace(accentHex) ? "#FF4E79" : accentHex);
        var softPanel = Color.FromArgb("#FFF2F6");
        var panelStroke = Color.FromArgb("#ECD8DE");
        var pageBackground = Color.FromArgb("#F8F5F7");
        var secondaryCardLabel = string.IsNullOrWhiteSpace(secondaryLabel) ? "Apex Points" : secondaryLabel!;
        var secondaryCardValue = string.IsNullOrWhiteSpace(secondaryValue) ? $"+{apexPoints}" : secondaryValue!;

        BackgroundColor = pageBackground;
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);

        var trophyLabel = new Label
        {
            Text = "\U0001F3C6",
            FontSize = 68,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = accent
        };

        var badgeLabel = new Label
        {
            Text = isNewBest ? "NEW BEST SCORE" : $"{gameTitle.ToUpperInvariant()} COMPLETE",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = accent
        };

        var scoreValueLabel = new Label
        {
            Text = score.ToString(),
            FontSize = 52,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb("#2E2E2E")
        };

        var summaryCard = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = panelStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(20, 18),
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#14C69AA8")),
                Offset = new Point(0, 10),
                Opacity = 0.08f,
                Radius = 20
            }
        };

        var detailsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        detailsGrid.Add(BuildStatCard("Best", bestScore.ToString(), softPanel, Color.FromArgb("#A35A6E"), accent), 0, 0);
        detailsGrid.Add(BuildStatCard(secondaryCardLabel, secondaryCardValue, softPanel, Color.FromArgb("#A35A6E"), accent), 1, 0);

        var noteLabel = new Label
        {
            Text = isNewBest
                ? $"New personal best unlocked. +{apexPoints} AP earned this run."
                : $"Nice run. +{apexPoints} AP earned. Keep going to beat your best.",
            FontSize = 14,
            TextColor = Color.FromArgb("#6B5660"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        summaryCard.Content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                badgeLabel,
                scoreValueLabel,
                detailsGrid,
                noteLabel
            }
        };

        var playAgainButton = new Button
        {
            Text = "Play Again",
            BackgroundColor = accent,
            TextColor = Colors.White,
            CornerRadius = 24,
            HeightRequest = 52,
            WidthRequest = 320,
            FontAttributes = FontAttributes.Bold
        };
        playAgainButton.Clicked += OnPlayAgainClicked;

        var doneButton = new Button
        {
            Text = "Done",
            BackgroundColor = Color.FromArgb("#EEF1F2"),
            TextColor = Color.FromArgb("#31433F"),
            CornerRadius = 24,
            HeightRequest = 52,
            WidthRequest = 320,
            FontAttributes = FontAttributes.Bold
        };
        doneButton.Clicked += OnDoneClicked;

        _rootLayout = new Grid();

        var contentView = new Grid
        {
            Padding = new Thickness(24, 42, 24, 24),
            Children =
            {
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        VerticalOptions = LayoutOptions.Center,
                        Spacing = 18,
                        Children =
                        {
                            trophyLabel,
                            summaryCard,
                            playAgainButton,
                            doneButton
                        }
                    }
                }
            }
        };

        _rootLayout.Children.Add(contentView);
        Content = _rootLayout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_didCelebrate)
        {
            return;
        }

        _didCelebrate = true;
        await CelebrationService.RunConfettiAsync(_rootLayout);
    }

    static Border BuildStatCard(string label, string value, Color background, Color labelColor, Color valueColor)
    {
        return new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 10),
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = label,
                        FontSize = 12,
                        TextColor = labelColor
                    },
                    new Label
                    {
                        Text = value,
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = valueColor
                    }
                }
            }
        };
    }

    async void OnPlayAgainClicked(object? sender, EventArgs e)
    {
        if (_playAgainFactory is null)
        {
            await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
            return;
        }

        var stack = Navigation.NavigationStack;
        var previousPage = stack.Count >= 2 ? stack[^2] : null;
        if (previousPage is not null)
        {
            Navigation.RemovePage(previousPage);
        }

        await PageTransitionService.PopAsync(Navigation);
        await PageTransitionService.PushAsync(Navigation, _playAgainFactory);
    }

    async void OnDoneClicked(object? sender, EventArgs e)
    {
        var stack = Navigation.NavigationStack;
        var previousPage = stack.Count >= 2 ? stack[^2] : null;
        var summaryPage = stack.Count >= 3 ? stack[^3] : null;

        if (previousPage is not null && IsGamePage(previousPage) && summaryPage is not null)
        {
            Navigation.RemovePage(previousPage);
            await PageTransitionService.PopAsync(Navigation);
            return;
        }

        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }

    static bool IsGamePage(Page page)
    {
        var name = page.GetType().Name;
        return name.EndsWith("GamePage", StringComparison.Ordinal)
               || string.Equals(name, "GamePlayPage", StringComparison.Ordinal);
    }
}
