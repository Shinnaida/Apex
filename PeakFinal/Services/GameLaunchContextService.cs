namespace Peak;

public enum GameLaunchOrigin
{
    Unknown,
    Today,
    Games,
    Tests,
    Stats,
    Me
}

public static class GameLaunchContextService
{
    static GameLaunchOrigin _currentOrigin;

    public static GameLaunchOrigin CurrentOrigin => _currentOrigin;

    public static void CaptureFromPage(Page? page)
    {
        var origin = ResolveOrigin(page);
        if (origin != GameLaunchOrigin.Unknown)
        {
            _currentOrigin = origin;
        }
    }

    public static void Clear()
    {
        _currentOrigin = GameLaunchOrigin.Unknown;
    }

    public static string? GetReturnRoute()
    {
        return _currentOrigin switch
        {
            GameLaunchOrigin.Today => "//today",
            GameLaunchOrigin.Games => "//games",
            GameLaunchOrigin.Tests => "//tests",
            GameLaunchOrigin.Stats => "//stats",
            GameLaunchOrigin.Me => "//me",
            _ => null
        };
    }

    static GameLaunchOrigin ResolveOrigin(Page? page)
    {
        return page switch
        {
            TodayPage => GameLaunchOrigin.Today,
            AllGamesPage => GameLaunchOrigin.Games,
            SkillGamesPage => GameLaunchOrigin.Games,
            TestsPage => GameLaunchOrigin.Tests,
            TestYourselfPage => GameLaunchOrigin.Tests,
            IQGamePage => GameLaunchOrigin.Tests,
            IQResultsPage => GameLaunchOrigin.Tests,
            StatsPage => GameLaunchOrigin.Stats,
            GamesStatsPage => GameLaunchOrigin.Stats,
            OverTimePage => GameLaunchOrigin.Stats,
            LeaderboardsPage => GameLaunchOrigin.Stats,
            GamePerformanceDetailPage => _currentOrigin,
            MePage => GameLaunchOrigin.Me,
            _ => GameLaunchOrigin.Unknown
        };
    }
}
