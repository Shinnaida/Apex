namespace Peak;

public static class TrueColorProgress
{
    public const int NoviceThreshold = 1570;
    public const int SkilledThreshold = 3050;
    public const int ExpertThreshold = 4720;
    public const int ExpectedTopScore = 6200;

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
            return "You've reached the top True Color rank.";
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
