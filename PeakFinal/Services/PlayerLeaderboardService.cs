using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Peak;

public sealed record PlayerLeaderboardEntry(
    int Rank,
    string PlayerId,
    string UsernameKey,
    string Name,
    int PeakScore,
    string PeakRank,
    bool IsCurrentPlayer,
    string AvatarText,
    string? AvatarImageSource,
    Color AccentColor);

public sealed record LeaderboardGameOption(
    string SourceId,
    string Name,
    BrainSkill Skill,
    Color AccentColor);

public sealed record PlayerProfileDetails(
    string Name,
    string AvatarText,
    string? AvatarImageSource,
    int CurrentRank,
    string RankLabel,
    string BestSkillName,
    int GamesPlayed,
    int Score,
    string ScoreLabel,
    string AchievementSummaryText,
    IReadOnlyList<AchievementItem> Achievements);

public sealed record PlayerLeaderboardResult(
    IReadOnlyList<PlayerLeaderboardEntry> Entries,
    bool IsLive,
    string Message);

public sealed record OnlineSyncResult(
    bool IsSuccess,
    string Message);

public enum LeaderboardTimeframe
{
    Weekly,
    AllTime
}

public static class PlayerLeaderboardService
{
    private const string EmojiAvatarPrefix = "emoji:";

    private sealed record PlayerRow(
        string id,
        string username,
        string display_name,
        string? avatar_url);

    private sealed record GameScoreRow(
        string game_id,
        int best_score,
        int peak_game_score,
        DateTime? last_played_at);

    private sealed record LeaderboardRow(
        string id,
        string username,
        string display_name,
        string? avatar_url,
        int peak_brain_score,
        DateTime? updated_at);

    private sealed record PlayerScoreRow(
        int peak_brain_score,
        int memory,
        int problem_solving,
        int language,
        int mental_agility,
        int focus);

    private sealed record AchievementProfileRow(
        string player_id,
        List<string>? unlocked_ids);

    private sealed record GameLeaderboardRow(
        string player_id,
        string game_id,
        int peak_game_score,
        DateTime? last_played_at,
        PlayerRow? players);

    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static OnlineSyncResult LastSyncResult { get; private set; } =
        new(false, "No online sync attempted yet.");

    private static readonly string[] SampleNames =
    {
        "Mika", "Andre", "Sofia", "Noah", "Leah", "Kenji", "Ava", "Lucas",
        "Mina", "Theo", "Elise", "Marco", "Iris", "Owen", "Nina", "Cruz"
    };

    private static readonly string[] SampleAvatars =
    {
        "A", "B", "C", "D", "E", "F", "G", "H",
        "J", "K", "L", "M", "N", "P", "R", "S"
    };

    private static readonly Color[] AccentPalette =
    {
        Color.FromArgb("#19B5EA"),
        Color.FromArgb("#6A5AF2"),
        Color.FromArgb("#28C56D"),
        Color.FromArgb("#F3A61C"),
        Color.FromArgb("#FF5874")
    };

    private static readonly IReadOnlyList<LeaderboardGameOption> SupportedGames = new[]
    {
        new LeaderboardGameOption("word_a_like", "Word-A-Like", BrainSkill.Language, Color.FromArgb("#6A58F1")),
        new LeaderboardGameOption("word_fresh", "Word Fresh", BrainSkill.Language, Color.FromArgb("#5E54EA")),
        new LeaderboardGameOption("word_hunt", "Word Hunt", BrainSkill.Language, Color.FromArgb("#5E54EA")),
        new LeaderboardGameOption("grow", "Grow", BrainSkill.Language, Color.FromArgb("#6B63F5")),
        new LeaderboardGameOption("perilous_path", "Perilous Path", BrainSkill.Memory, Color.FromArgb("#25B56A")),
        new LeaderboardGameOption("partial_match", "Partial Match", BrainSkill.ProblemSolving, Color.FromArgb("#FF4E73")),
        new LeaderboardGameOption("square_numbers", "Square Numbers", BrainSkill.ProblemSolving, Color.FromArgb("#28C85A")),
        new LeaderboardGameOption("moving_math", "Moving Math", BrainSkill.ProblemSolving, Color.FromArgb("#28C85A")),
        new LeaderboardGameOption("must_sort", "Must Sort", BrainSkill.Focus, Color.FromArgb("#FF4E73")),
        new LeaderboardGameOption("tap_trap", "Tap Trap", BrainSkill.Focus, Color.FromArgb("#FF4E73")),
        new LeaderboardGameOption("unique", "Unique", BrainSkill.Focus, Color.FromArgb("#FF4E73")),
        new LeaderboardGameOption("true_color", "True Color", BrainSkill.Focus, Color.FromArgb("#0F7AF3")),
        new LeaderboardGameOption("spin_cycle", "Spin Cycle", BrainSkill.Memory, Color.FromArgb("#F39A19")),
        new LeaderboardGameOption("memory_match", "Memory Match", BrainSkill.Memory, Color.FromArgb("#F0B31A")),
        new LeaderboardGameOption("turtle_traffic", "Turtle Traffic", BrainSkill.Focus, Color.FromArgb("#0F7AF3"))
    };

    public static IReadOnlyList<LeaderboardGameOption> GetSupportedGames() => SupportedGames;

