using System.Net.Http.Headers;
using System.Text.Json;

namespace Peak;

public sealed record OnlineAccountRestoreLookupResult(
    bool IsSuccess,
    bool AccountExists,
    string DisplayName,
    string Message);

public static class OnlineAccountRestoreService
{
    private sealed record PlayerRow(string username, string? display_name);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HttpClient Http = CreateHttpClient();

    public static async Task<OnlineAccountRestoreLookupResult> LookupAccountAsync(string username)
    {
        var normalizedUsername = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return new OnlineAccountRestoreLookupResult(false, false, string.Empty, "Enter a valid username.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/rest/v1/players?select=username,display_name&username=eq.{Uri.EscapeDataString(normalizedUsername)}&limit=1");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return new OnlineAccountRestoreLookupResult(
                    false,
                    false,
                    string.Empty,
                    $"Could not verify this account online right now. {response.ReasonPhrase}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            var players = await JsonSerializer.DeserializeAsync<List<PlayerRow>>(stream, JsonOptions) ?? new List<PlayerRow>();
            var player = players.FirstOrDefault();
            if (player is null)
            {
                return new OnlineAccountRestoreLookupResult(true, false, string.Empty, "No saved online account was found for that username.");
            }

            return new OnlineAccountRestoreLookupResult(
                true,
                true,
                string.IsNullOrWhiteSpace(player.display_name) ? player.username : player.display_name.Trim(),
                "Saved online account found.");
        }
        catch (Exception ex)
        {
            return new OnlineAccountRestoreLookupResult(
                false,
                false,
                string.Empty,
                $"Could not reach the account service. {ex.Message}");
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(SupabaseConfig.ProjectUrl)
        };

        client.DefaultRequestHeaders.Add("apikey", SupabaseConfig.AnonKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseConfig.AnonKey);
        return client;
    }

    private static string NormalizeUsername(string username)
        => username.Trim().ToLowerInvariant();
}
