using System.Text.Json;

namespace Peak;

public sealed record GameUnlockState(
    bool IsUnlocked,
    string RequirementText,
    int UnlockCost = 0,
    bool CanUnlockWithPoints = false);

public sealed record GameStoreEntry(
    string Title,
    string SourceId,
    BrainSkill Skill,
    int UnlockCost,
    bool Starter,
    string AccentHex,
    string AccentDeepHex,
    string IconSource);

public static class GameUnlockService
{
    const string UnlockedGamesPrefix = "apex_unlocked_games_v1_";

    static readonly IReadOnlyList<GameStoreEntry> Catalog =
    [
        new("Word Fresh", "word_fresh", BrainSkill.Language, 0, true, "#6B63F5", "#3F39CE", "word_fresh_icon.svg"),
        new("Word-A-Like", "word_a_like", BrainSkill.Language, 4500, false, "#6B63F5", "#3F39CE", "word_alike_icon.svg"),
        new("Babble Bots", "babble_bots", BrainSkill.Language, 6500, false, "#6B63F5", "#3F39CE", "babble_bots_icon.svg"),
        new("Word Hunt", "word_hunt", BrainSkill.Language, 9000, false, "#6B63F5", "#3F39CE", "word_hunt_icon.svg"),
        new("Grow", "grow", BrainSkill.Language, 12500, false, "#6B63F5", "#3F39CE", "grow_icon.svg"),

        new("Perilous Path", "perilous_path", BrainSkill.Memory, 0, true, "#FFBC47", "#E18A00", "perilous_path_icon.svg"),
        new("Partial Match", "partial_match", BrainSkill.Memory, 5000, false, "#FFBC47", "#E18A00", "partial_match_icon.svg"),
        new("Spin Cycle", "spin_cycle", BrainSkill.Memory, 7200, false, "#FFBC47", "#E18A00", "spin_cycle_icon.svg"),
        new("Memory Match", "memory_match", BrainSkill.Memory, 9800, false, "#FFBC47", "#E18A00", "memory_match_icon.svg"),
        new("Baggage Claim", "baggage_claim", BrainSkill.Memory, 13500, false, "#FFBC47", "#E18A00", "baggage_claim_icon.svg"),

        new("Matcha Madness", "matcha_madness", BrainSkill.ProblemSolving, 0, true, "#4FD37B", "#1E9F48", "matcha_madness_icon.svg"),
        new("Moving Math", "moving_math", BrainSkill.ProblemSolving, 5400, false, "#4FD37B", "#1E9F48", "moving_math_icon.svg"),
        new("Square Numbers", "square_numbers", BrainSkill.ProblemSolving, 7600, false, "#4FD37B", "#1E9F48", "square_numbers_icon.svg"),
        new("Pixel Logic", "pixel_logic", BrainSkill.ProblemSolving, 10400, false, "#4FD37B", "#1E9F48", "pixel_logic_icon.svg"),
        new("Low Pop", "low_pop", BrainSkill.ProblemSolving, 14500, false, "#4FD37B", "#1E9F48", "low_pop_icon.svg"),

        new("Decoder", "decoder", BrainSkill.Focus, 6500, false, "#FF6483", "#DD3359", "focus_decoder_icon.svg"),
        new("Must Sort", "must_sort", BrainSkill.Focus, 8600, false, "#FF6483", "#DD3359", "focus_mustsort_icon.svg"),
        new("Tap Trap", "tap_trap", BrainSkill.Focus, 11000, false, "#FF6483", "#DD3359", "focus_taptrap_icon.svg"),
        new("True Color", "true_color", BrainSkill.Focus, 13800, false, "#FF6483", "#DD3359", "true_color_icon.svg"),
        new("Unique", "unique", BrainSkill.Focus, 17000, false, "#FF6483", "#DD3359", "focus_unique_icon.svg"),

        new("Turtle Traffic", "turtle_traffic", BrainSkill.MentalAgility, 9200, false, "#44A7FF", "#197BDB", "turtle_traffic_icon.svg"),
        new("Face Switch", "face_switch", BrainSkill.MentalAgility, 12000, false, "#44A7FF", "#197BDB", "face_switch_icon.svg"),
        new("Speed Spotting", "speed_spotting", BrainSkill.MentalAgility, 15000, false, "#44A7FF", "#197BDB", "speed_spotting_icon.svg"),

        new("Smile On Me", "smile_on_me", BrainSkill.Emotion, 10500, false, "#C56AF8", "#9044C8", "smile_on_me_icon.svg"),
        new("Face To Face", "face_to_face", BrainSkill.Emotion, 13800, false, "#C56AF8", "#9044C8", "face_to_face_icon.svg"),
        new("Mood Match", "mood_match", BrainSkill.Emotion, 17200, false, "#C56AF8", "#9044C8", "mood_match_icon.svg"),
        new("Empathy Choice", "empathy_choice", BrainSkill.Emotion, 21000, false, "#C56AF8", "#9044C8", "mood_match_icon.svg")
    ];

