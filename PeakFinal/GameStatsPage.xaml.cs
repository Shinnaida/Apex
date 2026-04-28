using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class GamesStatsPage : ContentPage
{
    private sealed record GameStatsCardModel(
        string SourceId,
        string Title,
        string ScoreText,
        string LastPlayedText,
        string Description,
        Brush BackgroundBrush,
        Color PillColor,
        Color ChipColor,
        Color TextColor);

    private static readonly Dictionary<BrainSkill, string> SkillHeadings = new()
    {
        [BrainSkill.Language] = "LANGUAGE",
        [BrainSkill.Memory] = "MEMORY",
        [BrainSkill.ProblemSolving] = "PROBLEM SOLVING",
        [BrainSkill.Focus] = "FOCUS"
    };

    public GamesStatsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            RenderPlayedGames();
        }
        catch
        {
            GamesSectionsHost.Children.Clear();
            EmptyStateCard.IsVisible = true;
        }
    }

    async void OnBrainTapped(object sender, EventArgs e)
    {
        if (Shell.Current is not null)
        {
            await PageTransitionService.GoToAsync("//stats");
            return;
        }

        await PageTransitionService.PushAsync(Navigation, new StatsPage());
    }

    async void OnOverTimeTapped(object sender, EventArgs e)
    {
        await PageTransitionService.GoToAsync(nameof(OverTimePage));
    }

    void OnGamesTapped(object sender, EventArgs e)
    {
        // Already here.
    }

    async void OnLeaderboardsTapped(object sender, EventArgs e)
    {
        await PageTransitionService.GoToAsync(nameof(LeaderboardsPage));
    }

    private void RenderPlayedGames()
    {
        GamesSectionsHost.Children.Clear();

        var playedGames = BrainScoreService.GetPlayedGameScores()
            .Select(CreateCardModel)
            .GroupBy(item => item.skill, item => item.card)
            .OrderBy(group => GetSkillOrder(group.Key))
            .ToList();

        EmptyStateCard.IsVisible = playedGames.Count == 0;
        if (playedGames.Count == 0)
        {
            return;
        }

        foreach (var group in playedGames)
        {
            var section = new VerticalStackLayout
            {
                Spacing = 10
            };

            section.Children.Add(new Label
            {
                Text = SkillHeadings.TryGetValue(group.Key, out var heading)
                    ? heading
                    : group.Key.ToString().ToUpperInvariant(),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5A5D66")
            });

            foreach (var card in group)
            {
                section.Children.Add(BuildGameCard(card));
            }

            GamesSectionsHost.Children.Add(section);
        }
    }

    private static (BrainSkill skill, GameStatsCardModel card) CreateCardModel(PlayedGameScore game)
    {
        var title = game.SourceId switch
        {
            "word_a_like" => "Word-A-Like",
            "word_fresh" => "Word Fresh",
            "babble_bots" => "Babble Bots",
            "word_hunt" => "Word Hunt",
            "grow" => "Grow",
            "perilous_path" => "Perilous Path",
            "partial_match" => "Partial Match",
            "matcha_madness" => "Matcha Madness",
            "square_numbers" => "Square Numbers",
            "moving_math" => "Moving Math",
            "must_sort" => "Must Sort",
            "tap_trap" => "Tap Trap",
            "unique" => "Unique",
            "true_color" => "True Color",
            "spin_cycle" => "Spin Cycle",
            "turtle_traffic" => "Turtle Traffic",
            _ => ToTitle(game.SourceId)
        };

        var lastPlayedLocal = game.LastPlayedUtc.Kind == DateTimeKind.Utc
            ? game.LastPlayedUtc.ToLocalTime()
            : game.LastPlayedUtc;

        var description = game.Sessions <= 1
            ? "Best Peak Game Score from your latest run."
            : $"Best Peak Game Score across {game.Sessions} sessions.";

        Brush backgroundBrush = game.SourceId == "true_color"
            ? new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#0F7AF3"), 0.0f),
                    new GradientStop(Color.FromArgb("#42A1FF"), 0.62f),
                    new GradientStop(Color.FromArgb("#D3E8FF"), 1.0f)
                },
                new Point(0, 0.5),
                new Point(1, 0.5))
            : CreateBackgroundBrush(game.Skill);

        Color pillColor = game.SourceId == "true_color"
            ? Color.FromArgb("#1F86FF")
            : GetPillColor(game.Skill);

        Color chipColor = game.SourceId == "true_color"
            ? Color.FromArgb("#1475EC")
            : GetChipColor(game.Skill);

        return (game.Skill, new GameStatsCardModel(
            SourceId: game.SourceId,
            Title: title,
            ScoreText: game.PeakGameScore.ToString(),
            LastPlayedText: $"Played {lastPlayedLocal:dd MMM}",
            Description: description,
            BackgroundBrush: backgroundBrush,
            PillColor: pillColor,
            ChipColor: chipColor,
            TextColor: Colors.White));
    }

    private View BuildGameCard(GameStatsCardModel model)
    {
        var nameLabel = new Label
        {
            Text = model.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = model.TextColor,
            VerticalOptions = LayoutOptions.Center
        };

        var dateChip = new Border
        {
            BackgroundColor = model.ChipColor,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = model.LastPlayedText,
                FontSize = 11,
                TextColor = Colors.White,
                Opacity = 0.92
            }
        };

        var scorePill = new Border
        {
            BackgroundColor = model.PillColor,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(14, 8),
            Content = new Label
            {
                Text = model.ScoreText,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            }
        };

        var infoButton = new Border
        {
            BackgroundColor = Color.FromArgb("#EEF0F6"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            WidthRequest = 28,
            HeightRequest = 28,
            Padding = 0,
            Content = new Label
            {
                Text = "i",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#5D67A9"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (_, _) =>
            await PageTransitionService.PushAsync(Navigation, new GamePerformanceDetailPage(model.SourceId));
        infoButton.GestureRecognizers.Add(tapGesture);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        var details = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                nameLabel,
                dateChip
            }
        };

        Grid.SetColumn(scorePill, 1);
        Grid.SetColumn(infoButton, 2);

        grid.Children.Add(details);
        grid.Children.Add(scorePill);
        grid.Children.Add(infoButton);

        return new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(14, 12),
            Background = model.BackgroundBrush,
            Content = grid
        };
    }

    private static int GetSkillOrder(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Language => 0,
            BrainSkill.Memory => 1,
            BrainSkill.ProblemSolving => 2,
            BrainSkill.Focus => 3,
            _ => 4
        };
    }

    private static Brush CreateBackgroundBrush(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Language => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#6B5AF2"), 0.0f),
                    new GradientStop(Color.FromArgb("#7F6CFF"), 0.58f),
                    new GradientStop(Color.FromArgb("#ECE9FF"), 1.0f)
                },
                new Point(0, 0.5),
                new Point(1, 0.5)),
            BrainSkill.Memory => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#F3A61C"), 0.0f),
                    new GradientStop(Color.FromArgb("#F8C659"), 0.62f),
                    new GradientStop(Color.FromArgb("#FFF2D0"), 1.0f)
                },
                new Point(0, 0.5),
                new Point(1, 0.5)),
            BrainSkill.ProblemSolving => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#28C56D"), 0.0f),
                    new GradientStop(Color.FromArgb("#61D98F"), 0.62f),
                    new GradientStop(Color.FromArgb("#DCF8E7"), 1.0f)
                },
                new Point(0, 0.5),
                new Point(1, 0.5)),
            BrainSkill.Focus => new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#FF4A67"), 0.0f),
                    new GradientStop(Color.FromArgb("#FF7083"), 0.6f),
                    new GradientStop(Color.FromArgb("#FFE0E6"), 1.0f)
                },
                new Point(0, 0.5),
                new Point(1, 0.5)),
            _ => new SolidColorBrush(Color.FromArgb("#DDE5F5"))
        };
    }

    private static Color GetPillColor(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Language => Color.FromArgb("#8A80FF"),
            BrainSkill.Memory => Color.FromArgb("#E8A329"),
            BrainSkill.ProblemSolving => Color.FromArgb("#2DBF6B"),
            BrainSkill.Focus => Color.FromArgb("#FF5874"),
            _ => Color.FromArgb("#61748B")
        };
    }

    private static Color GetChipColor(BrainSkill skill)
    {
        return skill switch
        {
            BrainSkill.Language => Color.FromArgb("#7C70F7"),
            BrainSkill.Memory => Color.FromArgb("#D8941D"),
            BrainSkill.ProblemSolving => Color.FromArgb("#27AF61"),
            BrainSkill.Focus => Color.FromArgb("#F0506B"),
            _ => Color.FromArgb("#6C758E")
        };
    }

    private static string ToTitle(string value)
    {
        return string.Join(" ",
            value.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
