namespace Peak;

public sealed record PendingIqSummary(
    string TestTitle,
    int PeakScore,
    int CorrectCount,
    int QuestionCount,
    double Memory,
    double ProblemSolving,
    double Language,
    double Focus);

public static class PendingIqSummaryService
{
    static PendingIqSummary? _pendingSummary;

    public static bool HasPendingSummary => _pendingSummary is not null;

    public static void Store(PendingIqSummary summary)
    {
        _pendingSummary = summary;
    }

    public static bool TryPeek(out PendingIqSummary summary)
    {
        if (_pendingSummary is null)
        {
            summary = default!;
            return false;
        }

        summary = _pendingSummary;
        return true;
    }

    public static bool TryTake(out PendingIqSummary summary)
    {
        if (_pendingSummary is null)
        {
            summary = default!;
            return false;
        }

        summary = _pendingSummary;
        _pendingSummary = null;
        return true;
    }

    public static void Clear()
    {
        _pendingSummary = null;
    }
}