    public static IReadOnlyList<GameStoreEntry> GetCatalog() => Catalog;

    public static IReadOnlyList<GameStoreEntry> GetUnlockedGames()
        => Catalog.Where(item => IsUnlocked(item.Title)).ToList();

    public static GameStoreEntry? GetEntry(string gameTitle)
        => Catalog.FirstOrDefault(item => string.Equals(item.Title, gameTitle, StringComparison.OrdinalIgnoreCase));

    public static GameStoreEntry? GetEntryBySourceId(string sourceId)
        => Catalog.FirstOrDefault(item => string.Equals(item.SourceId, sourceId, StringComparison.OrdinalIgnoreCase));

    public static bool IsUnlocked(string gameTitle)
    {
        var entry = GetEntry(gameTitle);
        if (entry is null)
        {
            return true;
        }

        if (entry.Starter)
        {
            return true;
        }

        var unlocked = LoadUnlockedTitles();
        return unlocked.Contains(entry.Title, StringComparer.OrdinalIgnoreCase);
    }

    public static GameUnlockState GetState(string gameTitle)
    {
        var entry = GetEntry(gameTitle);
        if (entry is null)
        {
            return new GameUnlockState(true, "Playable");
        }

        if (IsUnlocked(entry.Title))
        {
            return entry.Starter
                ? new GameUnlockState(true, "Starter game")
                : new GameUnlockState(true, "Unlocked");
        }

        return new GameUnlockState(
            IsUnlocked: false,
            RequirementText: $"Unlock for {entry.UnlockCost:N0} points.",
            UnlockCost: entry.UnlockCost,
            CanUnlockWithPoints: true);
    }

    public static bool TryUnlock(string gameTitle, out string message)
    {
        var entry = GetEntry(gameTitle);
        if (entry is null)
        {
            message = "This game is already available.";
            return true;
        }

        if (IsUnlocked(entry.Title))
        {
            message = $"{entry.Title} is already unlocked.";
            return true;
        }

        if (!GamePointsService.TrySpendPoints(entry.UnlockCost, out message))
        {
            return false;
        }

        var unlocked = LoadUnlockedTitles();
        unlocked.Add(entry.Title);
        SaveUnlockedTitles(unlocked);
        message = $"{entry.Title} unlocked for {entry.UnlockCost:N0} points.";
        return true;
    }

    static HashSet<string> LoadUnlockedTitles()
    {
        var raw = Preferences.Default.Get(GetUnlockedKey(), string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    static void SaveUnlockedTitles(HashSet<string> unlocked)
    {
        Preferences.Default.Set(GetUnlockedKey(), JsonSerializer.Serialize(unlocked.OrderBy(x => x)));
    }

    static string GetUnlockedKey() => $"{UnlockedGamesPrefix}{GetUserKey()}";

    static string GetUserKey()
    {
        var username = LocalAccountStore.GetLastActiveUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            return "guest";
        }

        return new string(username.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
    }
}
