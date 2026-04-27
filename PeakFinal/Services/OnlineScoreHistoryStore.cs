using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Peak;

internal sealed record OnlineHistoryLoadResult(
    IReadOnlyList<BrainScoreHistoryItem> History,
    OnlineSyncResult Result);

internal static class OnlineScoreHistoryStore
{
    private sealed record PlayerRow(
        string id,
        string username,
        string display_name,
        string? avatar_url);

    private sealed record ScoreHistoryRow(
        string player_id,
        string source_id,
        string skill,
        double normalized_score,
        int raw_score,
        int peak_game_score,
        DateTime? timestamp_utc);

    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<OnlineHistoryLoadResult> GetCurrentUserHistoryAsync()
    {
        try
        {
            var playerResult = await EnsureCurrentPlayerWithResultAsync();
            if (!playerResult.Result.IsSuccess || playerResult.Player is null)
            {
                return new OnlineHistoryLoadResult(Array.Empty<BrainScoreHistoryItem>(), playerResult.Result);
            }

            var request = CreateRequest(
                HttpMethod.Get,
                $"/rest/v1/player_score_history?player_id=eq.{Uri.EscapeDataString(playerResult.Player.id)}&select=player_id,source_id,skill,normalized_score,raw_score,peak_game_score,timestamp_utc&order=timestamp_utc.asc&limit=1000");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new OnlineHistoryLoadResult(
                    Array.Empty<BrainScoreHistoryItem>(),
                    new OnlineSyncResult(
                        false,
                        await BuildFailureMessageAsync(response, "Could not load player score history from Supabase.")));
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var rows = await JsonSerializer.DeserializeAsync<List<ScoreHistoryRow>>(stream, JsonOptions)
                       ?? new List<ScoreHistoryRow>();

            var history = rows
                .Where(row => !string.IsNullOrWhiteSpace(row.source_id)
                              && !string.IsNullOrWhiteSpace(row.skill))
                .Select(row => new BrainScoreHistoryItem
                {
                    SourceId = row.source_id.Trim(),
                    Skill = row.skill.Trim(),
                    NormalizedScore = Math.Clamp(row.normalized_score, 0, 1),
                    RawScore = Math.Max(0, row.raw_score),
                    PeakGameScore = Math.Max(0, row.peak_game_score),
                    TimestampUtc = NormalizeTimestamp(row.timestamp_utc)
                })
                .OrderBy(item => item.TimestampUtc)
                .ToList();

            return new OnlineHistoryLoadResult(
                history,
                new OnlineSyncResult(true, "Player score history loaded from Supabase."));
        }
        catch (Exception ex)
        {
            return new OnlineHistoryLoadResult(
                Array.Empty<BrainScoreHistoryItem>(),
                new OnlineSyncResult(false, $"Could not reach Supabase for score history. {ex.Message}"));
        }
    }

    public static async Task<OnlineSyncResult> AppendCurrentUserHistoryItemAsync(BrainScoreHistoryItem item)
    {
        try
        {
            var playerResult = await EnsureCurrentPlayerWithResultAsync();
            if (!playerResult.Result.IsSuccess || playerResult.Player is null)
            {
                return playerResult.Result;
            }

            var payload = new[]
            {
                new
                {
                    player_id = playerResult.Player.id,
                    source_id = item.SourceId.Trim(),
                    skill = item.Skill.Trim(),
                    normalized_score = Math.Clamp(item.NormalizedScore, 0, 1),
                    raw_score = Math.Max(0, item.RawScore),
                    peak_game_score = Math.Max(0, item.PeakGameScore),
                    timestamp_utc = NormalizeTimestamp(item.TimestampUtc)
                }
            };

            var request = CreateRequest(HttpMethod.Post, "/rest/v1/player_score_history");
            request.Content = SerializeContent(payload);

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new OnlineSyncResult(
                    false,
                    await BuildFailureMessageAsync(response, "Could not save score history to Supabase."));
            }

            return new OnlineSyncResult(true, "Player score history synced online.");
        }
        catch (Exception ex)
        {
            return new OnlineSyncResult(false, $"Could not reach Supabase for score history. {ex.Message}");
        }
    }

    private static async Task<(PlayerRow? Player, OnlineSyncResult Result)> EnsureCurrentPlayerWithResultAsync()
    {
        if (!LocalAccountStore.TryGetProfile(out var profile) || string.IsNullOrWhiteSpace(profile.Username))
        {
            return (null, new OnlineSyncResult(false, "No signed-in account found for score history sync."));
        }

        var usernameKey = LocalAccountStore.GetLastActiveUsername();
        if (string.IsNullOrWhiteSpace(usernameKey))
        {
            usernameKey = profile.Username.Trim().ToLowerInvariant();
        }

        var displayName = profile.Username.Trim();
        var avatarValue = await AvatarImageSyncHelper.BuildAvatarSyncValueAsync(Http, usernameKey, displayName);

        var payload = new[]
        {
            new
            {
                username = usernameKey,
                display_name = displayName,
                avatar_url = avatarValue
            }
        };

        var request = CreateRequest(HttpMethod.Post, "/rest/v1/players?on_conflict=username&select=id,username,display_name,avatar_url");
        request.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
        request.Content = SerializeContent(payload);

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, new OnlineSyncResult(
                false,
                await BuildFailureMessageAsync(response, "Could not save the player profile to Supabase.")));
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var players = await JsonSerializer.DeserializeAsync<List<PlayerRow>>(stream, JsonOptions)
                      ?? new List<PlayerRow>();

        var player = players.FirstOrDefault();
        if (player is null)
        {
            return (null, new OnlineSyncResult(false, "Supabase returned no player row after profile sync."));
        }

        return (player, new OnlineSyncResult(true, "Player profile ready for score history sync."));
    }

    private static DateTime NormalizeTimestamp(DateTime? timestampUtc)
    {
        if (!timestampUtc.HasValue)
        {
            return DateTime.UtcNow;
        }

        return timestampUtc.Value.Kind switch
        {
            DateTimeKind.Utc => timestampUtc.Value,
            DateTimeKind.Local => timestampUtc.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestampUtc.Value, DateTimeKind.Utc)
        };
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
                return "Supabase rejected score history access. Check your RLS policies for player_score_history.";
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "player_score_history is missing in Supabase. Add the table first.";
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
