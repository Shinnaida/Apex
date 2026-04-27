namespace Peak;

public partial class UnlockGameSheetPage : ContentPage
{
    static int _isPresenting;
    readonly TaskCompletionSource<bool> _resultSource = new();
    bool _isClosing;

    public UnlockGameSheetPage(GameStoreEntry entry, int balance)
    {
        InitializeComponent();

        GameTitleLabel.Text = entry.Title;
        RequirementLabel.Text = $"Use Apex Points to unlock this brain-training game.";
        CostLabel.Text = entry.UnlockCost.ToString("N0");
        BalanceLabel.Text = balance.ToString("N0");
        UnlockHintLabel.Text = balance >= entry.UnlockCost
            ? "You have enough points to unlock this game instantly."
            : $"You need {(entry.UnlockCost - balance):N0} more points. Complete daily tasks and play games to earn more.";

        var accent = Color.FromArgb(entry.AccentHex);
        SheetCard.BackgroundColor = accent.WithAlpha(0.08f);
        BalanceCard.BackgroundColor = accent.WithAlpha(0.09f);
        BalanceCard.Stroke = new SolidColorBrush(accent.WithAlpha(0.22f));
        HintCard.BackgroundColor = accent.WithAlpha(0.12f);
        IconShell.BackgroundColor = accent.WithAlpha(0.14f);
        GameIconImage.Source = entry.IconSource;
        RequirementLabel.TextColor = Colors.White;
        UnlockHintLabel.TextColor = Colors.White;
        CostLabel.TextColor = accent;
        UnlockButton.BackgroundColor = accent;
        UnlockButton.IsEnabled = balance >= entry.UnlockCost;
        UnlockButton.Opacity = UnlockButton.IsEnabled ? 1 : 0.55;
    }

    public static async Task<bool> ShowAsync(INavigation navigation, GameStoreEntry entry)
    {
        if (Interlocked.Exchange(ref _isPresenting, 1) == 1)
        {
            return false;
        }

        var page = new UnlockGameSheetPage(entry, GamePointsService.GetBalance());
        try
        {
            await navigation.PushModalAsync(page, false);
            return await page._resultSource.Task;
        }
        finally
        {
            Interlocked.Exchange(ref _isPresenting, 0);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Opacity = 0;
        await Task.WhenAll(
            this.FadeTo(1, 160, Easing.CubicOut),
            SheetCard.TranslateTo(0, 0, 220, Easing.CubicOut),
            SheetCard.ScaleTo(1, 220, Easing.CubicOut));
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(false);
        return true;
    }

    async void OnBackdropTapped(object sender, TappedEventArgs e) => await CloseAsync(false);

    async void OnCloseClicked(object sender, EventArgs e) => await CloseAsync(false);

    async void OnUnlockClicked(object sender, EventArgs e) => await CloseAsync(true);

    async Task CloseAsync(bool result)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;

        await Task.WhenAll(
            this.FadeTo(0, 140, Easing.CubicIn),
            SheetCard.TranslateTo(0, 26, 140, Easing.CubicIn),
            SheetCard.ScaleTo(0.96, 140, Easing.CubicIn));

        await Navigation.PopModalAsync(false);
        _resultSource.TrySetResult(result);
    }
}
