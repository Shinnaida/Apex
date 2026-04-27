using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class GamePerformanceDetailPage : ContentPage
{
    private static readonly Dictionary<string, GamePerformanceSnapshot?> SnapshotCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _sourceId;
    private bool _hasLoaded;

    private sealed record GameTheme(
        string Title,
        string Watermark,
        Color Primary,
        Color Secondary,
        Color CardAccent,
        Func<Page>? CreatePlayPage);

    public GamePerformanceDetailPage(string sourceId)
    {
        InitializeComponent();
        _sourceId = sourceId;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        ApplyView();
    }

    private void ApplyView()
    {
        if (!SnapshotCache.TryGetValue(_sourceId, out var snapshot))
        {
            snapshot = BrainScoreService.GetGamePerformance(_sourceId);
            SnapshotCache[_sourceId] = snapshot;
        }

        if (snapshot is null)
        {
            return;
        }

        var theme = GetTheme(snapshot.SourceId, snapshot.Skill);
        ApplyTheme(theme);

        GameTitleLabel.Text = theme.Title.ToUpperInvariant();
        HeroWatermarkLabel.Text = theme.Watermark;
        BestScoreValueLabel.Text = snapshot.BestScore.ToString();
        GameRankValueLabel.Text = GetGameRankName(snapshot.BestPeakGameScore);
        PeakGameScoreValueLabel.Text = snapshot.BestPeakGameScore.ToString();
        SessionsValueLabel.Text = snapshot.SessionCount.ToString();
        TrendSubtitleLabel.Text = "Discover how you have done over the last week";
        TopScoresSubtitleLabel.Text = $"Your top scores since {ToLocalTime(snapshot.FirstPlayedUtc):dd/MM/yyyy}";

        TrendCard.TitleColor = theme.Primary;
        TrendCard.AreaColor = theme.CardAccent;
        TrendCard.Score = snapshot.BestPeakGameScore.ToString();
        TrendCard.Values = snapshot.TrendValues.ToList();
        TrendCard.AxisLabels = new List<string> { "1w", "", "", "", "", "", "Today" };

        TopScoresHost.Children.Clear();
        foreach (var session in snapshot.TopSessions)
        {
            TopScoresHost.Children.Add(BuildTopScoreCard(session, theme));
        }
    }

    private void ApplyTheme(GameTheme theme)
    {
        HeroSection.Background = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(theme.Primary, 0.0f),
                new GradientStop(theme.Secondary, 1.0f)
            },
            new Point(0, 0),
            new Point(1, 1));

        PlayButton.TextColor = theme.Primary;
        HeroGlowOne.BackgroundColor = Colors.White;
        HeroGlowTwo.BackgroundColor = Colors.White;
    }

    private View BuildTopScoreCard(GameTopSession session, GameTheme theme)
    {
        var largeScoreLabel = new Label
        {
            Text = session.Score.ToString(),
            FontSize = 30,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        };

        var agoLabel = new Label
        {
            Text = FormatAgo(ToLocalTime(session.PlayedUtc)),
            FontSize = 12,
            TextColor = Color.FromArgb("#E9E9FF")
        };

        var infoColumn = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                CreateScoreInfoRow("PEAK GAME SCORE", session.PeakGameScore.ToString()),
                CreateScoreInfoRow("GAME RANK", GetGameRankName(session.PeakGameScore)),
                CreateScoreInfoRow("PLAYED", ToLocalTime(session.PlayedUtc).ToString("dd MMM yyyy"))
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 16
        };

        var rankBadge = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,
            BackgroundColor = Color.FromArgb("#18FFFFFF"),
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#55FFFFFF"),
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Content = new Label
            {
                Text = session.Rank.ToString(),
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var headerStack = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                largeScoreLabel,
                agoLabel
            }
        };

        var body = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                headerStack,
                infoColumn
            }
        };

        grid.Children.Add(rankBadge);
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);

        return new Border
        {
            Padding = new Thickness(18),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(theme.Primary, 0.0f),
                    new GradientStop(theme.Secondary, 1.0f)
                },
                new Point(0, 0),
                new Point(1, 1)),
            Content = grid
        };
    }

    private static View CreateScoreInfoRow(string label, string value)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };

        var labelView = new Label
        {
            Text = label,
            FontSize = 12,
            TextColor = Color.FromArgb("#DADAFB"),
            VerticalTextAlignment = TextAlignment.Center
        };

        var valueView = new Label
        {
            Text = value,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        };

        row.Children.Add(labelView);
        Grid.SetColumn(valueView, 1);
        row.Children.Add(valueView);
        return row;
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await GameHubNavigationService.ReturnToNearestHubAsync(Navigation);
    }

    async void OnPlayClicked(object sender, EventArgs e)
    {
        var theme = GetTheme(_sourceId, BrainSkill.Memory);
        if (theme.CreatePlayPage is null)
        {
            return;
        }

        await PageTransitionService.PushAsync(Navigation, theme.CreatePlayPage());
    }

    private static GameTheme GetTheme(string sourceId, BrainSkill skill)
    {
        return sourceId switch
        {
            "word_fresh" => new GameTheme("Word Fresh", "WF", Color.FromArgb("#5E54EA"), Color.FromArgb("#7368FF"), Color.FromArgb("#D8D3FF"), () => new WordFreshGamePage()),
            "word_a_like" => new GameTheme("Word-A-Like", "WA", Color.FromArgb("#6A58F1"), Color.FromArgb("#8C78FF"), Color.FromArgb("#DDD6FF"), () => new WordALikeGamePage()),
            "babble_bots" => new GameTheme("Babble Bots", "BB", Color.FromArgb("#5E54EA"), Color.FromArgb("#7368FF"), Color.FromArgb("#D8D3FF"), () => new BabbleBotsGamePage()),
            "word_hunt" => new GameTheme("Word Hunt", "WH", Color.FromArgb("#5E54EA"), Color.FromArgb("#7368FF"), Color.FromArgb("#D8D3FF"), () => new WordHuntGamePage()),
            "grow" => new GameTheme("Grow", "GR", Color.FromArgb("#665AF4"), Color.FromArgb("#8A80FF"), Color.FromArgb("#DDD6FF"), () => new GrowGamePage()),
            "perilous_path" => new GameTheme("Perilous Path", "PP", Color.FromArgb("#25B56A"), Color.FromArgb("#5FD78D"), Color.FromArgb("#D4F6E1"), () => new PerilousPathGamePage()),
            "partial_match" => new GameTheme("Partial Match", "PM", Color.FromArgb("#FF4E73"), Color.FromArgb("#FF7595"), Color.FromArgb("#FFD5E0"), () => new PartialMatchGamePage()),
            "matcha_madness" => new GameTheme("Matcha Madness", "MM", Color.FromArgb("#47D85B"), Color.FromArgb("#66E47A"), Color.FromArgb("#D9FBE0"), () => new MatchaMadnessGamePage()),
            "square_numbers" => new GameTheme("Square Numbers", "SN", Color.FromArgb("#28C85A"), Color.FromArgb("#5BE57E"), Color.FromArgb("#D9FBE0"), () => new SquareNumbersGamePage()),
            "moving_math" => new GameTheme("Moving Math", "MM", Color.FromArgb("#28C85A"), Color.FromArgb("#5BE57E"), Color.FromArgb("#D9FBE0"), () => new MovingMathGamePage()),
            "must_sort" => new GameTheme("Must Sort", "MS", Color.FromArgb("#FF3F71"), Color.FromArgb("#FF6990"), Color.FromArgb("#FFD5E0"), () => new MustSortGamePage()),
            "tap_trap" => new GameTheme("Tap Trap", "TT", Color.FromArgb("#FF3F71"), Color.FromArgb("#FF6990"), Color.FromArgb("#FFD5E0"), () => new TapTrapGamePage()),
            "unique" => new GameTheme("Unique", "UN", Color.FromArgb("#FF3F71"), Color.FromArgb("#FF6990"), Color.FromArgb("#FFD5E0"), () => new UniqueGamePage()),
            "true_color" => new GameTheme("True Color", "TC", Color.FromArgb("#0F7AF3"), Color.FromArgb("#42A1FF"), Color.FromArgb("#D3E8FF"), () => new TrueColorGamePage()),
            "spin_cycle" => new GameTheme("Spin Cycle", "SC", Color.FromArgb("#F39A19"), Color.FromArgb("#FFBC54"), Color.FromArgb("#FFE6BA"), () => new SpinCycleGamePage()),
            "turtle_traffic" => new GameTheme("Turtle Traffic", "TT", Color.FromArgb("#0F7AF3"), Color.FromArgb("#42A1FF"), Color.FromArgb("#D3E8FF"), () => new TurtleTrafficGamePage()),
            _ => skill switch
            {
                BrainSkill.Language => new GameTheme(ToTitle(sourceId), "LG", Color.FromArgb("#6A58F1"), Color.FromArgb("#8C78FF"), Color.FromArgb("#DDD6FF"), null),
                BrainSkill.Memory => new GameTheme(ToTitle(sourceId), "MM", Color.FromArgb("#F39A19"), Color.FromArgb("#FFBC54"), Color.FromArgb("#FFE6BA"), null),
                BrainSkill.ProblemSolving => new GameTheme(ToTitle(sourceId), "PS", Color.FromArgb("#25B56A"), Color.FromArgb("#5FD78D"), Color.FromArgb("#D4F6E1"), null),
                BrainSkill.Focus => new GameTheme(ToTitle(sourceId), "FC", Color.FromArgb("#FF4E73"), Color.FromArgb("#FF7595"), Color.FromArgb("#FFD5E0"), null),
                _ => new GameTheme(ToTitle(sourceId), "GM", Color.FromArgb("#61748B"), Color.FromArgb("#7D8EA3"), Color.FromArgb("#E0E7EE"), null)
            }
        };
    }

    private static string GetGameRankName(int peakGameScore)
    {
        return peakGameScore switch
        {
            >= 900 => "Elite",
            >= 760 => "Expert",
            >= 580 => "Skilled",
            >= 360 => "Novice",
            _ => "Beginner"
        };
    }

    private static string FormatAgo(DateTime dateTime)
    {
        var delta = DateTime.Now - dateTime;
        if (delta.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (delta.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)delta.TotalMinutes)}m ago";
        }

        if (delta.TotalDays < 1)
        {
            return $"{Math.Max(1, (int)delta.TotalHours)}h ago";
        }

        if (delta.TotalDays < 7)
        {
            return $"{Math.Max(1, (int)delta.TotalDays)}d ago";
        }

        return dateTime.ToString("dd MMM yyyy");
    }

    private static DateTime ToLocalTime(DateTime timestampUtc)
    {
        return timestampUtc.Kind == DateTimeKind.Utc
            ? timestampUtc.ToLocalTime()
            : timestampUtc;
    }

    private static string ToTitle(string value)
    {
        return string.Join(" ",
            value.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
