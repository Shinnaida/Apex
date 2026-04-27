using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Peak;

public static class OnlineAchievementsStore
{
    private sealed record PlayerRow(
        string id,
        string username,
        string display_name);

    private sealed record AchievementRow(
        string player_id,
        int peak_score,
        int memory,
        int problem_solving,
        int language,
        int mental_agility,
        int focus,
        int recorded_sessions,
        int active_days,
        List<string>? unlocked_ids,
        List<string>? seen_unlocked_ids,
        DateTime? updated_at);

    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<AchievementProgressSnapshot?> GetCurrentUserSnapshotAsync()
    {
        try
        {
            var player = await EnsureCurrentPlayerAsync();
            if (player is null)
            {
                return null;
            }

            var request = CreateRequest(
                HttpMethod.Get,
                $"/rest/v1/player_achievements?player_id=eq.{Uri.EscapeDataString(player.id)}&select=player_id,peak_score,memory,problem_solving,language,mental_agility,focus,recorded_sessions,active_days,unlocked_ids,seen_unlocked_ids,updated_at&limit=1");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var rows = await JsonSerializer.DeserializeAsync<List<AchievementRow>>(stream, JsonOptions)
                       ?? new List<AchievementRow>();
            var row = rows.FirstOrDefault();
            if (row is null)
            {
                return null;
            }

            var scores = new BrainSkillScores(
                PeakScore: row.peak_score,
                Memory: row.memory,
                ProblemSolving: row.problem_solving,
                Language: row.language,
                MentalAgility: row.mental_agility,
                Focus: row.focus);

            return new AchievementProgressSnapshot(
                Scores: scores,
                PeakScore: row.peak_score,
                RecordedSessions: row.recorded_sessions,
                ActiveDays: row.active_days,
                CurrentStreakDays: 0,
                DistinctGamesPlayed: 0,
                BestGamePeakScore: 0,
                SeenUnlockedIds: (row.seen_unlocked_ids ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList(),
                UnlockedIds: (row.unlocked_ids ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList());
        }
        catch
        {
            return null;
        }
    }

    public static async Task<OnlineSyncResult> SyncCurrentUserAsync(AchievementProgressSnapshot snapshot)
    {
        try
        {
            var player = await EnsureCurrentPlayerAsync();
            if (player is null)
            {
                return new OnlineSyncResult(false, "No signed-in player found for achievement sync.");
            }

            var payload = new[]
            {
                new
                {
                    player_id = player.id,
                    peak_score = snapshot.PeakScore,
                    memory = snapshot.Scores.Memory,
                    problem_solving = snapshot.Scores.ProblemSolving,
                    language = snapshot.Scores.Language,
                    mental_agility = snapshot.Scores.MentalAgility,
                    focus = snapshot.Scores.Focus,
                    recorded_sessions = snapshot.RecordedSessions,
                    active_days = snapshot.ActiveDays,
                    unlocked_ids = snapshot.UnlockedIds
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray(),
                    seen_unlocked_ids = snapshot.SeenUnlockedIds
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray(),
                    updated_at = DateTime.UtcNow
                }
            };

            var request = CreateRequest(HttpMethod.Post, "/rest/v1/player_achievements?on_conflict=player_id");
            request.Headers.Add("Prefer", "resolution=merge-duplicates");
            request.Content = SerializeContent(payload);

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new OnlineSyncResult(
                    false,
                    await BuildFailureMessageAsync(response, "Could not save player achievements to Supabase."));
            }

            return new OnlineSyncResult(true, "Player achievements synced online.");
        }
        catch (Exception ex)
        {
            return new OnlineSyncResult(false, $"Could not reach Supabase for achievements. {ex.Message}");
        }
    }

    private static async Task<PlayerRow?> EnsureCurrentPlayerAsync()
    {
        if (!LocalAccountStore.TryGetProfile(out var profile) || string.IsNullOrWhiteSpace(profile.Username))
        {
            return null;
        }

        var usernameKey = LocalAccountStore.GetLastActiveUsername();
        if (string.IsNullOrWhiteSpace(usernameKey))
        {
            usernameKey = profile.Username.Trim().ToLowerInvariant();
        }

        var payload = new[]
        {
            new
            {
                username = usernameKey,
                display_name = profile.Username.Trim()
            }
        };

        var request = CreateRequest(HttpMethod.Post, "/rest/v1/players?on_conflict=username&select=id,username,display_name");
        request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        request.Content = SerializeContent(payload);

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var players = await JsonSerializer.DeserializeAsync<List<PlayerRow>>(stream, JsonOptions)
                      ?? new List<PlayerRow>();

        return players.FirstOrDefault();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(SupabaseConfig.ProjectUrl)
        };

        client.DefaultRequestHeaders.Add("apikey", SupabaseConfig.AnonKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseConfig.AnonKey);
        return client;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        return new HttpRequestMessage(method, path);
    }

    private static StringContent SerializeContent<T>(T payload)
    {
        return new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
    }

    private static async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden
                || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return "Supabase rejected achievement access. Check your RLS policies for player_achievements.";
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "player_achievements is missing in Supabase. Add the table first.";
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                return $"{fallback} Details: {body}";
            }
        }
        catch
        {
            // Ignore read failures and use fallback.
        }

        return fallback;
    }
}
