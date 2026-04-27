namespace Peak;

public class IQSession
{
    readonly HashSet<int> _answeredIndexes = new();

    public IQTestDefinition Definition { get; }
    public IReadOnlyList<IQQuestion> Questions { get; }
    public int Index { get; private set; }
    public int CorrectCount { get; private set; }
    public int CurrentScore { get; private set; }
    public int AnsweredCount => _answeredIndexes.Count;
    public int MaximumPossibleScore { get; }
    public bool IsCompleted { get; private set; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset EndsAtUtc { get; }
    public Dictionary<IQCategory, IQCategoryStats> Stats { get; } = new();

    public IQQuestion Current => Questions[Index];
    public bool IsLast => Index >= Questions.Count - 1;
    public TimeSpan RemainingTime => EndsAtUtc <= DateTimeOffset.UtcNow
        ? TimeSpan.Zero
        : EndsAtUtc - DateTimeOffset.UtcNow;

    public TimeSpan ElapsedTime
    {
        get
        {
            var elapsed = DateTimeOffset.UtcNow - StartedAtUtc;
            var maxElapsed = Definition.TimeLimit;
            return elapsed < TimeSpan.Zero
                ? TimeSpan.Zero
                : elapsed > maxElapsed
                    ? maxElapsed
                    : elapsed;
        }
    }

    public IQSession(IEnumerable<IQQuestion> sourceQuestions, int count = 10)
        : this(
            IQCatalog.GeneralChallenge,
            sourceQuestions.Take(Math.Max(1, count)).ToList())
    {
    }

    IQSession(IQTestDefinition definition, IReadOnlyList<IQQuestion> questions)
    {
        Definition = definition;
        Questions = questions;
        MaximumPossibleScore = Questions.Sum(question => IQDisplay.GetPointValue(question.Difficulty));
        StartedAtUtc = DateTimeOffset.UtcNow;
        EndsAtUtc = StartedAtUtc.Add(definition.TimeLimit);

        foreach (var question in Questions)
        {
            if (!Stats.TryGetValue(question.Category, out var stats))
            {
                stats = new IQCategoryStats();
                Stats[question.Category] = stats;
            }

            stats.RegisterQuestion(IQDisplay.GetPointValue(question.Difficulty));
        }
    }

    public static IQSession Create(IQTestDefinition definition)
    {
        var rng = new Random();
        var questions = IQQuestionBank.BuildSessionQuestions(definition, rng);
        return new IQSession(definition, questions);
    }

    public IQAnswerResult SubmitAnswer(int chosenIndex)
    {
        if (IsCompleted || _answeredIndexes.Contains(Index))
        {
            var current = Current;
            var possible = IQDisplay.GetPointValue(current.Difficulty);
            return new IQAnswerResult(
                IsCorrect: chosenIndex == current.CorrectIndex,
                ChosenIndex: chosenIndex,
                CorrectIndex: current.CorrectIndex,
                EarnedPoints: 0,
                PossiblePoints: possible,
                Explanation: current.Explanation);
        }

        var question = Current;
        var possiblePoints = IQDisplay.GetPointValue(question.Difficulty);
        var isCorrect = chosenIndex == question.CorrectIndex;
        var earnedPoints = isCorrect ? possiblePoints : 0;

        _answeredIndexes.Add(Index);

        if (isCorrect)
        {
            CorrectCount++;
            CurrentScore += earnedPoints;
        }

        Stats[question.Category].RegisterAnswer(isCorrect, earnedPoints, possiblePoints);

        return new IQAnswerResult(
            IsCorrect: isCorrect,
            ChosenIndex: chosenIndex,
            CorrectIndex: question.CorrectIndex,
            EarnedPoints: earnedPoints,
            PossiblePoints: possiblePoints,
            Explanation: question.Explanation);
    }

    public void CompleteByTimeout()
    {
        IsCompleted = true;
    }

    public void Next()
    {
        if (IsLast)
        {
            IsCompleted = true;
            return;
        }

        Index++;
    }

    public double GetOverallNormalized()
    {
        if (MaximumPossibleScore <= 0)
        {
            return 0;
        }

        return Math.Clamp(CurrentScore / (double)MaximumPossibleScore, 0, 1);
    }

    public int GetPeakScore()
    {
        return (int)Math.Round(GetOverallNormalized() * 1000);
    }

    public double GetCategoryNormalized(IQCategory category)
    {
        if (!Stats.TryGetValue(category, out var stats) || stats.TotalPossiblePoints <= 0)
        {
            return 0;
        }

        return Math.Clamp(stats.EarnedPoints / (double)stats.TotalPossiblePoints, 0, 1);
    }

    public int GetCategoryDisplayScore(IQCategory category)
    {
        return 100 + (int)Math.Round(GetCategoryNormalized(category) * 100);
    }

    public double GetMemoryNormalized()
    {
        return AverageCategories(IQCategory.Spatial, IQCategory.PhilippineHistory);
    }

    public double GetProblemSolvingNormalized()
    {
        return AverageCategories(IQCategory.LogicMath, IQCategory.Science);
    }

    public double GetLanguageNormalized()
    {
        return GetCategoryNormalized(IQCategory.Verbal);
    }

    public double GetFocusNormalized()
    {
        return GetCategoryNormalized(IQCategory.Abstract);
    }

    double AverageCategories(params IQCategory[] categories)
    {
        var values = categories
            .Select(GetCategoryNormalized)
            .ToList();

        if (values.Count == 0)
        {
            return 0;
        }

        return values.Average();
    }
}
