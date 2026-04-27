namespace Peak;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    async void OnGetStartedClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, () => new MemoryGamePage());
    }

    async void OnHaveAccountClicked(object sender, EventArgs e)
    {
        await PageTransitionService.PushAsync(Navigation, new AccountAccessPage());
    }
}

