using System.Text.Json;

namespace Peak;

public enum TaskScope
{
    Daily,
    Weekly
}

public enum TaskKind
{
    PlaySessions,
    ReachPeakScore
}

public sealed record TodayTaskItem(
    string Id,
    TaskScope Scope,
    TaskKind Kind,
    string Title,
    string Subtitle,
    string GameTitle,
    string IconSource,
    string AccentHex,
    int RewardPoints,
    int TargetValue,
    int ProgressValue,
    bool IsComplete,
    bool IsClaimed);

public static class TaskBoardService
{
    const string ClaimedTasksPrefix = "apex_claimed_tasks_v1_";

    public sealed record TaskBoardSnapshot(
        IReadOnlyList<TodayTaskItem> DailyTasks,
        IReadOnlyList<TodayTaskItem> WeeklyTasks);

    public static IReadOnlyList<TodayTaskItem> GetDailyTasks()
        => BuildTasks(TaskScope.Daily, 3);

    public static IReadOnlyList<TodayTaskItem> GetWeeklyTasks()
        => BuildTasks(TaskScope.Weekly, 2);

    public static TaskBoardSnapshot GetTaskBoardSnapshot()
    {
        var unlocked = GameUnlockService.GetUnlockedGames()
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToList();

        if (unlocked.Count == 0)
        {
            return new TaskBoardSnapshot([], []);
        }

        var sessions = BrainScoreService.GetGameSessions();
        var claimedIds = LoadClaimedTaskIds();

        return new TaskBoardSnapshot(
            BuildTasks(TaskScope.Daily, 3, unlocked, sessions, claimedIds),
            BuildTasks(TaskScope.Weekly, 2, unlocked, sessions, claimedIds));
    }

    public static bool TryClaimTask(string taskId, out int reward, out string message)
    {
        reward = 0;
        var snapshot = GetTaskBoardSnapshot();
        var task = snapshot.DailyTasks.Concat(snapshot.WeeklyTasks).FirstOrDefault(item => item.Id == taskId);
        if (task is null)
        {
            message = "Task not found.";
            return false;
        }

        if (task.IsClaimed)
        {
            message = "This task reward was already claimed.";
            return false;
        }

        if (!task.IsComplete)
        {
            message = "Finish the task first before claiming the reward.";
            return false;
        }

        var claimed = LoadClaimedTaskIds();
        claimed.Add(taskId);
        SaveClaimedTaskIds(claimed);

        reward = task.RewardPoints;
        GamePointsService.AddPoints(reward);
        message = $"You claimed {reward:N0} points.";
        return true;
    }

