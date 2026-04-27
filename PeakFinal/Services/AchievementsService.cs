namespace Peak;

public sealed record AchievementItem(
    string Id,
    string Title,
    string Description,
    string IconSource,
    bool IsUnlocked,
    string ProgressText,
    string Category,
    double ProgressFraction);

public sealed record AchievementProgressSnapshot(
    BrainSkillScores Scores,
    int PeakScore,
    int RecordedSessions,
    int ActiveDays,
    int CurrentStreakDays,
    int DistinctGamesPlayed,
    int BestGamePeakScore,
    IReadOnlyCollection<string> SeenUnlockedIds,
    IReadOnlyCollection<string> UnlockedIds);

public static class AchievementsService
{
    private const string SeenUnlockedPrefix = "achievements_seen_unlocked_";

    private sealed record AchievementRule(
        string Id,
        string Title,
        string Description,
        string IconSource,
        string Category,
        Func<AchievementProgressSnapshot, int> CurrentValue,
        int TargetValue);

    private static readonly AchievementRule[] Rules =
    {
        new(
            Id: "first_steps",
            Title: "First Steps",
            Description: "Complete your first tracked game session.",
            IconSource: "ach_first_steps.svg",
            Category: "Journey",
            CurrentValue: s => s.RecordedSessions,
            TargetValue: 1),
        new(
            Id: "on_the_trail",
            Title: "On The Trail",
            Description: "Complete 5 tracked game sessions.",
            IconSource: "ach_on_the_trail.svg",
            Category: "Journey",
            CurrentValue: s => s.RecordedSessions,
            TargetValue: 5),
        new(
            Id: "expedition",
            Title: "Expedition",
            Description: "Complete 15 tracked game sessions.",
            IconSource: "ach_expedition.svg",
            Category: "Journey",
            CurrentValue: s => s.RecordedSessions,
            TargetValue: 15),
        new(
            Id: "marathon_mode",
            Title: "Marathon Mode",
            Description: "Complete 30 tracked game sessions.",
            IconSource: "ach_expedition.svg",
            Category: "Journey",
            CurrentValue: s => s.RecordedSessions,
            TargetValue: 30),
        new(
            Id: "steady_climber",
            Title: "Steady Climber",
            Description: "Train on 3 different days.",
            IconSource: "ach_steady_climber.svg",
            Category: "Consistency",
            CurrentValue: s => s.ActiveDays,
            TargetValue: 3),
        new(
            Id: "trailblazer",
            Title: "Trailblazer",
            Description: "Train on 7 different days.",
            IconSource: "ach_steady_climber.svg",
            Category: "Consistency",
            CurrentValue: s => s.ActiveDays,
            TargetValue: 7),
        new(
            Id: "daily_rhythm",
            Title: "Daily Rhythm",
            Description: "Keep a 3-day training streak alive.",
            IconSource: "ach_focus_lock.svg",
            Category: "Consistency",
            CurrentValue: s => s.CurrentStreakDays,
            TargetValue: 3),
        new(
            Id: "weeklong_rhythm",
            Title: "Weeklong Rhythm",
            Description: "Keep a 7-day training streak alive.",
            IconSource: "rank_summit.svg",
            Category: "Consistency",
            CurrentValue: s => s.CurrentStreakDays,
            TargetValue: 7),
        new(
            Id: "game_sampler",
            Title: "Game Sampler",
            Description: "Play 3 different games.",
            IconSource: "ach_on_the_trail.svg",
            Category: "Arcade",
            CurrentValue: s => s.DistinctGamesPlayed,
            TargetValue: 3),
        new(
            Id: "arcade_explorer",
            Title: "Arcade Explorer",
            Description: "Play 6 different games.",
            IconSource: "ach_all_terrain.svg",
            Category: "Arcade",
            CurrentValue: s => s.DistinctGamesPlayed,
            TargetValue: 6),
        new(
            Id: "ascent_badge",
            Title: "Ascent Badge",
            Description: "Reach the Ascent rank.",
            IconSource: "ach_ascent_badge.svg",
            Category: "Rank",
            CurrentValue: s => s.PeakScore,
            TargetValue: 720),
        new(
            Id: "summit_badge",
            Title: "Summit Badge",
            Description: "Reach the Summit rank.",
            IconSource: "ach_summit_badge.svg",
            Category: "Rank",
            CurrentValue: s => s.PeakScore,
            TargetValue: 790),
        new(
            Id: "apex_badge",
            Title: "Apex Badge",
            Description: "Reach the Apex rank.",
            IconSource: "rank_apex.svg",
            Category: "Rank",
            CurrentValue: s => s.PeakScore,
            TargetValue: 860),
        new(
            Id: "peak_badge",
            Title: "Peak Badge",
            Description: "Reach the Peak rank.",
            IconSource: "ach_peak_badge.svg",
            Category: "Rank",
            CurrentValue: s => s.PeakScore,
            TargetValue: 930),
        new(
            Id: "memory_surge",
            Title: "Memory Surge",
            Description: "Reach 170 in Memory.",
            IconSource: "ach_memory_surge.svg",
            Category: "Skill",
            CurrentValue: s => s.Scores.Memory,
            TargetValue: 170),
        new(
            Id: "logic_fire",
            Title: "Logic Fire",
            Description: "Reach 170 in Problem Solving.",
            IconSource: "ach_logic_fire.svg",
            Category: "Skill",
            CurrentValue: s => s.Scores.ProblemSolving,
            TargetValue: 170),
        new(
            Id: "word_lift",
            Title: "Word Lift",
            Description: "Reach 170 in Language.",
            IconSource: "ach_word_lift.svg",
            Category: "Skill",
            CurrentValue: s => s.Scores.Language,
            TargetValue: 170),
        new(
            Id: "focus_lock",
            Title: "Focus Lock",
            Description: "Reach 170 in Focus.",
            IconSource: "ach_focus_lock.svg",
            Category: "Skill",
            CurrentValue: s => s.Scores.Focus,
            TargetValue: 170),
        new(
            Id: "agility_spark",
            Title: "Agility Spark",
            Description: "Reach 165 in Mental Agility.",
            IconSource: "rank_ridge.svg",
            Category: "Skill",
            CurrentValue: s => s.Scores.MentalAgility,
            TargetValue: 165),
        new(
            Id: "high_score_hunter",
            Title: "High Score Hunter",
            Description: "Reach 900 in any game.",
            IconSource: "rank_peak.svg",
            Category: "Arcade",
            CurrentValue: s => s.BestGamePeakScore,
            TargetValue: 900),
        new(
            Id: "all_terrain",
            Title: "All Terrain",
            Description: "Reach at least 160 in all four skills.",
            IconSource: "ach_all_terrain.svg",
            Category: "Skill",
            CurrentValue: s => new[]
            {
                s.Scores.Memory,
                s.Scores.ProblemSolving,
                s.Scores.Language,
                s.Scores.Focus
            }.Min(),
            TargetValue: 160)
    };

