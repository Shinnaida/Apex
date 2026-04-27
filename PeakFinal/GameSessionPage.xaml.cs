namespace Peak;

public partial class GameSessionPage : ContentPage
{
    readonly GameSession _session;

    public GameSessionPage(GameCategory category)
    {
        InitializeComponent();

        _session = new GameSession(category, gamesPerSession: 5);

        TitleLabel.Text = $"{category} Session";
        SubtitleLabel.Text = "You’ll get a randomized set of games.";
    }

    protected override bool OnBackButtonPressed() => true;

    async void OnStartClicked(object sender, EventArgs e)
    {
        await GoNextGame();
    }

    async Task GoNextGame()
    {
        if (!_session.TryNext(out var next) || next is null)
        {
            await DisplayAlert("Done", "Session complete!", "OK");
            await Navigation.PopToRootAsync();
            return;
        }

        var page = next.CreatePage();
        await PageTransitionService.PushAsync(Navigation, new GameWrapperPage(next.Title, page, GoNextGame));
    }
}

