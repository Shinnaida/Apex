namespace Peak
{
    public partial class AppShell : Shell
    {
        private readonly Dictionary<ShellContent, string> _tabTitles = new();

        public AppShell()
        {
            InitializeComponent();
            PageTransitionService.AttachShell(this);
            CacheTabTitles();
            Navigated += OnShellNavigated;

            Routing.RegisterRoute(nameof(OverTimePage), typeof(OverTimePage));
            Routing.RegisterRoute(nameof(ComparePage), typeof(ComparePage));
            Routing.RegisterRoute(nameof(StatsPage), typeof(StatsPage));
            Routing.RegisterRoute(nameof(PercentilePage), typeof(PercentilePage));
            Routing.RegisterRoute(nameof(GamesStatsPage), typeof(GamesStatsPage));
            Routing.RegisterRoute(nameof(LeaderboardsPage), typeof(LeaderboardsPage));
            Routing.RegisterRoute(nameof(AllGamesPage), typeof(AllGamesPage));
            Routing.RegisterRoute(nameof(OnboardingPage), typeof(OnboardingPage));
            Routing.RegisterRoute(nameof(AccountAccessPage), typeof(AccountAccessPage));
            Routing.RegisterRoute(nameof(ValidateEmailPage), typeof(ValidateEmailPage));
            Routing.RegisterRoute(nameof(VerifyCodePage), typeof(VerifyCodePage));
            Routing.RegisterRoute(nameof(WordALikePage), typeof(WordALikePage));
            Routing.RegisterRoute(nameof(WordFreshPage), typeof(WordFreshPage));
            Routing.RegisterRoute(nameof(WordHuntPage), typeof(WordHuntPage));
            Routing.RegisterRoute(nameof(WordHuntGamePage), typeof(WordHuntGamePage));
            Routing.RegisterRoute(nameof(GrowPage), typeof(GrowPage));
            Routing.RegisterRoute(nameof(GrowGamePage), typeof(GrowGamePage));
            Routing.RegisterRoute(nameof(BabbleBotsPage), typeof(BabbleBotsPage));
            Routing.RegisterRoute(nameof(BabbleBotsGamePage), typeof(BabbleBotsGamePage));
            Routing.RegisterRoute(nameof(PerilousPathPage), typeof(PerilousPathPage));
            Routing.RegisterRoute(nameof(PartialMatchPage), typeof(PartialMatchPage));
            Routing.RegisterRoute(nameof(MatchaMadnessPage), typeof(MatchaMadnessPage));
            Routing.RegisterRoute(nameof(MatchaMadnessGamePage), typeof(MatchaMadnessGamePage));
            Routing.RegisterRoute(nameof(MatchaMadnessInsightsPage), typeof(MatchaMadnessInsightsPage));
            Routing.RegisterRoute(nameof(SquareNumbersPage), typeof(SquareNumbersPage));
            Routing.RegisterRoute(nameof(SquareNumbersGamePage), typeof(SquareNumbersGamePage));
            Routing.RegisterRoute(nameof(MovingMathPage), typeof(MovingMathPage));
            Routing.RegisterRoute(nameof(MovingMathGamePage), typeof(MovingMathGamePage));
            Routing.RegisterRoute(nameof(MovingMathInsightsPage), typeof(MovingMathInsightsPage));
            Routing.RegisterRoute(nameof(MustSortPage), typeof(MustSortPage));
            Routing.RegisterRoute(nameof(MustSortGamePage), typeof(MustSortGamePage));
            Routing.RegisterRoute(nameof(MustSortInsightsPage), typeof(MustSortInsightsPage));
            Routing.RegisterRoute(nameof(TapTrapPage), typeof(TapTrapPage));
            Routing.RegisterRoute(nameof(TapTrapGamePage), typeof(TapTrapGamePage));
            Routing.RegisterRoute(nameof(TapTrapInsightsPage), typeof(TapTrapInsightsPage));
            Routing.RegisterRoute(nameof(DecoderPage), typeof(DecoderPage));
            Routing.RegisterRoute(nameof(DecoderGamePage), typeof(DecoderGamePage));
            Routing.RegisterRoute(nameof(UniquePage), typeof(UniquePage));
            Routing.RegisterRoute(nameof(UniqueGamePage), typeof(UniqueGamePage));
            Routing.RegisterRoute(nameof(UniqueInsightsPage), typeof(UniqueInsightsPage));
            Routing.RegisterRoute(nameof(TrueColorPage), typeof(TrueColorPage));
            Routing.RegisterRoute(nameof(TrueColorGamePage), typeof(TrueColorGamePage));
            Routing.RegisterRoute(nameof(TrueColorInsightsPage), typeof(TrueColorInsightsPage));
            Routing.RegisterRoute(nameof(SpinCyclePage), typeof(SpinCyclePage));
            Routing.RegisterRoute(nameof(SpinCycleGamePage), typeof(SpinCycleGamePage));
            Routing.RegisterRoute(nameof(SpinCycleInsightsPage), typeof(SpinCycleInsightsPage));
            Routing.RegisterRoute(nameof(PartialMatchGamePage), typeof(PartialMatchGamePage));
            Routing.RegisterRoute(nameof(PartialMatchInsightsPage), typeof(PartialMatchInsightsPage));
            Routing.RegisterRoute(nameof(AchievementsPage), typeof(AchievementsPage));

            UpdateTabTitles();
        }

        private void CacheTabTitles()
        {
            _tabTitles[TodayTab] = "Today";
            _tabTitles[GamesTab] = "All Games";
            _tabTitles[StatsTab] = "Stats";
            _tabTitles[TestsTab] = "Tests";
            _tabTitles[MeTab] = "Me";
        }

        private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            UpdateTabTitles();
        }

        private void UpdateTabTitles()
        {
            foreach (var entry in _tabTitles)
            {
                entry.Key.Title = IsCurrentTab(entry.Key)
                    ? entry.Value
                    : " ";
            }
        }

        private bool IsCurrentTab(ShellContent content)
        {
            return ReferenceEquals(CurrentItem?.CurrentItem?.CurrentItem, content);
        }
    }
}