    private static readonly StringComparer IdComparer = StringComparer.Ordinal;
    private static string _cachedUsernameKey = string.Empty;
    private static AchievementProgressSnapshot? _cachedSnapshot;

    public static IReadOnlyList<AchievementItem> GetAchievements()
    {
        return BuildAchievements(GetEffectiveSnapshot());
    }

    public static IReadOnlyList<AchievementItem> GetAchievementsFromUnlockedIds(IEnumerable<string> unlockedIds)
    {
        var unlockedSet = new HashSet<string>(
            unlockedIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            IdComparer);

        return Rules
            .Where(rule => unlockedSet.Contains(rule.Id))
            .Select(rule => BuildAchievementItem(rule, rule.TargetValue, true, rule.TargetValue))
            .ToList();
    }

    public static (int Unlocked, int Total) GetSummary()
    {
        var achievements = GetAchievements();
        return (achievements.Count(x => x.IsUnlocked), achievements.Count);
    }

    public static async Task RefreshCurrentUserAsync(bool syncLocalProgress = true)
    {
        var usernameKey = GetCurrentUsernameKey();
        var localSnapshot = BuildLocalSnapshot();
        var effectiveSnapshot = localSnapshot;

        if (!string.IsNullOrWhiteSpace(usernameKey))
        {
            try
            {
                if (syncLocalProgress)
                {
                    await OnlineAchievementsStore.SyncCurrentUserAsync(localSnapshot);
                }

                var remoteSnapshot = await OnlineAchievementsStore.GetCurrentUserSnapshotAsync();
                if (remoteSnapshot is not null)
                {
                    effectiveSnapshot = remoteSnapshot;
                }
            }
            catch
            {
                effectiveSnapshot = localSnapshot;
            }
        }

        CacheSnapshot(usernameKey, effectiveSnapshot);
    }

