namespace Peak;

public enum BrainSkill
{
    Memory,
    ProblemSolving,
    Language,
    Focus,
    MentalAgility,
    Emotion
}

public sealed record BrainSkillScores(
    int PeakScore,
    int Memory,
    int ProblemSolving,
    int Language,
    int MentalAgility,
    int Focus,
    int Emotion = 0);

public sealed record PeakRankInfo(
    string Name,
    int MinScore,
    string? NextName,
    int? NextMinScore);

public sealed record PeakRankTier(
    string Name,
    int MinScore,
    int MaxScore,
    string IconSource);

public sealed record BrainScoreTrendPoint(
    DateTime DateUtc,
    BrainSkillScores Scores);

public sealed record PlayedGameScore(
    string SourceId,
    BrainSkill Skill,
    int PeakGameScore,
    DateTime LastPlayedUtc,
    int Sessions);

public sealed record GameSessionRecord(
    string SourceId,
    BrainSkill Skill,
    int RawScore,
    int PeakGameScore,
    DateTime PlayedUtc);

public sealed record GameTopSession(
    int Rank,
    int Score,
    int PeakGameScore,
    DateTime PlayedUtc);

public sealed record GamePerformanceSnapshot(
    string SourceId,
    BrainSkill Skill,
    int BestScore,
    int BestPeakGameScore,
    int AveragePeakGameScore,
    int SessionCount,
    DateTime FirstPlayedUtc,
    DateTime LastPlayedUtc,
    IReadOnlyList<double> TrendValues,
    IReadOnlyList<GameTopSession> TopSessions);