    public static async Task<PlayerLeaderboardResult> GetLeaderboardAsync(
        int limit = 25,
        LeaderboardTimeframe timeframe = LeaderboardTimeframe.AllTime,
        string? gameSourceId = null)
    {
        try
        {
            if (LocalAccountStore.IsSignedIn)
            {
                _ = await SyncCurrentUserProfileOnlyAsync();
            }

            var safeLimit = Math.Max(1, limit);
            var normalizedGameSourceId = NormalizeSourceId(gameSourceId);
            var query = BuildLeaderboardQuery(safeLimit, timeframe, normalizedGameSourceId);
            var request = CreateRequest(HttpMethod.Get, query);

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var fallbackMessage = normalizedGameSourceId is null
                    ? "Live leaderboard is not available yet. Showing local fallback."
                    : $"Live {GetGameDisplayName(normalizedGameSourceId)} rankings are not available yet. Showing local fallback.";
                return BuildFallbackResult(normalizedGameSourceId, await BuildFailureMessageAsync(
                    response,
                    fallbackMessage));
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var currentUsername = GetCurrentUsernameKey();

            if (normalizedGameSourceId is not null)
            {
                var gameRows = await JsonSerializer.DeserializeAsync<List<GameLeaderboardRow>>(stream, JsonOptions)
                               ?? new List<GameLeaderboardRow>();

                if (gameRows.Count == 0)
                {
                    return BuildFallbackResult(normalizedGameSourceId, $"No {GetGameDisplayName(normalizedGameSourceId)} scores yet. Showing local fallback.");
                }

                var gameEntries = gameRows
                    .Where(row => row.players is not null)
                    .Select((row, index) =>
                    {
                        var player = row.players!;
                        var isCurrentPlayer = string.Equals(player.username, currentUsername, StringComparison.OrdinalIgnoreCase);
                        return new PlayerLeaderboardEntry(
                            Rank: index + 1,
                            PlayerId: row.player_id,
                            UsernameKey: player.username,
                            Name: string.IsNullOrWhiteSpace(player.display_name) ? player.username : player.display_name,
                            PeakScore: row.peak_game_score,
                            PeakRank: BrainScoreService.GetPeakRankName(row.peak_game_score),
                            IsCurrentPlayer: isCurrentPlayer,
                            AvatarText: GetAvatarText(player.display_name, player.username, player.avatar_url, isCurrentPlayer),
                            AvatarImageSource: GetAvatarImageSource(player.display_name, player.username, player.avatar_url, isCurrentPlayer),
                            AccentColor: AccentPalette[index % AccentPalette.Length]);
                    })
                    .ToList();

                if (!string.IsNullOrWhiteSpace(currentUsername) && !gameEntries.Any(entry => entry.IsCurrentPlayer))
                {
                    var currentGameRow = await GetCurrentPlayerGameRowAsync(currentUsername, normalizedGameSourceId, timeframe);
                    if (currentGameRow?.players is not null)
                    {
                        var currentRank = await GetApproximateGameRankAsync(currentGameRow.peak_game_score, normalizedGameSourceId, timeframe);
                        var player = currentGameRow.players;
                        gameEntries.Add(new PlayerLeaderboardEntry(
                            Rank: currentRank,
                            PlayerId: currentGameRow.player_id,
                            UsernameKey: player.username,
                            Name: string.IsNullOrWhiteSpace(player.display_name) ? player.username : player.display_name,
                            PeakScore: currentGameRow.peak_game_score,
                            PeakRank: BrainScoreService.GetPeakRankName(currentGameRow.peak_game_score),
                            IsCurrentPlayer: true,
                            AvatarText: GetAvatarText(player.display_name, player.username, player.avatar_url, isCurrentPlayer: true),
                            AvatarImageSource: GetAvatarImageSource(player.display_name, player.username, player.avatar_url, isCurrentPlayer: true),
                            AccentColor: GetGameAccentColor(normalizedGameSourceId)));
                    }
                }

                var gameModeMessage = timeframe == LeaderboardTimeframe.Weekly
                    ? $"Live weekly {GetGameDisplayName(normalizedGameSourceId)} rankings from active players."
                    : $"Live all-time {GetGameDisplayName(normalizedGameSourceId)} rankings from online players.";
                return new PlayerLeaderboardResult(gameEntries, true, gameModeMessage);
            }

            var rows = await JsonSerializer.DeserializeAsync<List<LeaderboardRow>>(stream, JsonOptions)
                       ?? new List<LeaderboardRow>();

            if (rows.Count == 0)
            {
                return BuildFallbackResult(null, "No online scores yet. Showing local fallback.");
            }

            var entries = rows
                .Select((row, index) =>
                {
                    var isCurrentPlayer = string.Equals(row.username, currentUsername, StringComparison.OrdinalIgnoreCase);
                    return new PlayerLeaderboardEntry(
                        Rank: index + 1,
                        PlayerId: row.id,
                        UsernameKey: row.username,
                        Name: string.IsNullOrWhiteSpace(row.display_name) ? row.username : row.display_name,
                        PeakScore: row.peak_brain_score,
                        PeakRank: BrainScoreService.GetPeakRankName(row.peak_brain_score),
                        IsCurrentPlayer: isCurrentPlayer,
                        AvatarText: GetAvatarText(row.display_name, row.username, row.avatar_url, isCurrentPlayer),
                        AvatarImageSource: GetAvatarImageSource(row.display_name, row.username, row.avatar_url, isCurrentPlayer),
                        AccentColor: AccentPalette[index % AccentPalette.Length]);
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(currentUsername) && !entries.Any(entry => entry.IsCurrentPlayer))
            {
                var currentRow = await GetCurrentPlayerRowAsync(currentUsername, timeframe);
                if (currentRow is not null)
                {
                    var currentRank = await GetApproximateRankAsync(currentRow.peak_brain_score, timeframe);
                    entries.Add(new PlayerLeaderboardEntry(
                        Rank: currentRank,
                        PlayerId: currentRow.id,
                        UsernameKey: currentRow.username,
                        Name: string.IsNullOrWhiteSpace(currentRow.display_name) ? currentRow.username : currentRow.display_name,
                        PeakScore: currentRow.peak_brain_score,
                        PeakRank: BrainScoreService.GetPeakRankName(currentRow.peak_brain_score),
                        IsCurrentPlayer: true,
                        AvatarText: GetAvatarText(currentRow.display_name, currentRow.username, currentRow.avatar_url, isCurrentPlayer: true),
                        AvatarImageSource: GetAvatarImageSource(currentRow.display_name, currentRow.username, currentRow.avatar_url, isCurrentPlayer: true),
                        AccentColor: Color.FromArgb("#19B5EA")));
                }
            }

            var modeMessage = timeframe == LeaderboardTimeframe.Weekly
                ? "Live weekly rankings from active players."
                : "Live rankings from online players.";
            return new PlayerLeaderboardResult(entries, true, modeMessage);
        }
        catch (Exception ex)
        {
            return BuildFallbackResult(NormalizeSourceId(gameSourceId), $"Could not reach Supabase. Showing local fallback. {ex.Message}");
        }
    }

    public static async Task<PlayerProfileDetails> GetPlayerProfileAsync(
        PlayerLeaderboardEntry entry,
        LeaderboardTimeframe timeframe,
        string? gameSourceId = null)
    {
        try
        {
            var normalizedGameSourceId = NormalizeSourceId(gameSourceId);
            var playerScoresRequest = CreateRequest(
                HttpMethod.Get,
                $"/rest/v1/player_scores?player_id=eq.{Uri.EscapeDataString(entry.PlayerId)}&select=peak_brain_score,memory,problem_solving,language,mental_agility,focus&limit=1");
            using var playerScoresResponse = await Http.SendAsync(playerScoresRequest);

            PlayerScoreRow? scoreRow = null;
            if (playerScoresResponse.IsSuccessStatusCode)
            {
                await using var scoreStream = await playerScoresResponse.Content.ReadAsStreamAsync();
                scoreRow = (await JsonSerializer.DeserializeAsync<List<PlayerScoreRow>>(scoreStream, JsonOptions)
                           ?? new List<PlayerScoreRow>())
                    .FirstOrDefault();
            }

            var gamesRequest = CreateRequest(
                HttpMethod.Get,
                $"/rest/v1/game_scores?player_id=eq.{Uri.EscapeDataString(entry.PlayerId)}&select=game_id,peak_game_score,last_played_at&limit=100");
            using var gamesResponse = await Http.SendAsync(gamesRequest);

            var gameRows = new List<GameScoreRow>();
            if (gamesResponse.IsSuccessStatusCode)
            {
                await using var gameStream = await gamesResponse.Content.ReadAsStreamAsync();
                gameRows = await JsonSerializer.DeserializeAsync<List<GameScoreRow>>(gameStream, JsonOptions)
                           ?? new List<GameScoreRow>();
            }

            var achievementsRequest = CreateRequest(
                HttpMethod.Get,
                $"/rest/v1/player_achievements?player_id=eq.{Uri.EscapeDataString(entry.PlayerId)}&select=player_id,unlocked_ids&limit=1");
            using var achievementsResponse = await Http.SendAsync(achievementsRequest);

            var unlockedIds = new List<string>();
            if (achievementsResponse.IsSuccessStatusCode)
            {
                await using var achievementsStream = await achievementsResponse.Content.ReadAsStreamAsync();
                var achievementRow = (await JsonSerializer.DeserializeAsync<List<AchievementProfileRow>>(achievementsStream, JsonOptions)
                                      ?? new List<AchievementProfileRow>())
                    .FirstOrDefault();

                if (achievementRow?.unlocked_ids is not null)
                {
                    unlockedIds = achievementRow.unlocked_ids
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                }
            }

            var bestSkillName = scoreRow is not null
                ? GetBestSkillName(scoreRow)
                : GetBestSkillNameFallback(entry, gameRows);

            var gamesPlayed = Math.Max(0, gameRows
                .Where(row => !string.IsNullOrWhiteSpace(row.game_id))
                .Select(row => row.game_id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

            var score = normalizedGameSourceId is not null
                ? gameRows.Where(row => string.Equals(row.game_id, normalizedGameSourceId, StringComparison.OrdinalIgnoreCase))
                    .Select(row => row.peak_game_score)
                    .DefaultIfEmpty(entry.PeakScore)
                    .Max()
                : scoreRow?.peak_brain_score ?? entry.PeakScore;

            var rankLabel = normalizedGameSourceId is not null
                ? $"{GetGameDisplayName(normalizedGameSourceId)} rank #{entry.Rank}"
                : $"Overall rank #{entry.Rank}";

            var scoreLabel = normalizedGameSourceId is not null
                ? $"{GetGameDisplayName(normalizedGameSourceId)} best"
                : "Peak Brain Score";

            var profileAchievements = unlockedIds.Count > 0
                ? AchievementsService.GetAchievementsFromUnlockedIds(unlockedIds)
                : entry.IsCurrentPlayer
                    ? AchievementsService.GetAchievements()
                        .Where(item => item.IsUnlocked)
                        .ToList()
                    : Array.Empty<AchievementItem>();

            return new PlayerProfileDetails(
                Name: entry.Name,
                AvatarText: entry.AvatarText,
                AvatarImageSource: entry.AvatarImageSource,
                CurrentRank: entry.Rank,
                RankLabel: rankLabel,
                BestSkillName: bestSkillName,
                GamesPlayed: gamesPlayed,
                Score: score,
                ScoreLabel: scoreLabel,
                AchievementSummaryText: profileAchievements.Count == 0
                    ? "No achievements unlocked yet"
                    : $"{profileAchievements.Count} unlocked",
                Achievements: profileAchievements);
        }
        catch
        {
            return BuildProfileFallback(entry, NormalizeSourceId(gameSourceId));
        }
    }

    public static async Task SyncCurrentUserAsync()
    {
        _ = await SyncCurrentUserWithResultAsync();
    }

    public static async Task<OnlineSyncResult> SyncCurrentUserProfileOnlyAsync()
    {
        var profileResult = await EnsureCurrentPlayerWithResultAsync();
        return SetLastSyncResult(profileResult.Result);
    }

    public static async Task SyncCurrentUserFullAsync()
    {
        _ = await SyncCurrentUserFullWithResultAsync();
    }

    public static Task<OnlineSyncResult> SyncCurrentUserWithResultAsync()
        => SyncCurrentUserInternalAsync(includeGames: false);

    public static Task<OnlineSyncResult> SyncCurrentUserFullWithResultAsync()
        => SyncCurrentUserInternalAsync(includeGames: true);

    public static async Task SyncGameScoreAsync(string sourceId, int rawScore, int peakGameScore)
    {
        try
        {
            var playerResult = await EnsureCurrentPlayerWithResultAsync();
            if (!playerResult.Result.IsSuccess || playerResult.Player is null || string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            var existingRequest = CreateRequest(HttpMethod.Get,
                $"/rest/v1/game_scores?player_id=eq.{Uri.EscapeDataString(playerResult.Player.id)}&game_id=eq.{Uri.EscapeDataString(sourceId)}&select=best_score,peak_game_score&limit=1");

            using var existingResponse = await Http.SendAsync(existingRequest);
            int bestScore = rawScore;
            int bestPeakScore = peakGameScore;

            if (existingResponse.IsSuccessStatusCode)
            {
                await using var existingStream = await existingResponse.Content.ReadAsStreamAsync();
                var existingRows = await JsonSerializer.DeserializeAsync<List<GameScoreRow>>(existingStream, JsonOptions)
                                   ?? new List<GameScoreRow>();

                if (existingRows.Count > 0)
                {
                    bestScore = Math.Max(bestScore, existingRows[0].best_score);
                    bestPeakScore = Math.Max(bestPeakScore, existingRows[0].peak_game_score);
                }
            }

            var payload = new[]
            {
                new
                {
                    player_id = playerResult.Player.id,
                    game_id = sourceId,
                    best_score = bestScore,
                    peak_game_score = bestPeakScore,
                    last_played_at = DateTime.UtcNow
                }
            };

            var request = CreateRequest(HttpMethod.Post, "/rest/v1/game_scores?on_conflict=player_id,game_id");
            request.Headers.Add("Prefer", "resolution=merge-duplicates");
            request.Content = SerializeContent(payload);

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                SetLastSyncResult(new OnlineSyncResult(
                    false,
                    await BuildFailureMessageAsync(response, "Could not sync the latest game score to Supabase.")));
            }
        }
        catch (Exception ex)
        {
            SetLastSyncResult(new OnlineSyncResult(false, $"Could not sync the latest game score. {ex.Message}"));
        }
    }

    private static async Task<OnlineSyncResult> SyncCurrentUserInternalAsync(bool includeGames)
    {
        try
        {
            var playerResult = await EnsureCurrentPlayerWithResultAsync();
            if (!playerResult.Result.IsSuccess || playerResult.Player is null)
            {
                return SetLastSyncResult(playerResult.Result);
            }

            var scoreResult = await UpsertCurrentScoresWithResultAsync(playerResult.Player.id);
            if (!scoreResult.IsSuccess)
            {
                return SetLastSyncResult(scoreResult);
            }

            if (includeGames)
            {
                var gamesResult = await UpsertPlayedGamesWithResultAsync(playerResult.Player.id);
                if (!gamesResult.IsSuccess)
                {
                    return SetLastSyncResult(gamesResult);
                }
            }

            var successMessage = includeGames
                ? "Online sync succeeded for account, scores, and played games."
                : "Online sync succeeded for account and scores.";

            return SetLastSyncResult(new OnlineSyncResult(true, successMessage));
        }
        catch (Exception ex)
        {
            return SetLastSyncResult(new OnlineSyncResult(false, $"Could not reach Supabase. {ex.Message}"));
        }
    }

    private static async Task<(PlayerRow? Player, OnlineSyncResult Result)> EnsureCurrentPlayerWithResultAsync()
    {
        if (!LocalAccountStore.TryGetProfile(out var profile) || string.IsNullOrWhiteSpace(profile.Username))
        {
            return (null, new OnlineSyncResult(false, "No signed-in account found for online sync."));
        }

        var usernameKey = GetCurrentUsernameKey();
        var displayName = GetCurrentPlayerDisplayName().Trim();
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

        return (player, new OnlineSyncResult(true, "Player profile synced online."));
    }

    private static async Task<OnlineSyncResult> UpsertCurrentScoresWithResultAsync(string playerId)
    {
        if (!BrainScoreService.HasResolvedCurrentUserHistory)
        {
            return new OnlineSyncResult(true, "Skipped player score sync until score history finishes loading.");
        }

        var scores = BrainScoreService.GetCurrentScores();
        var payload = new[]
        {
            new
            {
                player_id = playerId,
                peak_brain_score = scores.PeakScore,
                memory = scores.Memory,
                problem_solving = scores.ProblemSolving,
                language = scores.Language,
                mental_agility = scores.MentalAgility,
                focus = scores.Focus,
                updated_at = DateTime.UtcNow
            }
        };

        var request = CreateRequest(HttpMethod.Post, "/rest/v1/player_scores?on_conflict=player_id");
        request.Headers.Add("Prefer", "resolution=merge-duplicates");
        request.Content = SerializeContent(payload);

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return new OnlineSyncResult(
                false,
                await BuildFailureMessageAsync(response, "Could not save player scores to Supabase."));
        }

        return new OnlineSyncResult(true, "Player scores synced online.");
    }

    private static async Task<OnlineSyncResult> UpsertPlayedGamesWithResultAsync(string playerId)
    {
        if (!BrainScoreService.HasResolvedCurrentUserHistory)
        {
            return new OnlineSyncResult(true, "Skipped played game sync until score history finishes loading.");
        }

        var games = BrainScoreService.GetPlayedGameScores();
        if (games.Count == 0)
        {
            return new OnlineSyncResult(true, "No played games yet to sync.");
        }

        var performanceBySource = games
            .Select(game => BrainScoreService.GetGamePerformance(game.SourceId))
            .Where(snapshot => snapshot is not null)
            .Select(snapshot => snapshot!)
            .ToDictionary(snapshot => snapshot.SourceId, StringComparer.Ordinal);

        var payload = games
            .Select(game =>
            {
                performanceBySource.TryGetValue(game.SourceId, out var snapshot);
                return new
                {
                    player_id = playerId,
                    game_id = game.SourceId,
                    best_score = snapshot?.BestScore ?? game.PeakGameScore,
                    peak_game_score = game.PeakGameScore,
                    last_played_at = game.LastPlayedUtc
                };
            })
            .ToArray();

        if (payload.Length == 0)
        {
            return new OnlineSyncResult(true, "No played games yet to sync.");
        }

        var request = CreateRequest(HttpMethod.Post, "/rest/v1/game_scores?on_conflict=player_id,game_id");
        request.Headers.Add("Prefer", "resolution=merge-duplicates");
        request.Content = SerializeContent(payload);

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return new OnlineSyncResult(
                false,
                await BuildFailureMessageAsync(response, "Could not save played games to Supabase."));
        }

        return new OnlineSyncResult(true, "Played games synced online.");
    }

    private static PlayerLeaderboardResult BuildFallbackResult(string? gameSourceId, string message)
    {
        if (gameSourceId is not null)
        {
            return BuildFallbackGameResult(gameSourceId, message);
        }

        var scores = BrainScoreService.GetCurrentScores();
        var currentName = GetCurrentPlayerDisplayName();
        var currentUsername = GetCurrentUsernameKey();
        var currentAvatar = GetCurrentAvatarText(currentName, currentUsername);
        var seed = HashCode.Combine(currentName, scores.PeakScore, scores.Memory, scores.Focus);
        var random = new Random(seed);
        var entries = new List<PlayerLeaderboardEntry>();

        for (int i = 0; i < 14; i++)
        {
            var baseOffset = 210 - (i * 28);
            var wobble = random.Next(-16, 17);
            var peerScore = Math.Clamp(scores.PeakScore + baseOffset + wobble, 120, 995);

            entries.Add(new PlayerLeaderboardEntry(
                Rank: 0,
                PlayerId: $"sample-{i + 1}",
                UsernameKey: NormalizeUsername(SampleNames[i % SampleNames.Length]),
                Name: SampleNames[i % SampleNames.Length],
                PeakScore: peerScore,
                PeakRank: BrainScoreService.GetPeakRankName(peerScore),
                IsCurrentPlayer: false,
                AvatarText: SampleAvatars[i % SampleAvatars.Length],
                AvatarImageSource: null,
                AccentColor: AccentPalette[i % AccentPalette.Length]));
        }

        entries.Add(new PlayerLeaderboardEntry(
            Rank: 0,
            PlayerId: "current-player",
            UsernameKey: currentUsername,
            Name: currentName,
            PeakScore: scores.PeakScore,
            PeakRank: BrainScoreService.GetPeakRankName(scores.PeakScore),
            IsCurrentPlayer: true,
            AvatarText: currentAvatar,
                AvatarImageSource: GetCurrentAvatarImageSource(currentName, currentUsername),
            AccentColor: Color.FromArgb("#19B5EA")));

        var ranked = entries
            .OrderByDescending(entry => entry.PeakScore)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select((entry, index) => entry with { Rank = index + 1 })
            .ToList();

        return new PlayerLeaderboardResult(ranked, false, message);
    }

    private static PlayerLeaderboardResult BuildFallbackGameResult(string gameSourceId, string message)
    {
        var gameName = GetGameDisplayName(gameSourceId);
        var currentName = GetCurrentPlayerDisplayName();
        var currentUsername = GetCurrentUsernameKey();
        var currentAvatar = GetCurrentAvatarText(currentName, currentUsername);
        var currentSnapshot = BrainScoreService.GetGamePerformance(gameSourceId);
        var currentScore = currentSnapshot?.BestPeakGameScore ?? 0;
        var seed = HashCode.Combine(currentName, gameSourceId, currentScore);
        var random = new Random(seed);
        var entries = new List<PlayerLeaderboardEntry>();

        for (int i = 0; i < 12; i++)
        {
            var baseOffset = 180 - (i * 24);
            var wobble = random.Next(-18, 19);
            var peerScore = Math.Clamp(currentScore + baseOffset + wobble, 120, 995);

            entries.Add(new PlayerLeaderboardEntry(
                Rank: 0,
                PlayerId: $"sample-game-{i + 1}",
                UsernameKey: NormalizeUsername(SampleNames[i % SampleNames.Length]),
                Name: SampleNames[i % SampleNames.Length],
                PeakScore: peerScore,
                PeakRank: BrainScoreService.GetPeakRankName(peerScore),
                IsCurrentPlayer: false,
                AvatarText: SampleAvatars[i % SampleAvatars.Length],
                AvatarImageSource: null,
                AccentColor: GetGameAccentColor(gameSourceId)));
        }

        entries.Add(new PlayerLeaderboardEntry(
            Rank: 0,
            PlayerId: "current-player",
            UsernameKey: currentUsername,
            Name: currentName,
            PeakScore: currentScore,
            PeakRank: BrainScoreService.GetPeakRankName(currentScore),
            IsCurrentPlayer: true,
            AvatarText: currentAvatar,
            AvatarImageSource: GetCurrentAvatarImageSource(currentName, currentUsername),
            AccentColor: GetGameAccentColor(gameSourceId)));

        var ranked = entries
            .OrderByDescending(entry => entry.PeakScore)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select((entry, index) => entry with { Rank = index + 1 })
            .ToList();

        return new PlayerLeaderboardResult(ranked, false, $"{message} {gameName} players shown from local fallback.");
    }

    private static async Task<LeaderboardRow?> GetCurrentPlayerRowAsync(
        string usernameKey,
        LeaderboardTimeframe timeframe)
    {
        var timeframeFilter = BuildTimeframeFilter(timeframe);
        var request = CreateRequest(
            HttpMethod.Get,
            $"/rest/v1/leaderboard_overall?select=id,username,display_name,avatar_url,peak_brain_score,updated_at&username=eq.{Uri.EscapeDataString(usernameKey)}{timeframeFilter}&limit=1");

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var rows = await JsonSerializer.DeserializeAsync<List<LeaderboardRow>>(stream, JsonOptions)
                   ?? new List<LeaderboardRow>();
        return rows.FirstOrDefault();
    }

    private static async Task<GameLeaderboardRow?> GetCurrentPlayerGameRowAsync(
        string usernameKey,
        string gameSourceId,
        LeaderboardTimeframe timeframe)
    {
        var timeframeFilter = BuildGameTimeframeFilter(timeframe);
        var request = CreateRequest(
            HttpMethod.Get,
            $"/rest/v1/game_scores?select=player_id,game_id,peak_game_score,last_played_at,players!inner(id,username,display_name,avatar_url)&game_id=eq.{Uri.EscapeDataString(gameSourceId)}&players.username=eq.{Uri.EscapeDataString(usernameKey)}{timeframeFilter}&limit=1");

        using var response = await Http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        var rows = await JsonSerializer.DeserializeAsync<List<GameLeaderboardRow>>(stream, JsonOptions)
                   ?? new List<GameLeaderboardRow>();
        return rows.FirstOrDefault();
    }

    private static async Task<int> GetApproximateRankAsync(int peakScore, LeaderboardTimeframe timeframe)
    {
        try
        {
            var timeframeFilter = BuildPlayerScoreTimeframeFilter(timeframe);
            var request = CreateRequest(
                HttpMethod.Head,
                $"/rest/v1/player_scores?player_id=not.is.null&peak_brain_score=gt.{Math.Max(0, peakScore)}{timeframeFilter}&select=player_id");
            request.Headers.Add("Prefer", "count=exact");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            var count = ExtractCount(response);
            return count + 1;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<int> GetApproximateGameRankAsync(int peakScore, string gameSourceId, LeaderboardTimeframe timeframe)
    {
        try
        {
            var timeframeFilter = BuildGameTimeframeFilter(timeframe);
            var request = CreateRequest(
                HttpMethod.Head,
                $"/rest/v1/game_scores?game_id=eq.{Uri.EscapeDataString(gameSourceId)}&peak_game_score=gt.{Math.Max(0, peakScore)}{timeframeFilter}&select=player_id");
            request.Headers.Add("Prefer", "count=exact");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return 0;
            }

            var count = ExtractCount(response);
            return count + 1;
        }
        catch
        {
            return 0;
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(SupabaseConfig.ProjectUrl)
        };

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
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

    private static string GetCurrentPlayerDisplayName()
    {
        if (LocalAccountStore.TryGetProfile(out var profile) && !string.IsNullOrWhiteSpace(profile.Username))
        {
            return profile.Username;
        }

        var fallback = LocalAccountStore.GetLastActiveUsername();
        return string.IsNullOrWhiteSpace(fallback) ? "You" : fallback;
    }

    private static string GetCurrentUsernameKey()
    {
        var usernameKey = LocalAccountStore.GetLastActiveUsername();
        if (!string.IsNullOrWhiteSpace(usernameKey))
        {
            return NormalizeUsername(usernameKey);
        }

        return NormalizeUsername(GetCurrentPlayerDisplayName());
    }

    private static bool TryGetCurrentAvatarProfile(string? displayName, string? usernameKey, out LocalAvatarProfile avatar)
    {
        return AvatarImageSyncHelper.TryGetAvatarProfile(
            string.IsNullOrWhiteSpace(usernameKey) ? GetCurrentUsernameKey() : usernameKey,
            string.IsNullOrWhiteSpace(displayName) ? GetCurrentPlayerDisplayName() : displayName,
            out avatar);
    }

    private static string GetCurrentAvatarText(string? displayName, string? usernameKey)
    {
        if (TryGetCurrentAvatarProfile(displayName, usernameKey, out var avatar))
        {
            if (string.Equals(avatar.Mode, LocalAccountStore.AvatarModePhoto, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(avatar.Value)
                && File.Exists(avatar.Value))
            {
                var fallbackName = string.IsNullOrWhiteSpace(displayName) ? usernameKey : displayName;
                return string.IsNullOrWhiteSpace(fallbackName)
                    ? "Y"
                    : char.ToUpperInvariant(fallbackName.Trim()[0]).ToString();
            }

            if (string.Equals(avatar.Mode, LocalAccountStore.AvatarModeEmoji, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(avatar.Value))
            {
                return avatar.Value;
            }
        }

        var source = string.IsNullOrWhiteSpace(displayName) ? usernameKey : displayName;
        return string.IsNullOrWhiteSpace(source)
            ? "Y"
            : char.ToUpperInvariant(source.Trim()[0]).ToString();
    }

    private static string? GetCurrentAvatarImageSource(string? displayName, string? usernameKey)
    {
        return AvatarImageSyncHelper.ResolveLocalAvatarImagePath(usernameKey, displayName);
    }

    private static string GetAvatarText(string? displayName, string username, string? avatarUrl, bool isCurrentPlayer)
    {
        if (isCurrentPlayer)
        {
            return GetCurrentAvatarText(displayName, username);
        }

        if (!string.IsNullOrWhiteSpace(avatarUrl)
            && avatarUrl.StartsWith(EmojiAvatarPrefix, StringComparison.Ordinal))
        {
            return avatarUrl[EmojiAvatarPrefix.Length..];
        }

        var source = string.IsNullOrWhiteSpace(displayName) ? username : displayName;
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            return char.ToUpperInvariant(source.Trim()[0]).ToString();
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            return "P";
        }

        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        return char.ToUpperInvariant(source[0]).ToString();
    }

    private static string? GetAvatarImageSource(string? displayName, string username, string? avatarUrl, bool isCurrentPlayer)
    {
        if (isCurrentPlayer)
        {
            return GetCurrentAvatarImageSource(displayName, username)
                   ?? (AvatarImageSyncHelper.TryCreateImageSource(avatarUrl, out _) ? avatarUrl : null);
        }

        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return null;
        }

        if (avatarUrl.StartsWith(EmojiAvatarPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        if (AvatarImageSyncHelper.TryCreateImageSource(avatarUrl, out _))
        {
            return avatarUrl;
        }

        return null;
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    private static string? NormalizeSourceId(string? sourceId)
    {
        return string.IsNullOrWhiteSpace(sourceId)
            ? null
            : sourceId.Trim().ToLowerInvariant();
    }

    private static int ExtractCount(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Content-Range", out var values))
        {
            return 0;
        }

        var raw = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var slashIndex = raw.LastIndexOf('/');
        if (slashIndex < 0 || slashIndex >= raw.Length - 1)
        {
            return 0;
        }

        return int.TryParse(raw[(slashIndex + 1)..], out var count)
            ? Math.Max(0, count)
            : 0;
    }

    private static async Task<string> BuildFailureMessageAsync(HttpResponseMessage response, string fallback)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden
                || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return "Supabase rejected leaderboard access. Check your RLS policies for read and write access.";
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "Leaderboard tables or view are missing in Supabase. Showing local fallback.";
            }

            if (!string.IsNullOrWhiteSpace(body) && body.Contains("row-level security", StringComparison.OrdinalIgnoreCase))
            {
                return "Supabase row-level security is blocking leaderboard access. Update your RLS policies.";
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                return $"{fallback} Details: {body}";
            }
        }
        catch
        {
            // Ignore parse issues and fall back to the default message.
        }

        return fallback;
    }

    private static OnlineSyncResult SetLastSyncResult(OnlineSyncResult result)
    {
        LastSyncResult = result;
        return result;
    }

    private static string BuildLeaderboardQuery(int safeLimit, LeaderboardTimeframe timeframe, string? gameSourceId)
    {
        if (gameSourceId is not null)
        {
            var gameTimeframeFilter = BuildGameTimeframeFilter(timeframe);
            return $"/rest/v1/game_scores?select=player_id,game_id,peak_game_score,last_played_at,players!inner(id,username,display_name,avatar_url)&game_id=eq.{Uri.EscapeDataString(gameSourceId)}{gameTimeframeFilter}&order=peak_game_score.desc&limit={safeLimit}";
        }

        var timeframeFilter = BuildTimeframeFilter(timeframe);
        return $"/rest/v1/leaderboard_overall?select=id,username,display_name,avatar_url,peak_brain_score,updated_at{timeframeFilter}&order=peak_brain_score.desc&limit={safeLimit}";
    }

    private static string BuildTimeframeFilter(LeaderboardTimeframe timeframe)
    {
        if (timeframe != LeaderboardTimeframe.Weekly)
        {
            return string.Empty;
        }

        var start = DateTime.UtcNow.Date.AddDays(-6).ToString("O");
        return $"&updated_at=gte.{Uri.EscapeDataString(start)}";
    }

    private static string BuildPlayerScoreTimeframeFilter(LeaderboardTimeframe timeframe)
    {
        if (timeframe != LeaderboardTimeframe.Weekly)
        {
            return string.Empty;
        }

        var start = DateTime.UtcNow.Date.AddDays(-6).ToString("O");
        return $"&updated_at=gte.{Uri.EscapeDataString(start)}";
    }

    private static string BuildGameTimeframeFilter(LeaderboardTimeframe timeframe)
    {
        if (timeframe != LeaderboardTimeframe.Weekly)
        {
            return string.Empty;
        }

        var start = DateTime.UtcNow.Date.AddDays(-6).ToString("O");
        return $"&last_played_at=gte.{Uri.EscapeDataString(start)}";
    }

    public static string GetGameDisplayName(string sourceId)
    {
        return SupportedGames.FirstOrDefault(game => string.Equals(game.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))?.Name
               ?? sourceId.Replace('_', ' ').Trim();
    }

    public static Color GetGameAccentColor(string sourceId)
    {
        return SupportedGames.FirstOrDefault(game => string.Equals(game.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))?.AccentColor
               ?? Color.FromArgb("#7A73E8");
    }

    private static string GetBestSkillName(PlayerScoreRow scoreRow)
    {
        var ordered = new[]
        {
            ("Memory", scoreRow.memory),
            ("Problem Solving", scoreRow.problem_solving),
            ("Language", scoreRow.language),
            ("Mental Agility", scoreRow.mental_agility),
            ("Focus", scoreRow.focus)
        };

        return ordered
            .OrderByDescending(item => item.Item2)
            .ThenBy(item => item.Item1, StringComparer.Ordinal)
            .First().Item1;
    }

    private static string GetBestSkillNameFallback(PlayerLeaderboardEntry entry, IReadOnlyList<GameScoreRow> gameRows)
    {
        var bestGame = gameRows
            .OrderByDescending(row => row.peak_game_score)
            .FirstOrDefault();

        if (bestGame is not null)
        {
            var match = SupportedGames.FirstOrDefault(game => string.Equals(game.SourceId, bestGame.game_id, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match.Skill switch
                {
                    BrainSkill.Memory => "Memory",
                    BrainSkill.ProblemSolving => "Problem Solving",
                    BrainSkill.Language => "Language",
                    BrainSkill.Focus => "Focus",
                    _ => entry.PeakRank
                };
            }
        }

        return entry.PeakRank;
    }

    private static PlayerProfileDetails BuildProfileFallback(PlayerLeaderboardEntry entry, string? gameSourceId)
    {
        var scores = BrainScoreService.GetCurrentScores();
        var isCurrent = entry.IsCurrentPlayer;
        var bestSkillName = isCurrent
            ? GetBestSkillName(new PlayerScoreRow(
                scores.PeakScore,
                scores.Memory,
                scores.ProblemSolving,
                scores.Language,
                scores.MentalAgility,
                scores.Focus))
            : entry.PeakRank;

        var gamesPlayed = isCurrent
            ? BrainScoreService.GetPlayedGameScores().Count
            : 0;

        var score = gameSourceId is not null
            ? BrainScoreService.GetGamePerformance(gameSourceId)?.BestPeakGameScore ?? entry.PeakScore
            : (isCurrent ? scores.PeakScore : entry.PeakScore);

        return new PlayerProfileDetails(
            Name: entry.Name,
            AvatarText: entry.AvatarText,
            AvatarImageSource: entry.AvatarImageSource,
            CurrentRank: entry.Rank,
            RankLabel: gameSourceId is null ? $"Overall rank #{entry.Rank}" : $"{GetGameDisplayName(gameSourceId)} rank #{entry.Rank}",
            BestSkillName: bestSkillName,
            GamesPlayed: gamesPlayed,
            Score: score,
            ScoreLabel: gameSourceId is null ? "Peak Brain Score" : $"{GetGameDisplayName(gameSourceId)} best",
            AchievementSummaryText: isCurrent
                ? $"{AchievementsService.GetAchievements().Count(item => item.IsUnlocked)} unlocked"
                : "No achievements unlocked yet",
            Achievements: isCurrent
                ? AchievementsService.GetAchievements()
                    .Where(item => item.IsUnlocked)
                    .ToList()
                : Array.Empty<AchievementItem>());
    }
}
