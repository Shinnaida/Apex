namespace Peak;

public partial class TestsPage : ContentPage
{
    public TestsPage()
    {
        InitializeComponent();
    }

    async void OnGeneralChallengeClicked(object sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            await InteractionEffects.AnimateTapAsync(element);
        }

        await LaunchAsync(IQCatalog.GeneralChallenge);
    }

    async void OnQuickCheckClicked(object sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            await InteractionEffects.AnimateTapAsync(element);
        }

        await LaunchAsync(IQCatalog.QuickCheck);
    }

    async void OnGeneralChallengeTapped(object sender, TappedEventArgs e)
    {
        if (GeneralCard is not null)
        {
            await InteractionEffects.AnimateTapAsync(GeneralCard);
        }
        await LaunchAsync(IQCatalog.GeneralChallenge);
    }

    async void OnQuickCheckTapped(object sender, TappedEventArgs e)
    {
        if (QuickCard is not null)
        {
            await InteractionEffects.AnimateTapAsync(QuickCard);
        }
        await LaunchAsync(IQCatalog.QuickCheck);
    }

    Task LaunchAsync(IQTestDefinition definition)
    {
        return PageTransitionService.PushAsync(Navigation, () => new TestYourselfPage(definition));
    }
}