    public static async Task SyncCurrentUserProgressAsync()
    {
        var usernameKey = GetCurrentUsernameKey();
        var snapshot = BuildLocalSnapshot();
        CacheSnapshot(usernameKey, snapshot);

        if (string.IsNullOrWhiteSpace(usernameKey))
        {
            return;
        }

        try
        {
            await OnlineAchievementsStore.SyncCurrentUserAsync(snapshot);
        }
        catch
        {
            // Keep the local in-memory snapshot as fallback when the network is unavailable.
        }
    }

    public static async Task<IReadOnlyList<AchievementItem>> GetNewlyUnlockedAchievementsAsync()
    {
        await RefreshCurrentUserAsync();

        var snapshot = GetEffectiveSnapshot();
        var unlocked = BuildAchievements(snapshot)
            .Where(x => x.IsUnlocked)
            .ToList();

        if (unlocked.Count == 0)
        {
            return Array.Empty<AchievementItem>();
        }

        var seen = new HashSet<string>(snapshot.SeenUnlockedIds ?? Array.Empty<string>(), IdComparer);
        var newlyUnlocked = unlocked
            .Where(x => !seen.Contains(x.Id))
            .OrderBy(x => x.Title, StringComparer.Ordinal)
            .ToList();

        if (newlyUnlocked.Count == 0)
        {
            return Array.Empty<AchievementItem>();
        }

        foreach (var achievement in newlyUnlocked)
        {
            seen.Add(achievement.Id);
        }

        var refreshedSnapshot = BuildSnapshot(
            snapshot.Scores,
            snapshot.RecordedSessions,
            snapshot.ActiveDays,
            snapshot.CurrentStreakDays,
            snapshot.DistinctGamesPlayed,
            snapshot.BestGamePeakScore,
            seen);

        refreshedSnapshot = refreshedSnapshot with
        {
            UnlockedIds = refreshedSnapshot.UnlockedIds
                .Union(snapshot.UnlockedIds ?? Array.Empty<string>(), IdComparer)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList()
        };

        CacheSnapshot(GetCurrentUsernameKey(), refreshedSnapshot);
        SaveSeenUnlockedIds(seen);

        if (!string.IsNullOrWhiteSpace(GetCurrentUsernameKey()))
        {
            try
            {
                await OnlineAchievementsStore.SyncCurrentUserAsync(refreshedSnapshot);
            }
            catch
            {
                // Ignore online save failures here so the toast flow remains smooth.
            }
        }

        return newlyUnlocked;
    }

    private static IReadOnlyList<AchievementItem> BuildAchievements(AchievementProgressSnapshot snapshot)
    {
        var unlockedSet = new HashSet<string>(snapshot.UnlockedIds ?? Array.Empty<string>(), IdComparer);

        return Rules
            .Select(rule =>
            {
                var current = rule.CurrentValue(snapshot);
                var unlocked = unlockedSet.Contains(rule.Id) || current >= rule.TargetValue;
                return BuildAchievementItem(rule, current, unlocked, rule.TargetValue);
            })
            .OrderBy(x => x.IsUnlocked ? 0 : 1)
            .ThenBy(x => x.Title, StringComparer.Ordinal)
            .ToList();
    }

    private static AchievementItem BuildAchievementItem(
        AchievementRule rule,
        int currentValue,
        bool isUnlocked,
        int targetValue)
    {
        var safeTarget = Math.Max(1, targetValue);
        var normalizedProgress = isUnlocked
            ? 1
            : Math.Clamp(currentValue / (double)safeTarget, 0, 1);
        var progress = isUnlocked
            ? "Unlocked"
            : $"{Math.Min(currentValue, safeTarget)}/{safeTarget}";

        return new AchievementItem(
            Id: rule.Id,
            Title: rule.Title,
            Description: rule.Description,
            IconSource: rule.IconSource,
            IsUnlocked: isUnlocked,
            ProgressText: progress,
            Category: rule.Category,
            ProgressFraction: normalizedProgress);
    }

