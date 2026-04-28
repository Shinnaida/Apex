
using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;

namespace Peak;

public partial class LeaderboardsPage : ContentPage
{
    private const string RankSnapshotPrefix = "rankings_snapshot_";

    private sealed record ScopeOption(string Label, string? GameSourceId);

    private sealed class PodiumColumnParts
    {
        public required View Root { get; init; }
        public required int DisplayRank { get; init; }
        public Border? Crown { get; init; }
        public BoxView? Shimmer { get; init; }
    }

    private readonly List<ScopeOption> _scopeOptions = new();
    private Dictionary<string, string> _rankChangeLabels = new(StringComparer.OrdinalIgnoreCase);

    private bool _hasLoaded;
    private bool _isLoading;
    private string? _selectedGameSourceId;
    private LeaderboardTimeframe _currentTimeframe = LeaderboardTimeframe.AllTime;

    public LeaderboardsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_hasLoaded)
        {
            return;
        }

        try
        {
            BuildScopeOptions();
            ApplyTimeframeButtons();
            ApplyScopeSelection();
            _hasLoaded = true;
            await RenderLeaderboardAsync();
        }
        catch
        {
            LeaderboardSubtitleLabel.Text = "Rankings are unavailable right now. Please try again in a moment.";
            BoardCaptionLabel.Text = "Showing no live data";
            InsightRankLabel.Text = "--";
            InsightTitleLabel.Text = "Rankings unavailable";
            InsightDetailLabel.Text = "Your saved data is safe. Try reopening rankings after the app finishes syncing.";
            _hasLoaded = true;
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

    async void OnGamesTapped(object sender, EventArgs e)
    {
        await PageTransitionService.GoToAsync(nameof(GamesStatsPage));
    }

    void OnLeaderboardsTapped(object sender, EventArgs e)
    {
        // Already here.
    }

    async void OnWeeklyClicked(object sender, EventArgs e)
    {
        await ChangeTimeframeAsync(LeaderboardTimeframe.Weekly);
    }

    async void OnAllTimeClicked(object sender, EventArgs e)
    {
        await ChangeTimeframeAsync(LeaderboardTimeframe.AllTime);
    }

    async void OnScopeTriggerTapped(object sender, TappedEventArgs e)
    {
        await ShowScopeOverlayAsync();
    }

    async void OnScopeBackdropTapped(object sender, TappedEventArgs e)
    {
        await HideScopeOverlayAsync();
    }

    async void OnCloseScopeClicked(object sender, EventArgs e)
    {
        await HideScopeOverlayAsync();
    }

    async void OnProfileBackdropTapped(object sender, TappedEventArgs e)
    {
        await HideProfileAsync();
    }

    async void OnCloseProfileClicked(object sender, EventArgs e)
    {
        await HideProfileAsync();
    }

    private void BuildScopeOptions()
    {
        _scopeOptions.Clear();
        _scopeOptions.Add(new ScopeOption("Overall", null));

        foreach (var game in PlayerLeaderboardService.GetSupportedGames())
        {
            _scopeOptions.Add(new ScopeOption(game.Name, game.SourceId));
        }
        BuildScopeOptionCards();
    }

    private async Task ChangeTimeframeAsync(LeaderboardTimeframe timeframe)
    {
        if (_currentTimeframe == timeframe)
        {
            return;
        }

        _currentTimeframe = timeframe;
        ApplyTimeframeButtons();
        await RenderLeaderboardAsync();
    }

    private async Task ChangeScopeAsync(string? gameSourceId)
    {
        var normalized = string.IsNullOrWhiteSpace(gameSourceId)
            ? null
            : gameSourceId.Trim().ToLowerInvariant();

        if (string.Equals(_selectedGameSourceId, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _selectedGameSourceId = normalized;
        ApplyScopeSelection();
        await RenderLeaderboardAsync();
    }

    private void ApplyTimeframeButtons()
    {
        var weeklySelected = _currentTimeframe == LeaderboardTimeframe.Weekly;

        WeeklyButton.BackgroundColor = weeklySelected ? Color.FromArgb("#7A73E8") : Color.FromArgb("#E8E4FF");
        WeeklyButton.TextColor = weeklySelected ? Colors.White : Color.FromArgb("#716BA7");

        AllTimeButton.BackgroundColor = weeklySelected ? Color.FromArgb("#E8E4FF") : Color.FromArgb("#7A73E8");
        AllTimeButton.TextColor = weeklySelected ? Color.FromArgb("#716BA7") : Colors.White;
    }

    private void ApplyScopeSelection()
    {
        var selectedOption = _scopeOptions.FirstOrDefault(option =>
            string.Equals(option.GameSourceId, _selectedGameSourceId, StringComparison.OrdinalIgnoreCase))
            ?? _scopeOptions[0];

        ScopeSelectionLabel.Text = selectedOption.Label;
        ScopeModeLabel.Text = selectedOption.GameSourceId is null ? "Leaderboard View" : "Game Leaderboard";

        foreach (var child in ScopeOptionHost.Children.OfType<Border>())
        {
            if (child.BindingContext is not ScopeOption option)
            {
                continue;
            }

            var selected = string.Equals(option.GameSourceId, _selectedGameSourceId, StringComparison.OrdinalIgnoreCase)
                           || (option.GameSourceId is null && _selectedGameSourceId is null);

            child.BackgroundColor = selected ? Color.FromArgb("#F3F0FF") : Colors.White;
            child.Stroke = selected ? Color.FromArgb("#4538FF") : Color.FromArgb("#E7E1FF");

            if (child.Content is Grid grid)
            {
                if (grid.Children[0] is VerticalStackLayout stack)
                {
                    if (stack.Children[0] is Label title)
                    {
                        title.TextColor = selected ? Color.FromArgb("#4538FF") : Color.FromArgb("#3D3760");
                    }

                    if (stack.Children.Count > 1 && stack.Children[1] is Label subtitle)
                    {
                        subtitle.TextColor = selected ? Color.FromArgb("#6D63CC") : Color.FromArgb("#948DB6");
                    }
                }

                if (grid.Children.Count > 1 && grid.Children[1] is Border chip)
                {
                    chip.BackgroundColor = selected ? Color.FromArgb("#4538FF") : Color.FromArgb("#F3F0FF");
                    if (chip.Content is Label chipLabel)
                    {
                        chipLabel.TextColor = selected ? Colors.White : Color.FromArgb("#8C84B1");
                    }
                }
            }
        }
    }

    private void BuildScopeOptionCards()
    {
        ScopeOptionHost.Children.Clear();

        foreach (var option in _scopeOptions)
        {
            var title = new Label
            {
                Text = option.Label,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#3D3760")
            };

            var subtitle = new Label
            {
                Text = option.GameSourceId is null
                    ? "See rankings across all players"
                    : $"See rankings for {option.Label} only",
                FontSize = 12,
                TextColor = Color.FromArgb("#948DB6")
            };

            var chipLabel = new Label
            {
                Text = option.GameSourceId is null ? "Default" : "Game",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#8C84B1"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            var chip = new Border
            {
                Padding = new Thickness(10, 6),
                BackgroundColor = Color.FromArgb("#F3F0FF"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = chipLabel,
                VerticalOptions = LayoutOptions.Center
            };

            var card = new Border
            {
                Padding = new Thickness(14, 12),
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E7E1FF"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                BindingContext = option,
                Content = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitionCollection
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 10,
                    Children =
                    {
                        new VerticalStackLayout
                        {
                            Spacing = 2,
                            Children = { title, subtitle }
                        },
                        chip
                    }
                }
            };

            Grid.SetColumn(chip, 1);

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                await HideScopeOverlayAsync();
                await ChangeScopeAsync(option.GameSourceId);
            };
            card.GestureRecognizers.Add(tap);

            ScopeOptionHost.Children.Add(card);
        }
    }

    private async Task ShowScopeOverlayAsync()
    {
        if (ScopeOverlay.IsVisible)
        {
            return;
        }

        ApplyScopeSelection();
        ScopeChevronLabel.Rotation = 180;
        ScopeOverlay.IsVisible = true;
        ScopeOverlay.Opacity = 0;
        ScopeSheet.TranslationY = -20;

        await Task.WhenAll(
            ScopeOverlay.FadeTo(1, 140, Easing.CubicOut),
            ScopeSheet.TranslateTo(0, 0, 160, Easing.CubicOut));
    }

    private async Task HideScopeOverlayAsync()
    {
        if (!ScopeOverlay.IsVisible)
        {
            ScopeChevronLabel.Rotation = 0;
            return;
        }

        ScopeChevronLabel.Rotation = 0;
        await Task.WhenAll(
            ScopeOverlay.FadeTo(0, 120, Easing.CubicIn),
            ScopeSheet.TranslateTo(0, -20, 120, Easing.CubicIn));

        ScopeOverlay.IsVisible = false;
    }

    private async Task RenderLeaderboardAsync()
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        try
        {
            LeaderboardHost.Children.Clear();
            PodiumHost.Children.Clear();
            CelebrationLayer.Children.Clear();
            CelebrationLayer.IsVisible = false;

            var activeModeLabel = _selectedGameSourceId is null
                ? "player rankings"
                : $"{PlayerLeaderboardService.GetGameDisplayName(_selectedGameSourceId)} rankings";

            LeaderboardSubtitleLabel.Text = _currentTimeframe == LeaderboardTimeframe.Weekly
                ? $"Refreshing weekly {activeModeLabel}..."
                : $"Refreshing all-time {activeModeLabel}...";
            BoardCaptionLabel.Text = "Loading...";
            InsightRankLabel.Text = "#--";
            InsightTitleLabel.Text = "Loading rankings...";
            InsightDetailLabel.Text = "We are refreshing the latest player standings.";

            await App.RefreshSignedInCloudStateAsync();
            var result = await PlayerLeaderboardService.GetLeaderboardAsync(25, _currentTimeframe, _selectedGameSourceId);
            var orderedEntries = result.Entries
                .OrderBy(entry => entry.Rank)
                .ToList();

            _rankChangeLabels = ApplyRankChanges(orderedEntries);

            LeaderboardSubtitleLabel.Text = result.Message;
            BoardCaptionLabel.Text = BuildBoardCaption(orderedEntries, _currentTimeframe, _selectedGameSourceId);

            RenderInsight(orderedEntries);
            await RenderPodiumAsync(orderedEntries);
            RenderRankedList(orderedEntries);

            if (Content is VisualElement pageRoot)
            {
                pageRoot.Opacity = 0.98;
                await pageRoot.FadeTo(1, 120, Easing.CubicOut);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private Dictionary<string, string> ApplyRankChanges(IReadOnlyList<PlayerLeaderboardEntry> entries)
    {
        var snapshotKey = BuildRankSnapshotKey();
        var previous = LoadRankSnapshot(snapshotKey);
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (previous.Count > 0)
        {
            foreach (var entry in entries)
            {
                if (previous.TryGetValue(entry.PlayerId, out var oldRank))
                {
                    var delta = oldRank - entry.Rank;
                    if (delta > 0)
                    {
                        labels[entry.PlayerId] = $"+{delta}";
                    }
                    else if (delta < 0)
                    {
                        labels[entry.PlayerId] = delta.ToString();
                    }
                }
                else
                {
                    labels[entry.PlayerId] = "NEW";
                }
            }
        }

        var current = entries.ToDictionary(entry => entry.PlayerId, entry => entry.Rank, StringComparer.OrdinalIgnoreCase);
        SaveRankSnapshot(snapshotKey, current);
        return labels;
    }

    private string BuildRankSnapshotKey()
    {
        var mode = _selectedGameSourceId ?? "overall";
        return $"{RankSnapshotPrefix}{_currentTimeframe.ToString().ToLowerInvariant()}_{mode}";
    }

    private static Dictionary<string, int> LoadRankSnapshot(string key)
    {
        var raw = Preferences.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(raw)
                   ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveRankSnapshot(string key, Dictionary<string, int> snapshot)
    {
        Preferences.Set(key, JsonSerializer.Serialize(snapshot));
    }
    private void RenderInsight(IReadOnlyList<PlayerLeaderboardEntry> orderedEntries)
    {
        var current = orderedEntries.FirstOrDefault(entry => entry.IsCurrentPlayer);
        if (current is null)
        {
            InsightCard.BackgroundColor = Color.FromArgb("#A19BCB");
            InsightRankLabel.Text = "--";
            InsightTitleLabel.Text = _currentTimeframe == LeaderboardTimeframe.Weekly
                ? "No weekly rank yet"
                : "No rank yet";
            InsightDetailLabel.Text = _selectedGameSourceId is null
                ? "Play more games and sync your score to appear on the board."
                : $"Play more {PlayerLeaderboardService.GetGameDisplayName(_selectedGameSourceId)} to appear on this board.";
            return;
        }

        var currentIndex = orderedEntries
            .Select((entry, index) => new { entry, index })
            .FirstOrDefault(item => ReferenceEquals(item.entry, current))
            ?.index ?? 0;

        var betterThan = orderedEntries.Count <= 1
            ? 0
            : (int)Math.Round(((orderedEntries.Count - currentIndex - 1) / (double)(orderedEntries.Count - 1)) * 100);

        InsightRankLabel.Text = $"#{current.Rank}";
        InsightCard.BackgroundColor = current.Rank <= 3
            ? GetLeaderboardAccent(current.Rank)
            : _selectedGameSourceId is null
                ? Color.FromArgb("#9A8CF8")
                : PlayerLeaderboardService.GetGameAccentColor(_selectedGameSourceId);

        var boardLabel = _selectedGameSourceId is null
            ? "rankings"
            : $"{PlayerLeaderboardService.GetGameDisplayName(_selectedGameSourceId)} rankings";

        InsightTitleLabel.Text = current.Rank == 1
            ? $"You are leading the {boardLabel}"
            : _currentTimeframe == LeaderboardTimeframe.Weekly
                ? $"You are ahead of {betterThan}% of this week's players"
                : $"You are ahead of {betterThan}% of the players on this board";

        InsightDetailLabel.Text = current.Rank == 1
            ? $"Score {current.PeakScore}. Keep the crown this {_currentTimeframe.ToString().ToLowerInvariant()}."
            : $"Current rank {current.Rank} with {current.PeakScore} points and the {current.PeakRank} tier.";
    }

    private async Task RenderPodiumAsync(IReadOnlyList<PlayerLeaderboardEntry> orderedEntries)
    {
        var first = orderedEntries.FirstOrDefault(entry => entry.Rank == 1);
        var second = orderedEntries.FirstOrDefault(entry => entry.Rank == 2);
        var third = orderedEntries.FirstOrDefault(entry => entry.Rank == 3);

        var podiumEntries = new[]
        {
            (Entry: second, Rank: 2, Height: 122d, Color: GetLeaderboardAccent(2)),
            (Entry: first, Rank: 1, Height: 168d, Color: GetLeaderboardAccent(1)),
            (Entry: third, Rank: 3, Height: 104d, Color: GetLeaderboardAccent(3))
        };

        var podiumParts = new List<PodiumColumnParts>();

        for (var i = 0; i < podiumEntries.Length; i++)
        {
            var parts = BuildPodiumColumn(
                podiumEntries[i].Entry,
                podiumEntries[i].Rank,
                podiumEntries[i].Height,
                podiumEntries[i].Color);

            parts.Root.Opacity = 0;
            parts.Root.TranslationY = 28;
            parts.Root.Scale = 0.96;
            Grid.SetColumn(parts.Root, i);
            PodiumHost.Children.Add(parts.Root);
            podiumParts.Add(parts);
        }

        for (var i = 0; i < podiumParts.Count; i++)
        {
            var view = podiumParts[i].Root;
            if (i > 0)
            {
                await Task.Delay(55);
            }

            await Task.WhenAll(
                view.FadeTo(1, 180, Easing.CubicOut),
                view.TranslateTo(0, 0, 240, Easing.CubicOut),
                view.ScaleTo(1, 220, Easing.CubicOut));
        }

        var champion = podiumParts.FirstOrDefault(parts => parts.DisplayRank == 1);
        if (champion is not null && first is not null)
        {
            await PlayTopEffectsAsync(champion);
        }
    }

    private void RenderRankedList(IReadOnlyList<PlayerLeaderboardEntry> orderedEntries)
    {
        var remainingEntries = orderedEntries
            .Where(entry => entry.Rank > 3)
            .ToList();

        if (remainingEntries.Count == 0)
        {
            LeaderboardHost.Children.Add(new Label
            {
                Text = "More players will appear here once the rankings fill up.",
                FontSize = 13,
                TextColor = Color.FromArgb("#8F88B2"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4)
            });
            return;
        }

        foreach (var entry in remainingEntries)
        {
            LeaderboardHost.Children.Add(BuildLeaderboardRow(entry));
        }
    }

    private PodiumColumnParts BuildPodiumColumn(PlayerLeaderboardEntry? entry, int displayRank, double pedestalHeight, Color pedestalColor)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Fill
        };

        var avatarShell = new Grid
        {
            WidthRequest = displayRank == 1 ? 74 : 62,
            HeightRequest = displayRank == 1 ? 74 : 62,
            HorizontalOptions = LayoutOptions.Center
        };

        var avatar = new Border
        {
            WidthRequest = displayRank == 1 ? 70 : 58,
            HeightRequest = displayRank == 1 ? 70 : 58,
            BackgroundColor = entry?.AccentColor ?? Color.FromArgb("#D6D0F7"),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 4,
            StrokeShape = new RoundRectangle { CornerRadius = displayRank == 1 ? 35 : 29 },
            Content = BuildAvatarContent(
                entry,
                entry is null ? 18 : entry.AvatarText.Length <= 2 ? 22 : 26,
                Colors.White),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        avatarShell.Children.Add(avatar);

        Border? crown = null;
        if (displayRank == 1)
        {
            crown = new Border
            {
                WidthRequest = 26,
                HeightRequest = 26,
                BackgroundColor = Color.FromArgb("#FFCB52"),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2,
                StrokeShape = new RoundRectangle { CornerRadius = 13 },
                Shadow = new Shadow
                {
                    Brush = Colors.Gold,
                    Offset = new Point(0, 0),
                    Radius = 14,
                    Opacity = 0.9f
                },
                Content = new Label
                {
                    Text = "1",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                },
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                TranslationY = -6
            };

            avatarShell.Children.Add(crown);
        }

        stack.Children.Add(avatarShell);
        stack.Children.Add(new Label
        {
            Text = entry?.Name ?? "Waiting",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4A456D"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        });

        stack.Children.Add(new Border
        {
            BackgroundColor = GetLeaderboardAccent(displayRank).WithAlpha(0.16f),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = new Thickness(12, 6),
            HorizontalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = entry is null ? "--" : $"{entry.PeakScore} pts",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = displayRank <= 3 ? GetLeaderboardAccent(displayRank) : Color.FromArgb("#7A73A6"),
                HorizontalTextAlignment = TextAlignment.Center
            }
        });

        if (entry is not null && _rankChangeLabels.TryGetValue(entry.PlayerId, out var podiumChange))
        {
            stack.Children.Add(BuildRankChangeChip(podiumChange, true));
        }
        var pedestalGrid = new Grid();
        BoxView? shimmer = null;

        pedestalGrid.Children.Add(new Label
        {
            Text = displayRank.ToString(),
            FontSize = displayRank == 1 ? 54 : 44,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Start
        });

        if (displayRank == 1)
        {
            shimmer = new BoxView
            {
                WidthRequest = 18,
                Color = Colors.White.WithAlpha(0.28f),
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Fill,
                Rotation = -18,
                Opacity = 0,
                TranslationX = -80
            };
            pedestalGrid.Children.Add(shimmer);
        }

        stack.Children.Add(new Border
        {
            HeightRequest = pedestalHeight,
            BackgroundColor = pedestalColor,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20, 20, 0, 0) },
            Padding = new Thickness(0, 12, 0, 0),
            Content = pedestalGrid
        });

        if (entry is not null)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowProfileAsync(entry);
            stack.GestureRecognizers.Add(tap);
        }

        return new PodiumColumnParts
        {
            Root = stack,
            DisplayRank = displayRank,
            Crown = crown,
            Shimmer = shimmer
        };
    }

    private View BuildLeaderboardRow(PlayerLeaderboardEntry entry)
    {
        var border = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(12),
            BackgroundColor = entry.IsCurrentPlayer
                ? Color.FromArgb("#F0ECFF")
                : Color.FromArgb("#FBFAFF")
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        var rankChip = new Border
        {
            WidthRequest = 30,
            HeightRequest = 30,
            BackgroundColor = entry.IsCurrentPlayer ? Color.FromArgb("#7D73F3") : Color.FromArgb("#F0EEFA"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            Content = new Label
            {
                Text = entry.Rank.ToString(),
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = entry.IsCurrentPlayer ? Colors.White : Color.FromArgb("#8B84B0"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            },
            VerticalOptions = LayoutOptions.Center
        };

        var avatar = new Border
        {
            WidthRequest = 46,
            HeightRequest = 46,
            BackgroundColor = entry.AccentColor,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 23 },
            Content = BuildAvatarContent(
                entry,
                entry.AvatarText.Length <= 2 ? 18 : 20,
                Colors.White),
            VerticalOptions = LayoutOptions.Center
        };

        var nameStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = entry.Name,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#403A62"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                },
                new Label
                {
                    Text = entry.IsCurrentPlayer ? $"{entry.PeakRank} • You" : entry.PeakRank,
                    FontSize = 12,
                    TextColor = entry.IsCurrentPlayer ? Color.FromArgb("#7D73F3") : Color.FromArgb("#9D97BC")
                }
            }
        };

        var scoreStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };

        if (_rankChangeLabels.TryGetValue(entry.PlayerId, out var changeLabel))
        {
            scoreStack.Children.Add(BuildRankChangeChip(changeLabel, false));
        }

        scoreStack.Children.Add(new Label
        {
            Text = entry.PeakScore.ToString(),
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#36315A"),
            HorizontalTextAlignment = TextAlignment.End
        });
        scoreStack.Children.Add(new Label
        {
            Text = "points",
            FontSize = 11,
            TextColor = Color.FromArgb("#9D97BC"),
            HorizontalTextAlignment = TextAlignment.End
        });

        grid.Children.Add(rankChip);
        Grid.SetColumn(avatar, 1);
        grid.Children.Add(avatar);
        Grid.SetColumn(nameStack, 2);
        grid.Children.Add(nameStack);
        Grid.SetColumn(scoreStack, 3);
        grid.Children.Add(scoreStack);

        border.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await ShowProfileAsync(entry);
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private static Border BuildRankChangeChip(string label, bool compact)
    {
        var positive = label.StartsWith("+", StringComparison.Ordinal);
        var isNew = string.Equals(label, "NEW", StringComparison.OrdinalIgnoreCase);
        var background = isNew
            ? Color.FromArgb("#FFE8C2")
            : positive
                ? Color.FromArgb("#DDF8E8")
                : Color.FromArgb("#FFE1E8");
        var textColor = isNew
            ? Color.FromArgb("#C47A11")
            : positive
                ? Color.FromArgb("#1E9255")
                : Color.FromArgb("#D24C72");

        return new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = compact ? 10 : 12 },
            Padding = compact ? new Thickness(8, 3) : new Thickness(10, 4),
            HorizontalOptions = compact ? LayoutOptions.Center : LayoutOptions.End,
            Content = new Label
            {
                Text = label,
                FontSize = compact ? 10 : 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = textColor,
                HorizontalTextAlignment = TextAlignment.Center
            }
        };
    }

    private async Task ShowProfileAsync(PlayerLeaderboardEntry entry)
    {
        ApplyProfileDetails(new PlayerProfileDetails(
            Name: entry.Name,
            AvatarText: entry.AvatarText,
            AvatarImageSource: entry.AvatarImageSource,
            CurrentRank: entry.Rank,
            RankLabel: _selectedGameSourceId is null
                ? $"Overall rank #{entry.Rank}"
                : $"{PlayerLeaderboardService.GetGameDisplayName(_selectedGameSourceId)} rank #{entry.Rank}",
            BestSkillName: "Loading...",
            GamesPlayed: 0,
            Score: entry.PeakScore,
            ScoreLabel: _selectedGameSourceId is null
                ? "Peak Brain Score"
                : $"{PlayerLeaderboardService.GetGameDisplayName(_selectedGameSourceId)} best",
            AchievementSummaryText: "Loading achievements...",
            Achievements: Array.Empty<AchievementItem>()));

        await ShowProfileOverlayAsync();

        var details = await PlayerLeaderboardService.GetPlayerProfileAsync(entry, _currentTimeframe, _selectedGameSourceId);
        ApplyProfileDetails(details);
    }
    private void ApplyProfileDetails(PlayerProfileDetails details)
    {
        ProfileNameLabel.Text = details.Name;
        ProfileRankLabel.Text = details.RankLabel;
        ProfileBestSkillLabel.Text = details.BestSkillName;
        ProfileGamesPlayedLabel.Text = details.GamesPlayed.ToString();
        ProfileScoreLabel.Text = details.ScoreLabel;
        ProfilePrimaryScoreValueLabel.Text = details.Score.ToString();
        ProfileAchievementsSummaryLabel.Text = details.AchievementSummaryText;
        RenderProfileAchievements(details.Achievements, details.Name);

        if (TryCreateImageSource(details.AvatarImageSource, out var imageSource))
        {
            ProfileAvatarImage.Source = imageSource;
            ProfileAvatarImage.IsVisible = true;
            ProfileAvatarLabel.IsVisible = false;
        }
        else
        {
            ProfileAvatarImage.Source = null;
            ProfileAvatarImage.IsVisible = false;
            ProfileAvatarLabel.Text = details.AvatarText;
            ProfileAvatarLabel.IsVisible = true;
        }
    }

    private void RenderProfileAchievements(IReadOnlyList<AchievementItem> achievements, string ownerName)
    {
        ProfileAchievementsHost.Children.Clear();

        if (achievements.Count == 0)
        {
            ProfileAchievementsHost.Children.Add(new Border
            {
                Padding = new Thickness(12, 10),
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E3DDFF"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Content = new Label
                {
                    Text = "No badges unlocked yet",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#8E88B1")
                }
            });
            return;
        }

        foreach (var achievement in achievements)
        {
            var chip = new Border
            {
                Padding = new Thickness(12, 10),
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E3DDFF"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Content = new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Image
                        {
                            Source = achievement.IconSource,
                            WidthRequest = 20,
                            HeightRequest = 20,
                            VerticalOptions = LayoutOptions.Center
                        },
                        new VerticalStackLayout
                        {
                            Spacing = 1,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new Label
                                {
                                    Text = achievement.Title,
                                    FontSize = 12,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#453F69")
                                },
                                new Label
                                {
                                    Text = achievement.Category,
                                    FontSize = 10,
                                    TextColor = Color.FromArgb("#8E88B1")
                                }
                            }
                        }
                    }
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OpenProfileAchievementAsync(chip, achievement, ownerName);
            chip.GestureRecognizers.Add(tap);
            ProfileAchievementsHost.Children.Add(chip);
        }
    }

    private async Task OpenProfileAchievementAsync(Border chip, AchievementItem achievement, string ownerName)
    {
        await chip.ScaleTo(0.97, 70, Easing.CubicOut);
        await chip.ScaleTo(1.0, 90, Easing.CubicIn);
        await HideProfileAsync();
        await PageTransitionService.PushAsync(Navigation, new AchievementSpotlightPage(achievement, ownerName));
    }

    private async Task ShowProfileOverlayAsync()
    {
        if (ProfileOverlay.IsVisible)
        {
            return;
        }

        ProfileOverlay.IsVisible = true;
        ProfileOverlay.Opacity = 0;
        ProfileSheet.TranslationY = 420;

        await Task.WhenAll(
            ProfileOverlay.FadeTo(1, 140, Easing.CubicOut),
            ProfileSheet.TranslateTo(0, 0, 220, Easing.CubicOut));
    }

    private async Task HideProfileAsync()
    {
        if (!ProfileOverlay.IsVisible)
        {
            return;
        }

        await Task.WhenAll(
            ProfileOverlay.FadeTo(0, 120, Easing.CubicIn),
            ProfileSheet.TranslateTo(0, 420, 180, Easing.CubicIn));

        ProfileOverlay.IsVisible = false;
    }

    private async Task PlayTopEffectsAsync(PodiumColumnParts champion)
    {
        var effectTasks = new List<Task>
        {
            PlayConfettiAsync()
        };

        if (champion.Crown is not null)
        {
            effectTasks.Add(PlayCrownGlowAsync(champion.Crown));
        }

        if (champion.Shimmer is not null)
        {
            effectTasks.Add(PlayShimmerAsync(champion.Shimmer));
        }

        await Task.WhenAll(effectTasks);
    }

    private static async Task PlayCrownGlowAsync(VisualElement crown)
    {
        await crown.ScaleTo(1.12, 220, Easing.CubicOut);
        await crown.ScaleTo(1.0, 220, Easing.CubicIn);
        await crown.ScaleTo(1.08, 180, Easing.CubicOut);
        await crown.ScaleTo(1.0, 180, Easing.CubicIn);
    }

    private static async Task PlayShimmerAsync(BoxView shimmer)
    {
        shimmer.Opacity = 0.7;
        shimmer.TranslationX = -90;
        await shimmer.TranslateTo(98, 0, 420, Easing.CubicInOut);
        await shimmer.FadeTo(0, 140, Easing.CubicOut);
    }

    private async Task PlayConfettiAsync()
    {
        CelebrationLayer.Children.Clear();
        CelebrationLayer.IsVisible = true;

        var random = new Random();
        var colors = new[]
        {
            Color.FromArgb("#FFCB52"),
            Color.FromArgb("#7A73E8"),
            Color.FromArgb("#4CD8FF"),
            Color.FromArgb("#FF7E98"),
            Color.FromArgb("#7FE29D")
        };

        var originX = Math.Max(Width / 2, 140);
        var originY = 110d;
        var tasks = new List<Task>();

        for (var i = 0; i < 18; i++)
        {
            var piece = new Border
            {
                WidthRequest = random.Next(8, 14),
                HeightRequest = random.Next(10, 18),
                BackgroundColor = colors[i % colors.Length],
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                TranslationX = originX + random.Next(-34, 35),
                TranslationY = originY + random.Next(-8, 12),
                Rotation = random.Next(-35, 36)
            };

            CelebrationLayer.Children.Add(piece);

            var targetX = piece.TranslationX + random.Next(-180, 181);
            var targetY = piece.TranslationY + random.Next(120, 260);
            var targetRotation = piece.Rotation + random.Next(-240, 241);

            tasks.Add(Task.WhenAll(
                piece.TranslateTo(targetX, targetY, 680, Easing.CubicOut),
                piece.RotateTo(targetRotation, 680, Easing.Linear),
                piece.FadeTo(0, 640, Easing.CubicIn)));
        }

        await Task.WhenAll(tasks);
        CelebrationLayer.Children.Clear();
        CelebrationLayer.IsVisible = false;
    }

    private static string BuildBoardCaption(
        IReadOnlyList<PlayerLeaderboardEntry> orderedEntries,
        LeaderboardTimeframe timeframe,
        string? gameSourceId)
    {
        var currentBeyondTop = orderedEntries.Any(entry => entry.IsCurrentPlayer && entry.Rank > 25);
        var prefix = timeframe == LeaderboardTimeframe.Weekly ? "This week" : "All time";
        if (!string.IsNullOrWhiteSpace(gameSourceId))
        {
            prefix = $"{prefix} • {PlayerLeaderboardService.GetGameDisplayName(gameSourceId)}";
        }

        return currentBeyondTop
            ? $"{prefix}: Top 25 + you"
            : $"{prefix}: Top {orderedEntries.Count}";
    }

    private static Color GetLeaderboardAccent(int rank) => rank switch
    {
        1 => Color.FromArgb("#FFB800"),
        2 => Color.FromArgb("#7B9CFF"),
        3 => Color.FromArgb("#FF8A5B"),
        _ => Color.FromArgb("#7A73E8")
    };

    private static View BuildAvatarContent(PlayerLeaderboardEntry? entry, double fallbackFontSize, Color fallbackTextColor)
    {
        if (entry is not null && TryCreateImageSource(entry.AvatarImageSource, out var imageSource))
        {
            return new Image
            {
                Source = imageSource,
                Aspect = Aspect.AspectFill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
        }

        return new Label
        {
            Text = entry?.AvatarText ?? "-",
            FontSize = fallbackFontSize,
            FontAttributes = FontAttributes.Bold,
            TextColor = fallbackTextColor,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
    }

    private static bool TryCreateImageSource(string? rawValue, out ImageSource? imageSource)
    {
        return AvatarImageSyncHelper.TryCreateImageSource(rawValue, out imageSource);
    }
}
