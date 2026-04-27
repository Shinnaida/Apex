using System.Collections.ObjectModel;

namespace Peak;

public partial class WorkoutPlanPage : ContentPage
{
    readonly IReadOnlyList<WorkoutTileViewModel> _playOrder;

    public ObservableCollection<WorkoutTileViewModel> WorkoutTiles { get; } = new();

    public WorkoutPlanPage()
    {
        InitializeComponent();
        BindingContext = this;

        _playOrder = BuildWorkoutTiles();
        foreach (var tile in _playOrder)
        {
            WorkoutTiles.Add(tile);
        }

        WorkoutGamesLabel.Text = $"{_playOrder.Count} games";
    }

    static IReadOnlyList<WorkoutTileViewModel> BuildWorkoutTiles()
    {
        return new[]
        {
            new WorkoutTileViewModel("Word Fresh", "Language", "#6B63F5", "#4035D1", "▦", false, () => PageTransitionService.GoToAsync(nameof(WordFreshPage))),
            new WorkoutTileViewModel("Square Numbers", "Problem Solving", "#4FD37B", "#1E9F48", "12", false, () => PageTransitionService.GoToAsync(nameof(SquareNumbersPage))),
            new WorkoutTileViewModel("Perilous Path", "Memory", "#FFBC47", "#E18A00", "◇", false, () => PageTransitionService.GoToAsync(nameof(PerilousPathPage))),
            new WorkoutTileViewModel("Must Sort", "Focus", "#FF6483", "#DD3359", "△", false, () => PageTransitionService.GoToAsync(nameof(MustSortPage))),
            new WorkoutTileViewModel("Turtle Traffic", "Mental Agility", "#44A7FF", "#197BDB", "↺", false, () => PageTransitionService.PushAsync(Shell.Current!.Navigation, new TurtleTrafficPage())),
            new WorkoutTileViewModel("Emotion Lens", "Emotion", "#C56AF8", "#9044C8", "♡", true, null)
        };
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnTileTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable ||
            bindable.BindingContext is not WorkoutTileViewModel tile)
        {
            return;
        }

        if (sender is VisualElement visual)
        {
            await InteractionEffects.AnimateTapAsync(visual);
        }

        if (tile.IsLocked || tile.OpenAsync is null)
        {
            await PageTransitionService.GoToAsync(
                nameof(ComingSoonPage),
                new Dictionary<string, object> { { "GameTitle", tile.Title } });
            return;
        }

        await tile.OpenAsync();
    }

    async void OnStartWorkoutClicked(object sender, EventArgs e)
    {
        await InteractionEffects.AnimateTapAsync(StartWorkoutButton);

        var firstPlayable = _playOrder.FirstOrDefault(tile => !tile.IsLocked && tile.OpenAsync is not null);
        if (firstPlayable?.OpenAsync is not null)
        {
            await firstPlayable.OpenAsync();
        }
    }
}

public sealed class WorkoutTileViewModel
{
    public string Title { get; }
    public string Category { get; }
    public string AccentColor { get; }
    public string AccentDeepColor { get; }
    public string Glyph { get; }
    public bool IsLocked { get; }
    public Func<Task>? OpenAsync { get; }
    public string TileAColor => HexWithAlpha(AccentColor, 0.24f);
    public string TileBColor => HexWithAlpha(AccentColor, 0.38f);
    public string TileCColor => HexWithAlpha(AccentColor, 0.52f);
    public string TileDColor => HexWithAlpha(AccentColor, 0.16f);

    public WorkoutTileViewModel(string title, string category, string accentColor, string accentDeepColor, string glyph, bool isLocked, Func<Task>? openAsync)
    {
        Title = title;
        Category = category;
        AccentColor = accentColor;
        AccentDeepColor = accentDeepColor;
        Glyph = glyph;
        IsLocked = isLocked;
        OpenAsync = openAsync;
    }

    static string HexWithAlpha(string hex, float alpha)
    {
        var color = Color.FromArgb(hex);
        byte a = (byte)Math.Round(Math.Clamp(alpha, 0f, 1f) * 255);
        byte r = (byte)Math.Round(Math.Clamp(color.Red, 0f, 1f) * 255);
        byte g = (byte)Math.Round(Math.Clamp(color.Green, 0f, 1f) * 255);
        byte b = (byte)Math.Round(Math.Clamp(color.Blue, 0f, 1f) * 255);
        return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }
}
