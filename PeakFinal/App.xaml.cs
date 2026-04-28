namespace Peak
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            BrainScoreService.PurgeLegacyLocalScoreStorage();
            SetRootPage(CreateInitialPage());

            if (LocalAccountStore.IsSignedIn)
            {
                _ = RefreshSignedInCloudStateAsync();
            }
        }

        public static Page CreateInitialPage()
        {
            if (LocalAccountStore.IsSignedIn)
            {
                return new AppShell();
            }

            return CreateSignedOutRoot();
        }

        public static NavigationPage CreateSignedOutRoot()
        {
            Page entryPage = LocalAccountStore.HasAccount
                ? new AccountAccessPage()
                : new OnboardingPage();

            NavigationPage.SetHasNavigationBar(entryPage, false);
            return new NavigationPage(entryPage);
        }

        public static void ShowSignedInExperience()
        {
            if (Current is null)
            {
                return;
            }

            SetRootPage(new AppShell());
        }

        public static void ShowSignedOutExperience()
        {
            if (Current is null)
            {
                return;
            }

            SetRootPage(CreateSignedOutRoot());
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            CompareBenchmarkService.ClearActiveComparison();
        }

        protected override void OnResume()
        {
            base.OnResume();

            if (LocalAccountStore.IsSignedIn)
            {
                _ = RefreshSignedInCloudStateAsync();
            }
        }

        public static async Task<OnlineSyncResult> RefreshSignedInCloudStateAsync(bool isNewAccount = false)
        {
            if (!LocalAccountStore.IsSignedIn)
            {
                return new OnlineSyncResult(false, "No signed-in account found for cloud sync.");
            }

            try
            {
                if (isNewAccount)
                {
                    BrainScoreService.InitializeEmptyCurrentUserHistory();
                }
                else
                {
                    var historyResult = await BrainScoreService.RefreshCurrentUserFromDatabaseAsync();
                    if (!historyResult.IsSuccess && !BrainScoreService.HasResolvedCurrentUserHistory)
                    {
                        await AchievementsService.RefreshCurrentUserAsync(syncLocalProgress: false);
                        return historyResult;
                    }
                }

                var scoreSyncResult = await PlayerLeaderboardService.SyncCurrentUserFullWithResultAsync();
                await AchievementsService.RefreshCurrentUserAsync(syncLocalProgress: BrainScoreService.HasResolvedCurrentUserHistory);
                return scoreSyncResult;
            }
            catch (Exception ex)
            {
                return new OnlineSyncResult(false, $"Signed-in sync did not complete. {ex.Message}");
            }
        }

        static void SetRootPage(Page page)
        {
            if (Current is null)
            {
                return;
            }

            void ApplyRoot()
            {
                var window = Current.Windows.FirstOrDefault();
                if (window is not null)
                {
                    window.Page = page;
                    return;
                }

                Current.MainPage = page;
            }

            if (MainThread.IsMainThread)
            {
                ApplyRoot();
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(ApplyRoot);
            }
        }
    }
}
