namespace Peak;

public static class MustSortProgress
{
    public const int NoviceThreshold = 2600;
    public const int SkilledThreshold = 4300;
    public const int ExpertThreshold = 6100;
    public const int ExpectedTopScore = 7800;

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
            return "You've reached the top Must Sort rank.";
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