internal sealed class BrainScoreHistoryItem
{
    public string SourceId { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public double NormalizedScore { get; set; }
    public int RawScore { get; set; }
    public int PeakGameScore { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public static class BrainScoreService
{
    private const string LegacyHistoryKey = "brain_score_history_v1";
    private const string HistoryKeyPrefix = "brain_score_history_v2_";
    private const string LegacyMigrationKey = "brain_score_history_v2_migrated";
    private const int MaxHistoryItems = 500;
    private const int MaxEntriesPerSkill = 24;
    private static readonly object HistorySync = new();
    private static List<BrainScoreHistoryItem> _historyCache = new();
    private static string _cachedUsernameKey = string.Empty;
    private static bool _historyResolvedForCurrentUser;

    private static readonly (string Name, int MinScore, string IconSource)[] PeakRanks =
    {
        ("Basecamp", 0, "rank_basecamp.svg"),
        ("Foothill", 1800, "rank_foothill.svg"),
        ("Ridge", 3200, "rank_ridge.svg"),
        ("Ascent", 5200, "rank_ascent.svg"),
        ("Summit", 7800, "rank_summit.svg"),
        ("Apex", 11000, "rank_apex.svg"),
        ("Peak", 15000, "rank_peak.svg")
    };

    public static bool HasResolvedCurrentUserHistory
    {
        get
        {
            AlignCacheWithCurrentUser();
            lock (HistorySync)
            {
                return _historyResolvedForCurrentUser;
            }
        }
    }

    public static async Task<OnlineSyncResult> RefreshCurrentUserFromDatabaseAsync()
    {
        AlignCacheWithCurrentUser();

        if (!LocalAccountStore.IsSignedIn)
        {
            InitializeEmptyCurrentUserHistory();
            return new OnlineSyncResult(true, "No signed-in account. Score history reset for this session.");
        }

        var result = await OnlineScoreHistoryStore.GetCurrentUserHistoryAsync();
        if (!result.Result.IsSuccess)
        {
            return result.Result;
        }

        lock (HistorySync)
        {
            _historyCache = result.History
                .OrderBy(item => item.TimestampUtc)
                .TakeLast(MaxHistoryItems)
                .Select(CloneHistoryItem)
                .ToList();
            _historyResolvedForCurrentUser = true;
        }

        return result.Result;
    }

    public static void InitializeEmptyCurrentUserHistory()
    {
        AlignCacheWithCurrentUser();

        lock (HistorySync)
        {
            _historyCache = new List<BrainScoreHistoryItem>();
            _historyResolvedForCurrentUser = true;
        }
    }

    public static void PurgeLegacyLocalScoreStorage()
    {
        Preferences.Default.Remove(LegacyHistoryKey);
        Preferences.Default.Remove(LegacyMigrationKey);

        foreach (var usernameKey in LocalAccountStore.GetStoredUsernameKeys())
        {
            Preferences.Default.Remove($"{HistoryKeyPrefix}{NormalizeHistoryUserKey(usernameKey)}");
        }

        Preferences.Default.Remove("WordALikeBestScore");
        Preferences.Default.Remove("WordFreshBestScore");
        Preferences.Default.Remove("PerilousPathBestScore");
        Preferences.Default.Remove("PartialMatchBestScore");
        Preferences.Default.Remove("SpinCycleBestScore");
    }

    public static int RecordGameScore(string sourceId, BrainSkill skill, int rawScore, int expectedTopScore)
    {
        if (expectedTopScore <= 0)
        {
            return 0;
        }

        var normalized = Math.Clamp(rawScore / (double)expectedTopScore, 0, 1);
        AppendHistoryItem(new BrainScoreHistoryItem
        {
            SourceId = sourceId.Trim(),
            Skill = skill.ToString(),
            NormalizedScore = normalized,
            RawScore = Math.Max(0, rawScore),
            PeakGameScore = (int)Math.Round(normalized * 1000),
            TimestampUtc = DateTime.UtcNow
        });

        var awardedPoints = GamePointsService.AwardGameplayPoints(sourceId.Trim(), Math.Max(0, rawScore), expectedTopScore);

        _ = PlayerLeaderboardService.SyncGameScoreAsync(
            sourceId.Trim(),
            Math.Max(0, rawScore),
            (int)Math.Round(normalized * 1000));
        _ = PlayerLeaderboardService.SyncCurrentUserAsync();
        _ = AchievementsService.SyncCurrentUserProgressAsync();
        return awardedPoints;
    }

    public static void RecordSkillNormalized(string sourceId, BrainSkill skill, double normalizedScore)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        AppendHistoryItem(new BrainScoreHistoryItem
        {
            SourceId = sourceId.Trim(),
            Skill = skill.ToString(),
            NormalizedScore = Math.Clamp(normalizedScore, 0, 1),
            TimestampUtc = DateTime.UtcNow
        });
    }

    public static void RecordIqSnapshot(double memory, double problemSolving, double language, double focus)
    {
        RecordSkillNormalized("iq_test", BrainSkill.Memory, memory);
        RecordSkillNormalized("iq_test", BrainSkill.ProblemSolving, problemSolving);
        RecordSkillNormalized("iq_test", BrainSkill.Language, language);
        RecordSkillNormalized("iq_test", BrainSkill.Focus, focus);
        _ = PlayerLeaderboardService.SyncCurrentUserAsync();
        _ = AchievementsService.SyncCurrentUserProgressAsync();
    }

    public static BrainSkillScores GetCurrentScores()
    {
        var history = LoadHistory();

        var memoryScore = ToSkillScore(CalculateSkillNormalized(history, BrainSkill.Memory));
        var problemScore = ToSkillScore(CalculateSkillNormalized(history, BrainSkill.ProblemSolving));
        var languageScore = ToSkillScore(CalculateSkillNormalized(history, BrainSkill.Language));
        var focusScore = ToSkillScore(CalculateSkillNormalized(history, BrainSkill.Focus));
        var emotionScore = ToSkillScore(CalculateSkillNormalized(history, BrainSkill.Emotion));

        var agilityScore = (int)Math.Round((memoryScore + problemScore + languageScore + focusScore) / 4.0);
        var peakScore = memoryScore + problemScore + languageScore + focusScore + agilityScore;

        return new BrainSkillScores(
            PeakScore: peakScore,
            Memory: memoryScore,
            ProblemSolving: problemScore,
            Language: languageScore,
            MentalAgility: agilityScore,
            Focus: focusScore,
            Emotion: emotionScore);
    }

    public static string GetPeakRankName(int peakScore)
    {
        return GetPeakRankInfo(peakScore).Name;
    }

    public static int GetRecordedSessionCount()
    {
        return LoadHistory().Count;
    }

    public static int GetActiveDayCount()
    {
        return LoadHistory()
            .Select(x => x.TimestampUtc.Date)
            .Distinct()
            .Count();
    }

    public static int GetCurrentStreakDays()
    {
        var activeDays = LoadHistory()
            .Select(x => ToLocalDate(x.TimestampUtc))
            .Distinct()
            .OrderByDescending(x => x)
            .ToList();

        if (activeDays.Count == 0)
        {
            return 0;
        }

        var today = DateTime.Now.Date;
        var latestActive = activeDays[0];

        // If the last recorded day is older than yesterday, the streak is broken.
        if (latestActive < today.AddDays(-1))
        {
            return 0;
        }

        var streak = 1;
        var expectedPreviousDay = latestActive.AddDays(-1);

        for (var i = 1; i < activeDays.Count; i++)
        {
            if (activeDays[i] != expectedPreviousDay)
            {
                break;
            }

            streak++;
            expectedPreviousDay = expectedPreviousDay.AddDays(-1);
        }

        return streak;
    }

    public static PeakRankInfo GetPeakRankInfo(int peakScore)
    {
        var clampedScore = Math.Clamp(peakScore, 0, 1000);
        var index = 0;

        for (int i = PeakRanks.Length - 1; i >= 0; i--)
        {
            if (clampedScore >= PeakRanks[i].MinScore)
            {
                index = i;
                break;
            }
        }

        var current = PeakRanks[index];
        if (index >= PeakRanks.Length - 1)
        {
            return new PeakRankInfo(current.Name, current.MinScore, null, null);
        }

        var next = PeakRanks[index + 1];
        return new PeakRankInfo(current.Name, current.MinScore, next.Name, next.MinScore);
    }

    public static IReadOnlyList<PeakRankTier> GetPeakRankTiers()
    {
        var tiers = new List<PeakRankTier>(PeakRanks.Length);

        for (int i = 0; i < PeakRanks.Length; i++)
        {
            var current = PeakRanks[i];
            var maxScore = i < PeakRanks.Length - 1
                ? PeakRanks[i + 1].MinScore - 1
                : 1000;

            tiers.Add(new PeakRankTier(current.Name, current.MinScore, maxScore, current.IconSource));
        }

        return tiers;
    }

    public static IReadOnlyList<BrainScoreTrendPoint> GetOverTimeTrend(int? lookbackDays, int maxPoints = 36)
    {
        var history = LoadHistory()
            .OrderBy(x => x.TimestampUtc)
            .ToList();

        if (history.Count == 0)
        {
            return new List<BrainScoreTrendPoint>
            {
                new(DateTime.UtcNow.Date, GetCurrentScores())
            };
        }

        var endDate = DateTime.UtcNow.Date;
        var earliestDate = history[0].TimestampUtc.Date;

        var startDate = lookbackDays.HasValue
            ? endDate.AddDays(-Math.Max(1, lookbackDays.Value) + 1)
            : earliestDate;

        if (startDate < earliestDate)
        {
            startDate = earliestDate;
        }

        var dailySkillAverages = BuildDailySkillAverages(history, startDate, endDate);
        var lastKnownNormalized = BuildInitialSkillState(history, startDate);
        var points = new List<BrainScoreTrendPoint>();

        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            foreach (var skill in GetTrendSkills())
            {
                if (dailySkillAverages.TryGetValue((day, skill), out var average))
                {
                    lastKnownNormalized[skill] = average;
                }
            }

            var memory = ToSkillScore(lastKnownNormalized[BrainSkill.Memory]);
            var problemSolving = ToSkillScore(lastKnownNormalized[BrainSkill.ProblemSolving]);
            var language = ToSkillScore(lastKnownNormalized[BrainSkill.Language]);
            var focus = ToSkillScore(lastKnownNormalized[BrainSkill.Focus]);
            var emotion = ToSkillScore(lastKnownNormalized[BrainSkill.Emotion]);
            var agility = (int)Math.Round((memory + problemSolving + language + focus) / 4.0);
            var peak = memory + problemSolving + language + focus + agility;

            points.Add(new BrainScoreTrendPoint(
                DateUtc: day,
                Scores: new BrainSkillScores(
                    PeakScore: peak,
                    Memory: memory,
                    ProblemSolving: problemSolving,
                    Language: language,
                    MentalAgility: agility,
                    Focus: focus,
                    Emotion: emotion)));
        }

        return DownsampleTrend(points, maxPoints);
    }

    public static IReadOnlyList<PlayedGameScore> GetPlayedGameScores()
    {
        var history = LoadHistory();
        if (history.Count == 0)
        {
            return Array.Empty<PlayedGameScore>();
        }

        return history
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId)
                           && Enum.TryParse<BrainSkill>(item.Skill, out _)
                           && !string.Equals(item.SourceId, "iq_test", StringComparison.Ordinal))
            .GroupBy(item => item.SourceId.Trim(), StringComparer.Ordinal)
            .Select(group =>
            {
                var latest = group
                    .OrderByDescending(item => item.TimestampUtc)
                    .First();

                var skill = Enum.TryParse<BrainSkill>(latest.Skill, out var parsedSkill)
                    ? parsedSkill
                    : BrainSkill.Memory;

                var peakGameScore = group.Max(GetPeakGameScore);

                return new PlayedGameScore(
                    SourceId: group.Key,
                    Skill: skill,
                    PeakGameScore: peakGameScore,
                    LastPlayedUtc: latest.TimestampUtc,
                    Sessions: group.Count());
            })
            .OrderByDescending(item => item.LastPlayedUtc)
            .ToList();
    }

    public static IReadOnlyList<GameSessionRecord> GetGameSessions()
    {
        return LoadHistory()
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId)
                           && Enum.TryParse<BrainSkill>(item.Skill, out _)
                           && !string.Equals(item.SourceId, "iq_test", StringComparison.Ordinal))
            .Select(item =>
            {
                var skill = Enum.TryParse<BrainSkill>(item.Skill, out var parsedSkill)
                    ? parsedSkill
                    : BrainSkill.Memory;

                return new GameSessionRecord(
                    SourceId: item.SourceId.Trim(),
                    Skill: skill,
                    RawScore: GetDisplayedSessionScore(item),
                    PeakGameScore: GetPeakGameScore(item),
                    PlayedUtc: item.TimestampUtc);
            })
            .OrderByDescending(item => item.PlayedUtc)
            .ToList();
    }

    public static GamePerformanceSnapshot? GetGamePerformance(string sourceId, int trendDays = 7, int topSessions = 5)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        var sessions = LoadHistory()
            .Where(item => string.Equals(item.SourceId?.Trim(), sourceId.Trim(), StringComparison.Ordinal)
                           && Enum.TryParse<BrainSkill>(item.Skill, out _))
            .OrderBy(item => item.TimestampUtc)
            .ToList();

        if (sessions.Count == 0)
        {
            return null;
        }

        var latest = sessions[^1];
        var skill = Enum.TryParse<BrainSkill>(latest.Skill, out var parsedSkill)
            ? parsedSkill
            : BrainSkill.Memory;

        var today = DateTime.Now.Date;
        var startDay = today.AddDays(-Math.Max(1, trendDays) + 1);
        var perDayPeak = sessions
            .GroupBy(item => ToLocalDate(item.TimestampUtc))
            .ToDictionary(group => group.Key, group => group.Max(GetPeakGameScore));

        var trend = new List<double>(trendDays);
        for (var day = startDay; day <= today; day = day.AddDays(1))
        {
            trend.Add(perDayPeak.TryGetValue(day, out var peak) ? peak : 0);
        }

        var rankedTopSessions = sessions
            .OrderByDescending(GetDisplayedSessionScore)
            .ThenByDescending(GetPeakGameScore)
            .ThenByDescending(item => item.TimestampUtc)
            .Take(Math.Max(1, topSessions))
            .Select((item, index) => new GameTopSession(
                Rank: index + 1,
                Score: GetDisplayedSessionScore(item),
                PeakGameScore: GetPeakGameScore(item),
                PlayedUtc: item.TimestampUtc))
            .ToList();

        return new GamePerformanceSnapshot(
            SourceId: sourceId.Trim(),
            Skill: skill,
            BestScore: sessions.Max(GetDisplayedSessionScore),
            BestPeakGameScore: sessions.Max(GetPeakGameScore),
            AveragePeakGameScore: (int)Math.Round(sessions.Average(item => (double)GetPeakGameScore(item))),
            SessionCount: sessions.Count,
            FirstPlayedUtc: sessions[0].TimestampUtc,
            LastPlayedUtc: latest.TimestampUtc,
            TrendValues: trend,
            TopSessions: rankedTopSessions);
    }

    private static int ToSkillScore(double normalized)
    {
        // Keep each skill in the familiar 100-200 range.
        return (int)Math.Round(100 + (normalized * 100));
    }

    private static void AppendHistoryItem(BrainScoreHistoryItem item)
    {
        AlignCacheWithCurrentUser();

        List<BrainScoreHistoryItem> history;
        lock (HistorySync)
        {
            history = _historyCache
                .Select(CloneHistoryItem)
                .ToList();
            history.Add(CloneHistoryItem(item));

            if (history.Count > MaxHistoryItems)
            {
                history = history
                .OrderByDescending(x => x.TimestampUtc)
                .Take(MaxHistoryItems)
                .OrderBy(x => x.TimestampUtc)
                .ToList();
            }

            _historyCache = history;
            _historyResolvedForCurrentUser = true;
        }

        if (LocalAccountStore.IsSignedIn)
        {
            _ = OnlineScoreHistoryStore.AppendCurrentUserHistoryItemAsync(item);
        }
    }

    private static int GetPeakGameScore(BrainScoreHistoryItem item)
    {
        if (item.PeakGameScore > 0)
        {
            return Math.Clamp(item.PeakGameScore, 0, 1000);
        }

        return (int)Math.Round(Math.Clamp(item.NormalizedScore, 0, 1) * 1000);
    }

    private static int GetDisplayedSessionScore(BrainScoreHistoryItem item)
    {
        return item.RawScore > 0
            ? item.RawScore
            : GetPeakGameScore(item);
    }

    private static IReadOnlyDictionary<(DateTime Day, BrainSkill Skill), double> BuildDailySkillAverages(
        IReadOnlyCollection<BrainScoreHistoryItem> history,
        DateTime startDate,
        DateTime endDate)
    {
        var map = new Dictionary<(DateTime, BrainSkill), List<double>>();

        foreach (var item in history)
        {
            var day = item.TimestampUtc.Date;
            if (day < startDate || day > endDate)
            {
                continue;
            }

            if (!Enum.TryParse<BrainSkill>(item.Skill, out var skill))
            {
                continue;
            }

            var key = (day, skill);
            if (!map.TryGetValue(key, out var values))
            {
                values = new List<double>();
                map[key] = values;
            }

            values.Add(Math.Clamp(item.NormalizedScore, 0, 1));
        }

        return map.ToDictionary(x => x.Key, x => x.Value.Average());
    }

    private static Dictionary<BrainSkill, double> BuildInitialSkillState(
        IReadOnlyCollection<BrainScoreHistoryItem> history,
        DateTime startDate)
    {
        var state = new Dictionary<BrainSkill, double>();

        foreach (var skill in GetTrendSkills())
        {
            var previous = history
                .Where(x => x.TimestampUtc.Date < startDate && string.Equals(x.Skill, skill.ToString(), StringComparison.Ordinal))
                .OrderByDescending(x => x.TimestampUtc)
                .Select(x => Math.Clamp(x.NormalizedScore, 0, 1))
                .FirstOrDefault();

            state[skill] = previous;
        }

        return state;
    }

    private static IReadOnlyList<BrainScoreTrendPoint> DownsampleTrend(IReadOnlyList<BrainScoreTrendPoint> points, int maxPoints)
    {
        if (points.Count <= maxPoints || maxPoints < 3)
        {
            return points.ToList();
        }

        var sampled = new List<BrainScoreTrendPoint>(maxPoints) { points[0] };
        var interiorSlots = maxPoints - 2;

        for (int i = 1; i <= interiorSlots; i++)
        {
            var t = i / (double)(interiorSlots + 1);
            var index = (int)Math.Round(t * (points.Count - 1));
            index = Math.Clamp(index, 1, points.Count - 2);

            var candidate = points[index];
            if (sampled[^1].DateUtc != candidate.DateUtc)
            {
                sampled.Add(candidate);
            }
        }

        sampled.Add(points[^1]);
        return sampled;
    }

    private static IReadOnlyList<BrainSkill> GetTrendSkills()
    {
        return new[]
        {
            BrainSkill.Memory,
            BrainSkill.ProblemSolving,
            BrainSkill.Language,
            BrainSkill.Focus
        };
    }

    private static double CalculateSkillNormalized(IReadOnlyCollection<BrainScoreHistoryItem> history, BrainSkill skill)
    {
        var skillName = skill.ToString();
        var entries = history
            .Where(x => string.Equals(x.Skill, skillName, StringComparison.Ordinal))
            .OrderByDescending(x => x.TimestampUtc)
            .Take(MaxEntriesPerSkill)
            .ToList();

        if (entries.Count == 0)
        {
            return 0;
        }

        double weightedSum = 0;
        double totalWeight = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            var weight = Math.Max(1, 12 - i);
            weightedSum += entries[i].NormalizedScore * weight;
            totalWeight += weight;
        }

        if (totalWeight <= 0)
        {
            return 0;
        }

        return Math.Clamp(weightedSum / totalWeight, 0, 1);
    }

    private static List<BrainScoreHistoryItem> LoadHistory()
    {
        AlignCacheWithCurrentUser();

        lock (HistorySync)
        {
            return _historyCache
                .Select(CloneHistoryItem)
                .ToList();
        }
    }

    private static void AlignCacheWithCurrentUser()
    {
        var usernameKey = GetCurrentUsernameKey();

        lock (HistorySync)
        {
            if (string.Equals(_cachedUsernameKey, usernameKey, StringComparison.Ordinal))
            {
                return;
            }

            _cachedUsernameKey = usernameKey;
            _historyCache = new List<BrainScoreHistoryItem>();
            _historyResolvedForCurrentUser = string.IsNullOrWhiteSpace(usernameKey);
        }
    }

    private static BrainScoreHistoryItem CloneHistoryItem(BrainScoreHistoryItem item)
    {
        return new BrainScoreHistoryItem
        {
            SourceId = item.SourceId,
            Skill = item.Skill,
            NormalizedScore = item.NormalizedScore,
            RawScore = item.RawScore,
            PeakGameScore = item.PeakGameScore,
            TimestampUtc = item.TimestampUtc
        };
    }

    private static string GetCurrentUsernameKey()
    {
        var username = LocalAccountStore.GetLastActiveUsername();
        return string.IsNullOrWhiteSpace(username)
            ? string.Empty
            : NormalizeHistoryUserKey(username);
    }

    private static string NormalizeHistoryUserKey(string username)
    {
        return new string(
            username
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray());
    }

    private static DateTime ToLocalDate(DateTime timestampUtc)
    {
        var normalizedUtc = timestampUtc.Kind switch
        {
            DateTimeKind.Utc => timestampUtc,
            DateTimeKind.Local => timestampUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, TimeZoneInfo.Local).Date;
    }
}
