namespace Peak;

public static class UniqueProgress
{
    public const int NoviceThreshold = 1860;
    public const int SkilledThreshold = 3420;
    public const int ExpertThreshold = 5120;
    public const int ExpectedTopScore = 6800;

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
            return "You've reached the top Unique rank.";
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
