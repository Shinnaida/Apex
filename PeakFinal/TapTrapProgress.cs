namespace Peak;

public static class TapTrapProgress
{
    public const int NoviceThreshold = 1720;
    public const int SkilledThreshold = 3380;
    public const int ExpertThreshold = 4860;
    public const int ExpectedTopScore = 6400;

    public static string ResolveRank(int bestScore)
    {
        if (bestScore >= ExpertThreshold)
        {
            return "Expert";
        }

        if (bestScore >= SkilledThreshold)
        {
            return "Skilled";
        }

        if (bestScore >= NoviceThreshold)
        {
            return "Novice";
        }

        return "Beginner";
    }

    public static string ResolveChallenge(int bestScore)
    {
        if (bestScore >= ExpertThreshold)
        {
            return "You've reached the top Tap Trap rank.";
        }

        if (bestScore >= SkilledThreshold)
        {
            return $"Score above {ExpertThreshold} to rank up to Expert";
        }

        if (bestScore >= NoviceThreshold)
        {
            return $"Score above {SkilledThreshold} to rank up to Skilled";
        }

        return $"Score above {NoviceThreshold} to rank up to Novice";
    }
}
