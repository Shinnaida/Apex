using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Peak;

public sealed record LocalAccountProfile(string Username, int Age);
public sealed record LocalAvatarProfile(string Mode, string Value);

public static class LocalAccountStore
{
    public const string AvatarModeEmoji = "emoji";
    public const string AvatarModePhoto = "photo";
    public const string DefaultAvatarEmoji = "\U0001FAE3";

    private const string HasAccountKey = "account_created";
    private const string IsSignedInKey = "account_signed_in";
    private const string CurrentUserKey = "account_current_user";

    private const string AccountsKey = "accounts_usernames";
    private const string UserDisplayNamePrefix = "account_username_";
    private const string UserAgePrefix = "account_age_";
    private const string UserPasswordHashPrefix = "account_password_hash_";
    private const string BiometricEnabledPrefix = "account_biometric_enabled_";

    private const string LegacyUsernameKey = "account_username";
    private const string LegacyAgeKey = "account_age";
    private const string LegacyPasswordHashKey = "account_password_hash";

    private const string AvatarModePrefix = "account_avatar_mode_";
    private const string AvatarValuePrefix = "account_avatar_value_";

    public static bool HasAccount
    {
        get
        {
            EnsureMigratedLegacyAccount();
            return GetKnownUsers().Count > 0;
        }
    }

    public static bool IsSignedIn
    {
        get
        {
            EnsureMigratedLegacyAccount();
            return Preferences.Get(IsSignedInKey, false);
        }
    }

    public static bool TryValidateRegistration(
        string username,
        string ageText,
        string password,
        string confirmPassword,
        out int age,
        out string error)
    {
        EnsureMigratedLegacyAccount();
        age = 0;

        var displayUsername = username.Trim();
        if (displayUsername.Length < 3)
        {
            error = "Username must be at least 3 characters.";
            return false;
        }

        var usernameKey = NormalizeUsername(displayUsername);
        if (GetKnownUsers().Contains(usernameKey))
        {
            error = "That username already exists. Try a different one.";
            return false;
        }

        if (!int.TryParse(ageText.Trim(), out age) || age is < 8 or > 99)
        {
            error = "Enter a valid age between 8 and 99.";
            return false;
        }

        if (password.Length < 6 || !password.Any(char.IsDigit))
        {
            error = "Password must be at least 6 characters and include a number.";
            return false;
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            error = "Passwords do not match.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static void SaveAccount(string username, int age, string password)
    {
        EnsureMigratedLegacyAccount();

        var displayUsername = username.Trim();
        var usernameKey = NormalizeUsername(displayUsername);
        var users = GetKnownUsers();
        if (!users.Contains(usernameKey))
        {
            users.Add(usernameKey);
            SaveKnownUsers(users);
        }

        Preferences.Set(GetUserDisplayNameKey(usernameKey), displayUsername);
        Preferences.Set(GetUserAgeKey(usernameKey), age);
        Preferences.Set(GetUserPasswordHashKey(usernameKey), ComputeHash(password));

        Preferences.Set(CurrentUserKey, usernameKey);
        Preferences.Set(IsSignedInKey, true);
        Preferences.Set(HasAccountKey, true);
        BrainScoreService.InitializeEmptyCurrentUserHistory();
    }

    public static bool TryGetProfile(out LocalAccountProfile profile)
    {
        EnsureMigratedLegacyAccount();
        profile = new LocalAccountProfile(string.Empty, 0);

        if (!IsSignedIn)
        {
            return false;
        }

        var currentUserKey = NormalizeUsername(Preferences.Get(CurrentUserKey, string.Empty));
        if (string.IsNullOrWhiteSpace(currentUserKey))
        {
            return false;
        }

        var users = GetKnownUsers();
        if (!users.Contains(currentUserKey))
        {
            Preferences.Set(IsSignedInKey, false);
            return false;
        }

        var displayName = Preferences.Get(GetUserDisplayNameKey(currentUserKey), string.Empty);
        var age = Preferences.Get(GetUserAgeKey(currentUserKey), 0);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = currentUserKey;
        }

        profile = new LocalAccountProfile(displayName, age);
        return true;
    }

    public static bool TrySignIn(string username, string password, out string error)
    {
        EnsureMigratedLegacyAccount();

        var inputUsername = username.Trim();
        var usernameKey = NormalizeUsername(inputUsername);
        if (!GetKnownUsers().Contains(usernameKey))
        {
            error = "Username not found.";
            return false;
        }

        var storedHash = Preferences.Get(GetUserPasswordHashKey(usernameKey), string.Empty);
        if (storedHash.Length == 0 || !string.Equals(storedHash, ComputeHash(password), StringComparison.Ordinal))
        {
            error = "Incorrect password.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(inputUsername))
        {
            Preferences.Set(GetUserDisplayNameKey(usernameKey), inputUsername);
        }

        Preferences.Set(CurrentUserKey, usernameKey);
        Preferences.Set(IsSignedInKey, true);
        error = string.Empty;
        return true;
    }

    public static void SignOut()
    {
        Preferences.Set(IsSignedInKey, false);
    }

    public static void SetBiometricEnabled(string username, bool enabled)
    {
        EnsureMigratedLegacyAccount();

        var usernameKey = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(usernameKey))
        {
            return;
        }

        if (!GetKnownUsers().Contains(usernameKey))
        {
            return;
        }

        Preferences.Set(GetBiometricEnabledKey(usernameKey), enabled);
    }

    public static bool IsBiometricEnabled(string username)
    {
        EnsureMigratedLegacyAccount();

        var usernameKey = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(usernameKey))
        {
            return false;
        }

        return Preferences.Get(GetBiometricEnabledKey(usernameKey), false);
    }

