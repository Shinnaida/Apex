namespace Peak;

public static class GameEntryNavigationService
{
    public static Task OpenByTitleAsync(INavigation navigation, string title)
    {
        if (navigation is null || string.IsNullOrWhiteSpace(title))
        {
            return Task.CompletedTask;
        }

        GameLaunchContextService.CaptureFromPage(navigation.NavigationStack.LastOrDefault());

        return PageTransitionService.PushAsync(navigation, () => CreatePage(title.Trim()));
    }

    public static ContentPage? CreatePage(string title)
    {
        return title switch
        {
            "Word Fresh" => new WordFreshPage(),
            "Word-A-Like" => new WordALikePage(),
            "Babble Bots" => new BabbleBotsPage(),
            "Word Hunt" => new WordHuntPage(),
            "Grow" => new GrowPage(),
            "Perilous Path" => new PerilousPathPage(),
            "Partial Match" => new PartialMatchPage(),
            "Spin Cycle" => new SpinCyclePage(),
            "Memory Match" => new MemoryGamePage(),
            "Baggage Claim" => ArcadeChallengeFactory.Create("Baggage Claim"),
            "Matcha Madness" => new MatchaMadnessPage(),
            "Moving Math" => new MovingMathPage(),
            "Square Numbers" => new SquareNumbersPage(),
            "Pixel Logic" => new PixelLogicPage(),
            "Low Pop" => ArcadeChallengeFactory.Create("Low Pop"),
            "Decoder" => new DecoderPage(),
            "Must Sort" => new MustSortPage(),
            "Tap Trap" => new TapTrapPage(),
            "True Color" => new TrueColorPage(),
            "Unique" => new UniquePage(),
            "Turtle Traffic" => new TurtleTrafficPage(),
            "Face Switch" => ArcadeChallengeFactory.Create("Face Switch"),
            "Speed Spotting" => ArcadeChallengeFactory.Create("Speed Spotting"),
            "Smile On Me" => new SmileOnMePage(),
            "Face To Face" => ArcadeChallengeFactory.Create("Face To Face"),
            "Mood Match" => ArcadeChallengeFactory.Create("Mood Match"),
            _ => null
        };
    }
}
