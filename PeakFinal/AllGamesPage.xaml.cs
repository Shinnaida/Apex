using System.Collections.ObjectModel;

namespace Peak;

public partial class AllGamesPage : ContentPage
{
    private bool _hasLoaded;
    public ObservableCollection<GamesSectionViewModel> VisibleSections { get; } = new();

    public AllGamesPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasLoaded)
        {
            BindingContext = this;
            _hasLoaded = true;
        }

        ReloadSections();
    }

    async void OnGameTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable ||
            bindable.BindingContext is not GameTileViewModel game)
        {
            return;
        }

        if (sender is VisualElement visual)
        {
            await InteractionEffects.AnimateTapAsync(visual);
        }

        var latestState = GameUnlockService.GetState(game.Title);
        if (!latestState.IsUnlocked)
        {
            var entry = GameUnlockService.GetEntry(game.Title);
            var shouldUnlock = latestState.CanUnlockWithPoints &&
                               entry is not null &&
                               await UnlockGameSheetPage.ShowAsync(Navigation, entry);

            if (shouldUnlock)
            {
                if (GameUnlockService.TryUnlock(game.Title, out var message))
                {
                    await GameAudioService.PlayWinAsync();
                    ReloadSections();
                    await DisplayAlert("Unlocked", message, "Nice");
                }
                else
                {
                    await DisplayAlert("Locked", message, "OK");
                }
            }
            else if (!latestState.CanUnlockWithPoints)
            {
                await DisplayAlert("Game Locked", latestState.RequirementText, "OK");
            }

            return;
        }

        if (game.RouteFactory is not null)
        {
            await game.RouteFactory();
            return;
        }

        await PageTransitionService.GoToAsync(nameof(ComingSoonPage), new Dictionary<string, object> { { "GameTitle", game.Title } });
    }

    void ReloadSections()
    {
        ApexPointsLabel.Text = GamePointsService.GetBalance().ToString("N0");
        VisibleSections.Clear();
        foreach (var section in BuildSkillLibrarySections())
        {
            VisibleSections.Add(section);
        }
    }

    async void OnSectionTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable ||
            bindable.BindingContext is not GamesSectionViewModel section)
        {
            return;
        }

        if (TryBuildSkillPage(section.Title, out var page))
        {
            if (sender is VisualElement visual)
            {
                await InteractionEffects.AnimateTapAsync(visual);
            }

            await PageTransitionService.PushAsync(Navigation, page);
        }
    }

    static IReadOnlyList<GamesSectionViewModel> BuildSkillLibrarySections()
    {
        return new[]
        {
            BuildSection("Language", "Play with words, patterns, and verbal speed", "#6256F4", new[]
            {
                Tile("Word Fresh", "Playable now", "#6B63F5", "#3F39CE", "WF", "WF", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Fresh"), "word_fresh_icon.svg"),
                Tile("Word-A-Like", "Playable now", "#6B63F5", "#3F39CE", "WA", "Aa", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word-A-Like"), "word_alike_icon.svg"),
                Tile("Babble Bots", "Playable now", "#6B63F5", "#3F39CE", "BB", "BB", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Babble Bots"), "babble_bots_icon.svg"),
                Tile("Word Hunt", "Playable now", "#6B63F5", "#3F39CE", "WH", "WH", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Hunt"), "word_hunt_icon.svg"),
                Tile("Grow", "Playable now", "#6B63F5", "#3F39CE", "GR", "GR", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Grow"), "grow_icon.svg")
            }),
            BuildSection("Memory", "Recall, matching, and sequence control", "#F2A41F", new[]
            {
                Tile("Perilous Path", "Playable now", "#FFBC47", "#E18A00", "PP", "PP", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Perilous Path"), "perilous_path_icon.svg"),
                Tile("Partial Match", "Playable now", "#FFBC47", "#E18A00", "PM", "PM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Partial Match"), "partial_match_icon.svg"),
                Tile("Spin Cycle", "Playable now", "#FFBC47", "#E18A00", "SC", "SC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Spin Cycle"), "spin_cycle_icon.svg"),
                Tile("Memory Match", "Playable now", "#FFBC47", "#E18A00", "MM", "MM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Memory Match"), "memory_match_icon.svg"),
                Tile("Baggage Claim", "Playable now", "#FFBC47", "#E18A00", "BC", "BC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Baggage Claim"), "baggage_claim_icon.svg")
            }),
            BuildSection("Problem Solving", "Math, logic, and pattern building", "#2CC768", new[]
            {
                Tile("Matcha Madness", "Playable now", "#4FD37B", "#1E9F48", "MM", "MM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Matcha Madness"), "matcha_madness_icon.svg"),
                Tile("Moving Math", "Playable now", "#4FD37B", "#1E9F48", "MV", "+", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Moving Math"), "moving_math_icon.svg"),
                Tile("Square Numbers", "Playable now", "#4FD37B", "#1E9F48", "SQ", "12", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Square Numbers"), "square_numbers_icon.svg"),
                Tile("Pixel Logic", "Playable now", "#4FD37B", "#1E9F48", "PL", "PL", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Pixel Logic"), "pixel_logic_icon.svg"),
                Tile("Low Pop", "Playable now", "#4FD37B", "#1E9F48", "LP", "LP", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Low Pop"), "low_pop_icon.svg")
            }),
            BuildSection("Focus", "Speed, filtering, and response control", "#F04472", new[]
            {
                Tile("Decoder", "Playable now", "#FF6483", "#DD3359", "DC", "DC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Decoder"), "focus_decoder_icon.svg"),
                Tile("Must Sort", "Playable now", "#FF6483", "#DD3359", "MS", "MS", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Must Sort"), "focus_mustsort_icon.svg"),
                Tile("Tap Trap", "Playable now", "#FF6483", "#DD3359", "TT", "TT", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Tap Trap"), "focus_taptrap_icon.svg"),
                Tile("True Color", "Playable now", "#FF6483", "#DD3359", "TC", "TC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "True Color"), "true_color_icon.svg"),
                Tile("Unique", "Playable now", "#FF6483", "#DD3359", "UN", "UN", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Unique"), "focus_unique_icon.svg")
            }),
            BuildSection("Mental Agility", "Switching, multitasking, and adaptation", "#2B8EF4", new[]
            {
                Tile("Turtle Traffic", "Playable now", "#44A7FF", "#197BDB", "TR", "TR", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Turtle Traffic"), "turtle_traffic_icon.svg"),
                Tile("True Color", "Playable now", "#44A7FF", "#197BDB", "TC", "TC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "True Color"), "true_color_icon.svg"),
                Tile("Face Switch", "Playable now", "#44A7FF", "#197BDB", "FS", "FS", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Face Switch"), "face_switch_icon.svg"),
                Tile("Speed Spotting", "Playable now", "#44A7FF", "#197BDB", "SS", "SS", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Speed Spotting"), "speed_spotting_icon.svg")
            }),
            BuildSection("Emotion", "Empathy, response, and self-regulation", "#B85FF3", new[]
            {
                Tile("Smile On Me", "Playable now", "#C56AF8", "#9044C8", "SM", "SM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Smile On Me"), "smile_on_me_icon.svg"),
                Tile("Face To Face", "Playable now", "#C56AF8", "#9044C8", "FF", "FF", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Face To Face"), "face_to_face_icon.svg"),
                Tile("Mood Match", "Playable now", "#C56AF8", "#9044C8", "MM", "MM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Mood Match"), "mood_match_icon.svg")
            })
        };
    }

    static GamesSectionViewModel BuildSection(string title, string subtitle, string accentColor, IEnumerable<GameTileViewModel> games)
        => new(title, subtitle, accentColor, games);

    static GameTileViewModel Tile(string title, string meta, string accentColor, string accentDeepColor, string iconText, string glyph, bool playable, Func<Task>? routeFactory, string? iconSource = null)
        => new(title, meta, accentColor, accentDeepColor, iconText, glyph, iconSource, playable, routeFactory);

    static bool TryBuildSkillPage(string title, out ContentPage page)
    {
        switch (title)
        {
            case "Problem Solving":
                page = new SkillGamesPage(BrainSkill.ProblemSolving, "Problem Solving", "Games to make you think creatively.", "#67E66F", "#1E9F48");
                return true;
            case "Language":
                page = new SkillGamesPage(BrainSkill.Language, "Language", "Games to challenge your language skills.", "#6B63F5", "#3F39CE");
                return true;
            case "Memory":
                page = new SkillGamesPage(BrainSkill.Memory, "Memory", "Games to keep your memory bank active.", "#F7B038", "#E18A00");
                return true;
            case "Focus":
                page = new SkillGamesPage(BrainSkill.Focus, "Focus", "Games to keep your mind on point.", "#FF6A78", "#DD3359");
                return true;
            case "Mental Agility":
                page = new SkillGamesPage(BrainSkill.MentalAgility, "Mental Agility", "Games to help you move between tasks easily.", "#44A7FF", "#197BDB");
                return true;
            case "Emotion":
                page = new SkillGamesPage(BrainSkill.Emotion, "Emotion", "Games to help you deal with the world around us.", "#B65AF6", "#9747E5");
                return true;
            default:
                page = null!;
                return false;
        }
    }
}

public sealed class GamesSectionViewModel
{
    public string Title { get; }
    public string Subtitle { get; }
    public string AccentColor { get; }
    public string BadgeBackground { get; }
    public string SectionBackground => HexWithAlpha(AccentColor, 0.08f);
    public string SectionStroke => HexWithAlpha(AccentColor, 0.18f);
    public string SectionLogoSource { get; }
    public string SectionLogoBackground { get; }
    public bool HasSectionLogo => !string.IsNullOrWhiteSpace(SectionLogoSource);
    public ObservableCollection<GameTileViewModel> Games { get; }
    public string CountText => $"{Games.Count} games";

    public GamesSectionViewModel(string title, string subtitle, string accentColor, IEnumerable<GameTileViewModel> games)
    {
        Title = title;
        Subtitle = subtitle;
        AccentColor = accentColor;
        BadgeBackground = HexWithAlpha(accentColor, 0.14f);
        SectionLogoSource = ResolveSectionLogo(title);
        SectionLogoBackground = string.IsNullOrWhiteSpace(SectionLogoSource) ? accentColor : "#00000000";
        Games = new ObservableCollection<GameTileViewModel>(games);
    }

    static string ResolveSectionLogo(string title)
    {
        return title switch
        {
            "Language" => "daily_language_logo.png",
            "Memory" => "daily_memory_logo.png",
            "Problem Solving" => "daily_problem_solving_logo.png",
            "Focus" => "daily_focus_logo.png",
            "Mental Agility" => "daily_mental_agility_logo.png",
            "Emotion" => "daily_emotion_logo.svg",
            _ => string.Empty
        };
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

public sealed class GameTileViewModel
{
    public string Title { get; }
    public string Meta { get; }
    public string AccentColor { get; }
    public string AccentDeepColor { get; }
    public string CardSurface => HexWithAlpha(AccentColor, 0.10f);
    public string ForegroundColor => "#FFFFFF";
    public string TileAColor => HexWithAlpha(AccentColor, 0.26f);
    public string TileBColor => HexWithAlpha(AccentColor, 0.38f);
    public string TileCColor => HexWithAlpha(AccentColor, 0.50f);
    public string TileDColor => HexWithAlpha(AccentColor, 0.18f);
    public string IconText { get; }
    public string Glyph { get; }
    public string IconSource { get; }
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconSource);
    public bool HasGlyph => !HasIcon && !string.IsNullOrWhiteSpace(Glyph);
    public bool IsPlayable { get; }
    public bool IsUnlocked { get; }
    public bool IsLocked => !IsUnlocked;
    public string UnlockRequirementText { get; }
    public Func<Task>? RouteFactory { get; }
    public double CardOpacity => IsUnlocked ? 1 : 0.86;
    public double IconOpacity => IsUnlocked ? 1 : 0.88;
    public string BadgeBackgroundColor => IsUnlocked ? "#18C99B" : "#E5E9EE";
    public string BadgeStrokeColor => IsUnlocked ? "#F7D86A" : "#B6BEC8";
    public string BadgeGlyphColor => IsUnlocked ? "#FFE984" : "#7E8792";
    public string BadgeText => IsUnlocked ? "GO" : string.Empty;
    public string ShadowBrush => IsUnlocked ? "#3D0F2740" : "#22000000";

    public GameTileViewModel(string title, string meta, string accentColor, string accentDeepColor, string iconText, string glyph, string? iconSource, bool isPlayable, Func<Task>? routeFactory)
    {
        var unlockState = GameUnlockService.GetState(title);
        Title = title;
        Meta = unlockState.IsUnlocked ? meta : unlockState.RequirementText;
        AccentColor = accentColor;
        AccentDeepColor = accentDeepColor;
        IconText = iconText;
        Glyph = glyph;
        IconSource = iconSource ?? string.Empty;
        IsPlayable = isPlayable && unlockState.IsUnlocked;
        IsUnlocked = unlockState.IsUnlocked;
        UnlockRequirementText = unlockState.RequirementText;
        RouteFactory = unlockState.IsUnlocked ? routeFactory : null;
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
