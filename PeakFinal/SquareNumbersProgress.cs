namespace Peak;

public static class SquareNumbersProgress
{
    public const int NoviceThreshold = 8860;
    public const int SkilledThreshold = 11200;
    public const int ExpertThreshold = 13600;
    public const int ExpectedTopScore = 18000;

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
            return "You've reached the top Square Numbers rank.";
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

    public static string ResolveRankUpLabel(int previousBest, int finalScore)
    {
        var previousRank = ResolveRank(previousBest);
        var currentRank = ResolveRank(finalScore);

        if (!string.Equals(previousRank, currentRank, StringComparison.Ordinal))
        {
            return $"Rank up {currentRank}";
        }

        return $"Rank {currentRank}";
    }
}
