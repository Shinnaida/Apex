namespace Peak;

public static class PageTransitionService
{
    static readonly SemaphoreSlim NavigationGate = new(1, 1);
    static int _navigationSequence;

    public static void AttachShell(Shell shell)
    {
        shell.Navigated -= OnShellNavigated;
        shell.Navigated += OnShellNavigated;

        shell.Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(20);
            await AnimatePageAsync(shell.CurrentPage);
        });
    }

    public static async Task GoToAsync(string route)
    {
        await NavigationGate.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return;
            }

            if (Shell.Current is not null)
            {
                try
                {
                    await Shell.Current.GoToAsync(route, false);
                }
                catch
                {
                    if (TryCreateFallbackPage(route, out var fallbackPage))
                    {
                        PreparePage(fallbackPage);
                        await Shell.Current.Navigation.PushAsync(fallbackPage, false);
                    }
                }
            }
        }
        finally
        {
            NavigationGate.Release();
        }
    }

    public static async Task GoToAsync(string route, IDictionary<string, object> parameters)
    {
        await NavigationGate.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return;
            }

            if (Shell.Current is not null)
            {
                try
                {
                    await Shell.Current.GoToAsync(route, false, parameters);
                }
                catch
                {
                    if (TryCreateFallbackPage(route, out var fallbackPage))
                    {
                        PreparePage(fallbackPage);
                        await Shell.Current.Navigation.PushAsync(fallbackPage, false);
                    }
                }
            }
        }
        finally
        {
            NavigationGate.Release();
        }
    }

    public static async Task PushAsync(INavigation navigation, Page page)
    {
        await PushCoreAsync(
            navigation,
            () => page,
            page.GetType());
    }

    public static Task PushAsync(INavigation navigation, Func<Page?> pageFactory)
    {
        return PushCoreAsync(navigation, pageFactory, null);
    }

    public static Task PushAsync<TPage>(INavigation navigation, Func<TPage?> pageFactory)
        where TPage : Page
    {
        return PushCoreAsync(navigation, () => pageFactory(), typeof(TPage));
    }

    static async Task PushCoreAsync(INavigation navigation, Func<Page?> pageFactory, Type? hintedPageType)
    {
        await NavigationGate.WaitAsync();
        var navigationToken = Interlocked.Increment(ref _navigationSequence);

        try
        {
            if (navigationToken != _navigationSequence)
            {
                return;
            }

            var currentPage = navigation.NavigationStack.LastOrDefault();
            GameLaunchContextService.CaptureFromPage(currentPage);

            if (ShouldShowLaunchCountdown(hintedPageType))
            {
                await GameLaunchCountdownService.ShowAsync(currentPage);
            }

            var page = pageFactory();
            if (page is null)
            {
                return;
            }

            if (navigation.NavigationStack.LastOrDefault()?.GetType() == page.GetType())
            {
                return;
            }

            if (!ShouldShowLaunchCountdown(hintedPageType) && ShouldShowLaunchCountdown(page))
            {
                await GameLaunchCountdownService.ShowAsync(currentPage);
            }

            PreparePage(page);
            try
            {
                await navigation.PushAsync(page, false);
            }
            catch
            {
                // Keep the app responsive if a platform navigation edge case occurs.
            }
        }
        finally
        {
            NavigationGate.Release();
        }
    }

    public static async Task PopAsync(INavigation navigation)
    {
        await NavigationGate.WaitAsync();
        try
        {
            if (navigation.NavigationStack.Count > 1)
            {
                try
                {
                    await navigation.PopAsync(false);
                }
                catch
                {
                    // Ignore platform-specific pop failures so the app does not crash.
                }
            }
        }
        finally
        {
            NavigationGate.Release();
        }
    }

    static async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        if (sender is not Shell shell)
        {
            return;
        }

        await AnimatePageAsync(shell.CurrentPage);
    }

    static void PreparePage(Page page)
    {
        ApplyGameChrome(page);

        async void OnAppearing(object? sender, EventArgs args)
        {
            page.Appearing -= OnAppearing;
            ApplyGameChrome(page);
            await AnimatePageAsync(page);
        }

        page.Appearing -= OnAppearing;
        page.Appearing += OnAppearing;
    }

    static void ApplyGameChrome(Page page)
    {
        if (!IsGamePage(page))
        {
            return;
        }

        Shell.SetNavBarIsVisible(page, false);
        Shell.SetTabBarIsVisible(page, false);
        NavigationPage.SetHasNavigationBar(page, false);
        NavigationPage.SetHasBackButton(page, false);
    }

    static bool IsGamePage(Page page)
    {
        var name = page.GetType().Name;
        return name.EndsWith("GamePage", StringComparison.Ordinal)
               || string.Equals(name, "GamePlayPage", StringComparison.Ordinal);
    }

    static bool IsGamePageType(Type? pageType)
    {
        if (pageType is null)
        {
            return false;
        }

        var name = pageType.Name;
        return name.EndsWith("GamePage", StringComparison.Ordinal)
               || string.Equals(name, "GamePlayPage", StringComparison.Ordinal);
    }

    static bool ShouldShowLaunchCountdown(Type? pageType)
    {
        return IsGamePageType(pageType) && !HasBuiltInLaunchCountdown(pageType);
    }

    static bool ShouldShowLaunchCountdown(Page page)
    {
        return IsGamePage(page) && !HasBuiltInLaunchCountdown(page.GetType());
    }

    static bool HasBuiltInLaunchCountdown(Type? pageType)
    {
        if (pageType is null)
        {
            return false;
        }

        return pageType.Name switch
        {
            "MatchaMadnessGamePage" => true,
            "MovingMathGamePage" => true,
            "MustSortGamePage" => true,
            "PartialMatchGamePage" => true,
            "SpinCycleGamePage" => true,
            "SquareNumbersGamePage" => true,
            "TapTrapGamePage" => true,
            "TrueColorGamePage" => true,
            "TurtleTrafficGamePage" => true,
            "UniqueGamePage" => true,
            _ => false
        };
    }

    static bool TryCreateFallbackPage(string route, out Page page)
    {
        var normalized = route.Trim().TrimStart('/').Split('?', '#')[0];
        switch (normalized)
        {
            case nameof(StatsPage):
            case "stats":
                page = new StatsPage();
                return true;
            case nameof(OverTimePage):
                page = new OverTimePage();
                return true;
            case nameof(GamesStatsPage):
                page = new GamesStatsPage();
                return true;
            case nameof(LeaderboardsPage):
                page = new LeaderboardsPage();
                return true;
            case nameof(ComparePage):
                page = new ComparePage();
                return true;
            default:
                page = null!;
                return false;
        }
    }

    static async Task AnimatePageAsync(Page? page)
    {
        if (page is not ContentPage contentPage || contentPage.Content is not VisualElement root)
        {
            return;
        }

        root.AbortAnimation("FastPageIn");
        root.Opacity = 0;
        root.TranslationY = 14;
        root.Scale = 0.985;

        await Task.WhenAll(
            root.FadeTo(1, 150, Easing.CubicOut),
            root.TranslateTo(0, 0, 150, Easing.CubicOut),
            root.ScaleTo(1, 150, Easing.CubicOut));
    }
}
