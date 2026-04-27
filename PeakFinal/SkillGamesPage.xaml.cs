using System.Collections.ObjectModel;

namespace Peak;

public partial class SkillGamesPage : ContentPage
{
    private readonly Action _initializePageChrome;
    private bool _hasLoaded;
    public ObservableCollection<SkillGameCardViewModel> Games { get; } = new();
    private readonly Func<IReadOnlyList<SkillGameCardViewModel>> _gamesFactory;

    public SkillGamesPage(BrainSkill skill, string title, string subtitle, string accentHex, string accentDeepHex)
    {
        InitializeComponent();
        _gamesFactory = () => BuildGamesForSkill(skill, accentHex, accentDeepHex);
        _initializePageChrome = () =>
        {
            SkillTitleLabel.Text = title;
            SkillSubtitleLabel.Text = subtitle;
            HeroGradientStart.Color = Color.FromArgb(accentHex);
            HeroGradientEnd.Color = Color.FromArgb(accentDeepHex);
            HeaderEyebrowLabel.Text = "Skill collection";
            ApplyHeroIcon(skill);
        };
    }

    public SkillGamesPage(
        string eyebrow,
        string title,
        string subtitle,
        string accentHex,
        string accentDeepHex,
        string heroIconSource,
        IEnumerable<SkillGameCardViewModel> games)
    {
        InitializeComponent();
        var seededGames = games.ToList();
        _gamesFactory = () => seededGames;
        _initializePageChrome = () =>
        {
            HeaderEyebrowLabel.Text = eyebrow;
            SkillTitleLabel.Text = title;
            SkillSubtitleLabel.Text = subtitle;
            HeroGradientStart.Color = Color.FromArgb(accentHex);
            HeroGradientEnd.Color = Color.FromArgb(accentDeepHex);
            ApplyHeroIcon(heroIconSource);
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_hasLoaded)
        {
            BindingContext = this;
            _initializePageChrome();
            _hasLoaded = true;
        }

        ReloadGames();
    }

