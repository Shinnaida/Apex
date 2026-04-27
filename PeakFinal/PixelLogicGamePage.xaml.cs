using System.Text.Json;

namespace Peak;

public partial class PixelLogicGamePage : ContentPage
{
    bool _completionHandled;

    public PixelLogicGamePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPixelLogicAsync();
    }

    async Task LoadPixelLogicAsync()
    {
        LoadingIndicator.IsRunning = true;
        try
        {
            string html = await ReadRawTextAsync("pixel_logic/index.html");
            string css = await ReadRawTextAsync("pixel_logic/styles.css");
            string js = await ReadRawTextAsync("pixel_logic/script.js");

            html = html.Replace("<link rel=\"stylesheet\" href=\"styles.css\">", $"<style>{css}</style>");
            html = html.Replace("<script src=\"script.js\"></script>", $"<script>{js}</script>");

            GameWebView.Source = new HtmlWebViewSource
            {
                Html = html
            };
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
        }
    }

    static async Task<string> ReadRawTextAsync(string path)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(path);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await PageTransitionService.PopAsync(Navigation);
    }

    async void OnWebViewNavigating(object sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("pixellogic://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;

        try
        {
            var uri = new Uri(e.Url);
            var action = uri.Host;
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var data = query["data"];

            if (string.Equals(action, "complete", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(data))
            {
                var payload = JsonSerializer.Deserialize<PixelLogicCompletePayload>(Uri.UnescapeDataString(data));
                if (payload is not null && !_completionHandled)
                {
                    _completionHandled = true;

                    var previousBest = BrainScoreService.GetGamePerformance("pixel_logic")?.BestScore ?? 0;
                    var bestScore = Math.Max(previousBest, payload.Score);
                    var isNewBest = payload.Score > previousBest;
                    var apexPoints = BrainScoreService.RecordGameScore("pixel_logic", BrainSkill.ProblemSolving, payload.Score, 1500);

                    await PageTransitionService.PushAsync(
                        Navigation,
                        () => new GenericGameSummaryPage(
                            gameTitle: "Pixel Logic",
                            score: payload.Score,
                            bestScore: bestScore,
                            apexPoints: apexPoints,
                            isNewBest: isNewBest,
                            playAgainFactory: () => new PixelLogicGamePage(),
                            accentHex: "#77C987"));
                }
            }
        }
        catch
        {
            // Ignore host callback parse errors so the game keeps running.
        }
    }

    sealed class PixelLogicCompletePayload
    {
        public int Score { get; set; }
        public int Seconds { get; set; }
        public string PuzzleId { get; set; } = string.Empty;
    }
}
