namespace Peak;

public class GameWrapperPage : ContentPage
{
    readonly Func<Task> _onNext;

    public GameWrapperPage(string title, ContentPage innerGamePage, Func<Task> onNext)
    {
        _onNext = onNext;
        Title = title;

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);

        var nextButton = new Button
        {
            Text = "Next Game",
            CornerRadius = 26,
            HeightRequest = 52,
            BackgroundColor = Color.FromArgb("#1DA1F2"),
            TextColor = Colors.White
        };

        nextButton.Clicked += async (_, __) =>
        {
            await PageTransitionService.PopAsync(Navigation);
            await _onNext();
        };

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        // Put game content on top
        var gameContent = innerGamePage.Content ?? new Label { Text = "Game has no content." };
        grid.Add(gameContent);
        Grid.SetRow(gameContent, 0);

        // Bottom button
        var bottom = new VerticalStackLayout
        {
            Padding = new Thickness(20, 10),
            Children = { nextButton }
        };

        grid.Add(bottom);
        Grid.SetRow(bottom, 1);

        Content = grid;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        NavigationPage.SetHasBackButton(this, false);
    }

    protected override bool OnBackButtonPressed() => true;
}