    private static AchievementProgressSnapshot GetEffectiveSnapshot()
    {
        var usernameKey = GetCurrentUsernameKey();
        if (!string.IsNullOrWhiteSpace(usernameKey)
            && string.Equals(usernameKey, _cachedUsernameKey, StringComparison.OrdinalIgnoreCase)
            && _cachedSnapshot is not null)
        {
            return _cachedSnapshot;
        }

        var snapshot = BuildLocalSnapshot();
        CacheSnapshot(usernameKey, snapshot);
        return snapshot;
    }

    private static AchievementProgressSnapshot BuildLocalSnapshot()
    {
        var scores = BrainScoreService.GetCurrentScores();
        var seen = LoadSeenUnlockedIds();
        var playedGames = BrainScoreService.GetPlayedGameScores();
        return BuildSnapshot(
            scores,
            BrainScoreService.GetRecordedSessionCount(),
            BrainScoreService.GetActiveDayCount(),
            BrainScoreService.GetCurrentStreakDays(),
            playedGames.Count,
            playedGames.Count == 0 ? 0 : playedGames.Max(game => game.PeakGameScore),
            seen);
    }

    private static AchievementProgressSnapshot BuildSnapshot(
        BrainSkillScores scores,
        int recordedSessions,
        int activeDays,
        int currentStreakDays,
        int distinctGamesPlayed,
        int bestGamePeakScore,
        IEnumerable<string> seenUnlockedIds)
    {
        var seen = new HashSet<string>(seenUnlockedIds.Where(id => !string.IsNullOrWhiteSpace(id)), IdComparer);

        var provisional = new AchievementProgressSnapshot(
            Scores: scores,
            PeakScore: scores.PeakScore,
            RecordedSessions: recordedSessions,
            ActiveDays: activeDays,
            CurrentStreakDays: currentStreakDays,
            DistinctGamesPlayed: distinctGamesPlayed,
            BestGamePeakScore: bestGamePeakScore,
            SeenUnlockedIds: seen.ToList(),
            UnlockedIds: Array.Empty<string>());

        var unlockedIds = Rules
            .Where(rule => rule.CurrentValue(provisional) >= rule.TargetValue)
            .Select(rule => rule.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        return provisional with { UnlockedIds = unlockedIds };
    }

    private static void CacheSnapshot(string usernameKey, AchievementProgressSnapshot snapshot)
    {
        _cachedUsernameKey = usernameKey;
        _cachedSnapshot = snapshot;
    }

    private static HashSet<string> LoadSeenUnlockedIds()
    {
        var key = GetSeenUnlockedKey();
        var raw = Preferences.Default.Get(key, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new HashSet<string>(IdComparer);
        }

        try
        {
            var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
            return new HashSet<string>(list.Where(id => !string.IsNullOrWhiteSpace(id)), IdComparer);
        }
        catch
        {
            return new HashSet<string>(IdComparer);
        }
    }

    private static void SaveSeenUnlockedIds(HashSet<string> seenIds)
    {
        var key = GetSeenUnlockedKey();
        var ordered = seenIds.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Preferences.Default.Set(key, System.Text.Json.JsonSerializer.Serialize(ordered));
    }

    private static string GetSeenUnlockedKey()
    {
        var username = LocalAccountStore.GetLastActiveUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            return $"{SeenUnlockedPrefix}guest";
        }

        var normalized = new string(
            username
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray());

        return $"{SeenUnlockedPrefix}{normalized}";
    }

    private static string GetCurrentUsernameKey()
    {
        if (LocalAccountStore.TryGetProfile(out var profile) && !string.IsNullOrWhiteSpace(profile.Username))
        {
            return NormalizeUsername(profile.Username);
        }

        var fallback = LocalAccountStore.GetLastActiveUsername();
        return string.IsNullOrWhiteSpace(fallback)
            ? string.Empty
            : NormalizeUsername(fallback);
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}
