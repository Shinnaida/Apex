namespace Peak;

public static class GamePointsService
{
    const string BalancePrefix = "apex_points_balance_v1_";
    const string LifetimeEarnedPrefix = "apex_points_earned_v1_";
    const int BaseGameplayPoints = 10;
    const int MaxScaledGameplayPoints = 35;
    const int NearPerfectBonus = 10;

    public static int GetBalance() => Preferences.Default.Get(GetBalanceKey(), 0);

    public static int GetLifetimeEarned() => Preferences.Default.Get(GetLifetimeEarnedKey(), 0);

    public static int AwardGameplayPoints(string sourceId, int rawScore, int expectedTopScore)
    {
        if (rawScore <= 0 || expectedTopScore <= 0)
        {
            return 0;
        }

        var normalized = Math.Clamp(rawScore / (double)expectedTopScore, 0, 1);
        var earned = (int)Math.Round(BaseGameplayPoints + (normalized * MaxScaledGameplayPoints));
        if (normalized >= 0.95)
        {
            earned += NearPerfectBonus;
        }

        AddPoints(earned);
        return earned;
    }

    public static void AddPoints(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Preferences.Default.Set(GetBalanceKey(), GetBalance() + amount);
        Preferences.Default.Set(GetLifetimeEarnedKey(), GetLifetimeEarned() + amount);
    }

    public static bool TrySpendPoints(int amount, out string message)
    {
        if (amount <= 0)
        {
            message = "Invalid unlock value.";
            return false;
        }

        var balance = GetBalance();
        if (balance < amount)
        {
            message = $"You need {amount:N0} points to unlock this game. Current balance: {balance:N0}.";
            return false;
        }

        Preferences.Default.Set(GetBalanceKey(), balance - amount);
        message = $"Spent {amount:N0} points.";
        return true;
    }

    static string GetBalanceKey() => $"{BalancePrefix}{GetUserKey()}";
    static string GetLifetimeEarnedKey() => $"{LifetimeEarnedPrefix}{GetUserKey()}";

    static string GetUserKey()
    {
        var username = LocalAccountStore.GetLastActiveUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            return "guest";
        }

        return new string(username.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
    }
}