    public static IReadOnlyList<SkillGameCardViewModel> BuildGamesForSkill(BrainSkill skill, string accentHex, string accentDeepHex)
    {
        return skill switch
        {
            BrainSkill.ProblemSolving => new[]
            {
                Card("Matcha Madness", "Creative pattern reasoning", accentHex, accentDeepHex, "MM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Matcha Madness")),
                Card("Moving Math", "Math under pressure", accentHex, accentDeepHex, "+", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Moving Math")),
                Card("Square Numbers", "Numerical pattern spotting", accentHex, accentDeepHex, "12", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Square Numbers")),
                Card("Pixel Logic", "Logic-grid challenge", accentHex, accentDeepHex, "PL", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Pixel Logic"))
            },
            BrainSkill.Language => new[]
            {
                Card("Word Fresh", "Fast verbal flexibility", accentHex, accentDeepHex, "WF", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Fresh")),
                Card("Word-A-Like", "Verbal pattern links", accentHex, accentDeepHex, "Aa", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word-A-Like")),
                Card("Babble Bots", "Word-building challenge", accentHex, accentDeepHex, "BB", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Babble Bots")),
                Card("Word Hunt", "Hidden word search", accentHex, accentDeepHex, "WH", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Word Hunt")),
                Card("Grow", "Prefix-building challenge", accentHex, accentDeepHex, "GR", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Grow"))
            },
            BrainSkill.Memory => new[]
            {
                Card("Perilous Path", "Spatial change recall", accentHex, accentDeepHex, "PP", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Perilous Path")),
                Card("Partial Match", "Resolve near-matches", accentHex, accentDeepHex, "PM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Partial Match")),
                Card("Spin Cycle", "Memory sequencing", accentHex, accentDeepHex, "SC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Spin Cycle")),
                Card("Memory Match", "Classic recall pacing", accentHex, accentDeepHex, "MM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Memory Match"))
            },
            BrainSkill.Focus => new[]
            {
                Card("Decoder", "Decode before time runs out", accentHex, accentDeepHex, true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Decoder"), iconSource: "focus_decoder_icon.svg"),
                Card("Must Sort", "Filter and sort fast", accentHex, accentDeepHex, true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Must Sort"), iconSource: "focus_mustsort_icon.svg"),
                Card("Tap Trap", "Avoid the wrong tap", accentHex, accentDeepHex, true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Tap Trap"), iconSource: "focus_taptrap_icon.svg"),
                Card("Unique", "Spot the odd one out", accentHex, accentDeepHex, true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Unique"), iconSource: "focus_unique_icon.svg")
            },
            BrainSkill.MentalAgility => new[]
            {
                Card("Turtle Traffic", "Switch lanes and adapt fast", accentHex, accentDeepHex, "TR", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Turtle Traffic")),
                Card("True Color", "Shift rules under pressure", accentHex, accentDeepHex, "TC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "True Color")),
                Card("Face Switch", "Rule-switching speed", accentHex, accentDeepHex, "FS", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Face Switch")),
                Card("Speed Spotting", "Rapid visual scanning", accentHex, accentDeepHex, "SS", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Speed Spotting"))
            },
            BrainSkill.Emotion => new[]
            {
                Card("Smile On Me", "Read positive emotional cues", accentHex, accentDeepHex, "SM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Smile On Me")),
                Card("Face To Face", "Interpret human reactions", accentHex, accentDeepHex, "FF", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Face To Face")),
                Card("Mood Match", "Match emotion and context", accentHex, accentDeepHex, "MM", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Mood Match")),
                Card("Empathy Choice", "Choose the healthiest response", accentHex, accentDeepHex, "EC", true, () => GameEntryNavigationService.OpenByTitleAsync(Shell.Current!.Navigation, "Face To Face"))
            },
            _ => new[]
            {
                Card("Skill Pack", "More training coming soon", accentHex, accentDeepHex, "SP", false, null)
            }
        };
    }

    void ApplyHeroIcon(BrainSkill skill)
    {
        var source = skill switch
        {
            BrainSkill.Language => "daily_language_logo.png",
            BrainSkill.Memory => "daily_memory_logo.png",
            BrainSkill.ProblemSolving => "daily_problem_solving_logo.png",
            BrainSkill.Focus => "daily_focus_logo.png",
            BrainSkill.MentalAgility => "daily_mental_agility_logo.png",
            BrainSkill.Emotion => "daily_emotion_logo.svg",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(source))
        {
            SkillHeroIconFrame.IsVisible = false;
            return;
        }

        SkillHeroIconFrame.IsVisible = true;
        SkillHeroIconFrame.BackgroundColor = Colors.Transparent;
        SkillHeroIconImage.Source = source;
    }

    void ApplyHeroIcon(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            SkillHeroIconFrame.IsVisible = false;
            return;
        }

        SkillHeroIconFrame.IsVisible = true;
        SkillHeroIconFrame.BackgroundColor = Colors.Transparent;
        SkillHeroIconImage.Source = source;
    }

    static SkillGameCardViewModel Card(string title, string shortSubtitle, string accentHex, string accentDeepHex, string glyph, bool isPlayable, Func<Task>? openAsync, string? iconSource = null)
        => new(title, shortSubtitle, accentHex, accentDeepHex, glyph, iconSource, isPlayable, openAsync);

    static SkillGameCardViewModel Card(string title, string shortSubtitle, string accentHex, string accentDeepHex, bool isPlayable, Func<Task>? openAsync, string iconSource)
        => new(title, shortSubtitle, accentHex, accentDeepHex, string.Empty, iconSource, isPlayable, openAsync);

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnGameTapped(object sender, TappedEventArgs e)
    {
        if (sender is not BindableObject bindable || bindable.BindingContext is not SkillGameCardViewModel game)
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
                    ReloadGames();
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

        if (game.OpenAsync is not null)
        {
            await game.OpenAsync();
            return;
        }

        await PageTransitionService.GoToAsync(nameof(ComingSoonPage), new Dictionary<string, object> { { "GameTitle", game.Title } });
    }

    void ReloadGames()
    {
        Games.Clear();
        foreach (var game in _gamesFactory())
        {
            Games.Add(game);
        }
    }
}

public sealed class SkillGameCardViewModel
{
    public string Title { get; }
    public string ShortSubtitle { get; }
    public string AccentColor { get; }
    public string AccentDeepColor { get; }
    public string Glyph { get; }
    public string IconSource { get; }
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconSource);
    public bool HasGlyph => !HasIcon && !string.IsNullOrWhiteSpace(Glyph);
    public bool IsPlayable { get; }
    public bool IsUnlocked { get; }
    public bool IsLocked => !IsUnlocked;
    public string UnlockRequirementText { get; }
    public Func<Task>? OpenAsync { get; }
    public string TileAColor => HexWithAlpha(AccentColor, 0.24f);
    public string TileBColor => HexWithAlpha(AccentColor, 0.36f);
    public string TileCColor => HexWithAlpha(AccentColor, 0.48f);
    public string TileDColor => HexWithAlpha(AccentColor, 0.18f);
    public double CardOpacity => IsUnlocked ? 1 : 0.86;
    public double IconOpacity => IsUnlocked ? 1 : 0.88;

    public SkillGameCardViewModel(string title, string shortSubtitle, string accentColor, string accentDeepColor, string glyph, string? iconSource, bool isPlayable, Func<Task>? openAsync)
    {
        var unlockState = GameUnlockService.GetState(title);
        Title = title;
        ShortSubtitle = unlockState.IsUnlocked ? shortSubtitle : unlockState.RequirementText;
        AccentColor = accentColor;
        AccentDeepColor = accentDeepColor;
        Glyph = glyph;
        IconSource = iconSource ?? string.Empty;
        IsPlayable = isPlayable && unlockState.IsUnlocked;
        IsUnlocked = unlockState.IsUnlocked;
        UnlockRequirementText = unlockState.RequirementText;
        OpenAsync = unlockState.IsUnlocked ? openAsync : null;
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