    public static bool HasBiometricEnabledAccounts()
    {
        EnsureMigratedLegacyAccount();
        var users = GetKnownUsers();
        return users.Any(user => Preferences.Get(GetBiometricEnabledKey(user), false));
    }

    public static IReadOnlyList<string> GetBiometricEnabledAccounts()
    {
        EnsureMigratedLegacyAccount();

        var result = new List<string>();
        foreach (var user in GetKnownUsers())
        {
            if (!Preferences.Get(GetBiometricEnabledKey(user), false))
            {
                continue;
            }

            var displayName = Preferences.Get(GetUserDisplayNameKey(user), string.Empty);
            result.Add(string.IsNullOrWhiteSpace(displayName) ? user : displayName);
        }

        return result;
    }

    public static IReadOnlyList<string> GetStoredUsernameKeys()
    {
        EnsureMigratedLegacyAccount();
        return GetKnownUsers().ToList();
    }

    public static string GetLastActiveUsername()
    {
        EnsureMigratedLegacyAccount();

        var currentUserKey = NormalizeUsername(Preferences.Get(CurrentUserKey, string.Empty));
        if (string.IsNullOrWhiteSpace(currentUserKey))
        {
            return string.Empty;
        }

        var displayName = Preferences.Get(GetUserDisplayNameKey(currentUserKey), string.Empty);
        return string.IsNullOrWhiteSpace(displayName) ? currentUserKey : displayName;
    }

    public static bool TrySignInWithBiometric(string username, out string error)
    {
        EnsureMigratedLegacyAccount();

        var inputUsername = username.Trim();
        var usernameKey = NormalizeUsername(inputUsername);
        if (!GetKnownUsers().Contains(usernameKey))
        {
            error = "Username not found.";
            return false;
        }

        if (!Preferences.Get(GetBiometricEnabledKey(usernameKey), false))
        {
            error = "Fingerprint login is not enabled for this account.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(inputUsername))
        {
            Preferences.Set(GetUserDisplayNameKey(usernameKey), inputUsername);
        }

        Preferences.Set(CurrentUserKey, usernameKey);
        Preferences.Set(IsSignedInKey, true);
        error = string.Empty;
        return true;
    }

    public static void SaveAvatarEmoji(string username, string emoji)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(emoji))
        {
            return;
        }

        Preferences.Set(GetAvatarModeKey(username), AvatarModeEmoji);
        Preferences.Set(GetAvatarValueKey(username), emoji.Trim());
    }

    public static void SaveAvatarPhotoPath(string username, string localPath)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        Preferences.Set(GetAvatarModeKey(username), AvatarModePhoto);
        Preferences.Set(GetAvatarValueKey(username), localPath);
    }

    public static bool TryGetAvatar(string username, out LocalAvatarProfile avatar)
    {
        avatar = new LocalAvatarProfile(AvatarModeEmoji, DefaultAvatarEmoji);

        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var mode = Preferences.Get(GetAvatarModeKey(username), string.Empty);
        var value = Preferences.Get(GetAvatarValueKey(username), string.Empty);
        if (string.IsNullOrWhiteSpace(mode) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        avatar = new LocalAvatarProfile(mode, value);
        return true;
    }

    private static void EnsureMigratedLegacyAccount()
    {
        var users = GetKnownUsers();
        if (users.Count > 0)
        {
            return;
        }

        if (!Preferences.Get(HasAccountKey, false))
        {
            return;
        }

        var legacyUsername = Preferences.Get(LegacyUsernameKey, string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(legacyUsername))
        {
            return;
        }

        var legacyAge = Preferences.Get(LegacyAgeKey, 0);
        var legacyPasswordHash = Preferences.Get(LegacyPasswordHashKey, string.Empty);
        var legacyUserKey = NormalizeUsername(legacyUsername);

        Preferences.Set(GetUserDisplayNameKey(legacyUserKey), legacyUsername);
        Preferences.Set(GetUserAgeKey(legacyUserKey), legacyAge);
        if (!string.IsNullOrWhiteSpace(legacyPasswordHash))
        {
            Preferences.Set(GetUserPasswordHashKey(legacyUserKey), legacyPasswordHash);
        }

        SaveKnownUsers(new List<string> { legacyUserKey });

        if (!Preferences.ContainsKey(CurrentUserKey))
        {
            Preferences.Set(CurrentUserKey, legacyUserKey);
        }

        if (!Preferences.ContainsKey(IsSignedInKey))
        {
            Preferences.Set(IsSignedInKey, true);
        }
    }

    private static List<string> GetKnownUsers()
    {
        var json = Preferences.Get(AccountsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return parsed
                .Select(NormalizeUsername)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void SaveKnownUsers(IReadOnlyCollection<string> users)
    {
        var normalized = users
            .Select(NormalizeUsername)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Preferences.Set(AccountsKey, JsonSerializer.Serialize(normalized));
        Preferences.Set(HasAccountKey, normalized.Count > 0);
    }

    private static string ComputeHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string GetUserDisplayNameKey(string usernameKey)
        => $"{UserDisplayNamePrefix}{NormalizeUsername(usernameKey)}";

    private static string GetUserAgeKey(string usernameKey)
        => $"{UserAgePrefix}{NormalizeUsername(usernameKey)}";

    private static string GetUserPasswordHashKey(string usernameKey)
        => $"{UserPasswordHashPrefix}{NormalizeUsername(usernameKey)}";

    private static string GetAvatarModeKey(string username)
        => $"{AvatarModePrefix}{NormalizeUsername(username)}";

    private static string GetAvatarValueKey(string username)
        => $"{AvatarValuePrefix}{NormalizeUsername(username)}";

    private static string GetBiometricEnabledKey(string username)
        => $"{BiometricEnabledPrefix}{NormalizeUsername(username)}";

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }
}
