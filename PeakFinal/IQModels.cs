namespace Peak;

public enum IQCategory
{
    LogicMath,
    Verbal,
    Abstract,
    Spatial,
    Science,
    PhilippineHistory
}

public enum IQDifficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}

public sealed record IQQuestion(
    string Id,
    IQCategory Category,
    IQDifficulty Difficulty,
    string Prompt,
    string? ImageSource,
    string[] Options,
    int CorrectIndex,
    string Explanation);

public sealed record IQTestDefinition(
    string Id,
    string Title,
    string Subtitle,
    string Description,
    string AccentColor,
    string GradientEndColor,
    string HeroImageSource,
    int QuestionCount,
    int QuestionsPerCategory,
    TimeSpan TimeLimit,
    string ActionLabel,
    IReadOnlyList<IQCategory> Categories);

public sealed class IQCategoryStats
{
    public int Correct { get; private set; }
    public int Answered { get; private set; }
    public int Total { get; private set; }
    public int EarnedPoints { get; private set; }
    public int PossiblePoints { get; private set; }
    public int TotalPossiblePoints { get; private set; }

    public void RegisterQuestion(int possiblePoints)
    {
        Total++;
        TotalPossiblePoints += possiblePoints;
    }

    public void RegisterAnswer(bool correct, int earnedPoints, int possiblePoints)
    {
        Answered++;
        if (correct)
        {
            Correct++;
        }

        EarnedPoints += earnedPoints;
        PossiblePoints += possiblePoints;
    }
}

public sealed record IQAnswerResult(
    bool IsCorrect,
    int ChosenIndex,
    int CorrectIndex,
    int EarnedPoints,
    int PossiblePoints,
    string Explanation);

public static class IQCatalog
{
    public static readonly IQTestDefinition GeneralChallenge = new(
        Id: "general_challenge",
        Title: "General IQ Challenge",
        Subtitle: "A balanced mix of logic, language, science, visual puzzles, and Philippine history.",
        Description: "18 randomized questions with fair weighted scoring. Expect a fast but friendly five-minute challenge.",
        AccentColor: "#315EF8",
        GradientEndColor: "#14A3D9",
        HeroImageSource: "iq_test_logo.svg",
        QuestionCount: 18,
        QuestionsPerCategory: 3,
        TimeLimit: TimeSpan.FromMinutes(5),
        ActionLabel: "Start 5-minute test",
        Categories: new[]
        {
            IQCategory.LogicMath,
            IQCategory.Verbal,
            IQCategory.Abstract,
            IQCategory.Spatial,
            IQCategory.Science,
            IQCategory.PhilippineHistory
        });

    public static readonly IQTestDefinition QuickCheck = new(
        Id: "quick_check",
        Title: "Quick Brain Check",
        Subtitle: "A shorter IQ sampler when you want a quick pulse check.",
        Description: "12 randomized questions in 3 minutes. Great for daily practice without the full test length.",
        AccentColor: "#0E7490",
        GradientEndColor: "#2DD4BF",
        HeroImageSource: "quick_brain_check_logo.svg",
        QuestionCount: 12,
        QuestionsPerCategory: 2,
        TimeLimit: TimeSpan.FromMinutes(3),
        ActionLabel: "Start 3-minute test",
        Categories: new[]
        {
            IQCategory.LogicMath,
            IQCategory.Verbal,
            IQCategory.Abstract,
            IQCategory.Spatial,
            IQCategory.Science,
            IQCategory.PhilippineHistory
        });

    public static IReadOnlyList<IQTestDefinition> All => new[]
    {
        GeneralChallenge,
        QuickCheck
    };
}

public static class IQDisplay
{
    public static string GetCategoryLabel(IQCategory category) => category switch
    {
        IQCategory.LogicMath => "Math & Logic",
        IQCategory.Verbal => "Language",
        IQCategory.Abstract => "Patterns",
        IQCategory.Spatial => "Visual",
        IQCategory.Science => "Science",
        IQCategory.PhilippineHistory => "PH History",
        _ => category.ToString()
    };

    public static string GetCategoryColor(IQCategory category) => category switch
    {
        IQCategory.LogicMath => "#2563EB",
        IQCategory.Verbal => "#7C3AED",
        IQCategory.Abstract => "#DB2777",
        IQCategory.Spatial => "#EA580C",
        IQCategory.Science => "#0F766E",
        IQCategory.PhilippineHistory => "#334155",
        _ => "#334155"
    };

    public static string GetDifficultyLabel(IQDifficulty difficulty) => difficulty switch
    {
        IQDifficulty.Easy => "Easy",
        IQDifficulty.Medium => "Medium",
        IQDifficulty.Hard => "Hard",
        _ => "Mixed"
    };

    public static string GetDifficultyColor(IQDifficulty difficulty) => difficulty switch
    {
        IQDifficulty.Easy => "#16A34A",
        IQDifficulty.Medium => "#2563EB",
        IQDifficulty.Hard => "#C2410C",
        _ => "#475569"
    };

    public static int GetPointValue(IQDifficulty difficulty) => difficulty switch
    {
        IQDifficulty.Easy => 80,
        IQDifficulty.Medium => 120,
        IQDifficulty.Hard => 170,
        _ => 100
    };
}
