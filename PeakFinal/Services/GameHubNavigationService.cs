namespace Peak;

public static class GameHubNavigationService
{
    public static async Task ReturnToNearestHubAsync(INavigation navigation)
    {
        var launchRoute = GameLaunchContextService.GetReturnRoute();
        if (!string.IsNullOrWhiteSpace(launchRoute))
        {
            GameLaunchContextService.Clear();
            await PageTransitionService.GoToAsync(launchRoute);
            return;
        }

        var stack = navigation.NavigationStack;
        var hubPage = stack.LastOrDefault(page =>
            page is TodayPage ||
            page is AllGamesPage ||
            page is TestsPage ||
            page is TrainingPickerPage);

        if (hubPage is null)
        {
            await PageTransitionService.PopAsync(navigation);
            return;
        }

        while (navigation.NavigationStack.LastOrDefault() != hubPage)
        {
            await PageTransitionService.PopAsync(navigation);
        }
    }
}