    static IReadOnlyList<TodayTaskItem> BuildTasks(TaskScope scope, int count)
    {
        var unlocked = GameUnlockService.GetUnlockedGames()
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceId))
            .ToList();

        if (unlocked.Count == 0)
        {
            return [];
        }

        var sessions = BrainScoreService.GetGameSessions();
        var claimedIds = LoadClaimedTaskIds();
        var random = new Random(GetSeed(scope));
        var picks = unlocked
            .OrderBy(_ => random.Next())
            .Take(Math.Min(count, unlocked.Count))
            .ToList();

        var tasks = new List<TodayTaskItem>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            var game = picks[i];
            var type = PickTaskKind(scope, i, random);
            tasks.Add(BuildTask(scope, type, game, sessions, claimedIds));
        }

        return tasks;
    }

    static IReadOnlyList<TodayTaskItem> BuildTasks(
        TaskScope scope,
        int count,
        IReadOnlyList<GameStoreEntry> unlocked,
        IReadOnlyList<GameSessionRecord> sessions,
        HashSet<string> claimedIds)
    {
        if (unlocked.Count == 0)
        {
            return [];
        }

        var random = new Random(GetSeed(scope));
        var picks = unlocked
            .OrderBy(_ => random.Next())
            .Take(Math.Min(count, unlocked.Count))
            .ToList();

        var tasks = new List<TodayTaskItem>(picks.Count);
        for (int i = 0; i < picks.Count; i++)
        {
            var game = picks[i];
            var type = PickTaskKind(scope, i, random);
            tasks.Add(BuildTask(scope, type, game, sessions, claimedIds));
        }

        return tasks;
    }

    static TodayTaskItem BuildTask(
        TaskScope scope,
        TaskKind kind,
        GameStoreEntry game,
        IReadOnlyList<GameSessionRecord> sessions,
        HashSet<string> claimedIds)
    {
        var rangeStartLocal = scope == TaskScope.Daily
            ? DateTime.Today
            : StartOfWeek(DateTime.Today);

        var relevantSessions = sessions
            .Where(item => string.Equals(item.SourceId, game.SourceId, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.PlayedUtc.ToLocalTime() >= rangeStartLocal)
            .ToList();

        int targetValue;
        int progressValue;
        string title;
        string subtitle;
        int reward;

        if (kind == TaskKind.PlaySessions)
        {
            targetValue = scope == TaskScope.Daily ? 3 : 8;
            progressValue = relevantSessions.Count;
            reward = scope == TaskScope.Daily ? 90 : 320;
            title = $"{game.Title} x{targetValue}";
            subtitle = scope == TaskScope.Daily
                ? $"Play {game.Title} {targetValue} times today."
                : $"Play {game.Title} {targetValue} times this week.";
        }
        else
        {
            targetValue = scope == TaskScope.Daily ? 900 : 1600;
            progressValue = relevantSessions.Count == 0 ? 0 : relevantSessions.Max(item => item.PeakGameScore);
            reward = scope == TaskScope.Daily ? 110 : 420;
            title = $"{game.Title} Peak Push";
            subtitle = scope == TaskScope.Daily
                ? $"Reach a {targetValue} peak score in {game.Title} today."
                : $"Reach a {targetValue} peak score in {game.Title} this week.";
        }

        var id = $"{scope}:{rangeStartLocal:yyyyMMdd}:{game.SourceId}:{kind}:{targetValue}";
        var complete = progressValue >= targetValue;

        return new TodayTaskItem(
            Id: id,
            Scope: scope,
            Kind: kind,
            Title: title,
            Subtitle: subtitle,
            GameTitle: game.Title,
            IconSource: game.IconSource,
            AccentHex: game.AccentHex,
            RewardPoints: reward,
            TargetValue: targetValue,
            ProgressValue: Math.Min(progressValue, targetValue),
            IsComplete: complete,
            IsClaimed: claimedIds.Contains(id));
    }

    static TaskKind PickTaskKind(TaskScope scope, int index, Random random)
    {
        if (scope == TaskScope.Weekly)
        {
            return index == 0 ? TaskKind.PlaySessions : TaskKind.ReachPeakScore;
        }

        return random.NextDouble() > 0.45 ? TaskKind.PlaySessions : TaskKind.ReachPeakScore;
    }

    static int GetSeed(TaskScope scope)
    {
        var user = LocalAccountStore.GetLastActiveUsername() ?? "guest";
        var start = scope == TaskScope.Daily ? DateTime.Today : StartOfWeek(DateTime.Today);
        return HashCode.Combine(scope, start.Date, user.ToLowerInvariant());
    }

    static DateTime StartOfWeek(DateTime value)
    {
        int offset = ((int)value.DayOfWeek + 6) % 7;
        return value.Date.AddDays(-offset);
    }

    static HashSet<string> LoadClaimedTaskIds()
    {
        var raw = Preferences.Default.Get(GetClaimedKey(), string.Empty);
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

    static void SaveClaimedTaskIds(HashSet<string> ids)
    {
        Preferences.Default.Set(GetClaimedKey(), JsonSerializer.Serialize(ids.OrderBy(x => x)));
    }

    static string GetClaimedKey() => $"{ClaimedTasksPrefix}{GetUserKey()}";

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
