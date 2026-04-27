namespace Peak;

internal static class AvatarImageSyncHelper
{
    private const string EmojiAvatarPrefix = "emoji:";
    private const string AvatarBucketName = "avatars";

    public static bool TryGetAvatarProfile(string? usernameKey, string? displayName, out LocalAvatarProfile avatar)
    {
        var candidates = new[]
        {
            usernameKey,
            displayName,
            LocalAccountStore.GetLastActiveUsername()
        };

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (LocalAccountStore.TryGetAvatar(candidate.Trim(), out avatar))
            {
                return true;
            }
        }

        avatar = new LocalAvatarProfile(LocalAccountStore.AvatarModeEmoji, LocalAccountStore.DefaultAvatarEmoji);
        return false;
    }

    public static string? BuildAvatarSyncValue(string? usernameKey, string? displayName)
    {
        if (!TryGetAvatarProfile(usernameKey, displayName, out var avatar)
            || string.IsNullOrWhiteSpace(avatar.Value))
        {
            return null;
        }

        if (string.Equals(avatar.Mode, LocalAccountStore.AvatarModeEmoji, StringComparison.OrdinalIgnoreCase))
        {
            return $"{EmojiAvatarPrefix}{avatar.Value.Trim()}";
        }

        if (!string.Equals(avatar.Mode, LocalAccountStore.AvatarModePhoto, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return BuildDataUrlFromPhotoPath(avatar.Value);
    }

    public static async Task<string?> BuildAvatarSyncValueAsync(HttpClient http, string? usernameKey, string? displayName)
    {
        if (!TryGetAvatarProfile(usernameKey, displayName, out var avatar)
            || string.IsNullOrWhiteSpace(avatar.Value))
        {
            return null;
        }

        if (string.Equals(avatar.Mode, LocalAccountStore.AvatarModeEmoji, StringComparison.OrdinalIgnoreCase))
        {
            return $"{EmojiAvatarPrefix}{avatar.Value.Trim()}";
        }

        if (!string.Equals(avatar.Mode, LocalAccountStore.AvatarModePhoto, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var stableKey = string.IsNullOrWhiteSpace(usernameKey)
            ? LocalAccountStore.GetLastActiveUsername()
            : usernameKey.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(stableKey))
        {
            return null;
        }

        return await UploadAvatarAsync(http, stableKey, avatar.Value);
    }

    public static string? ResolveLocalAvatarImagePath(string? usernameKey, string? displayName)
    {
        if (TryGetAvatarProfile(usernameKey, displayName, out var avatar)
            && string.Equals(avatar.Mode, LocalAccountStore.AvatarModePhoto, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(avatar.Value)
            && File.Exists(avatar.Value))
        {
            return avatar.Value;
        }

        return null;
    }

    public static bool TryCreateImageSource(string? rawValue, out ImageSource? imageSource)
    {
        imageSource = null;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (TryDecodeDataUrl(rawValue, out var bytes))
        {
            imageSource = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
            return true;
        }

        if (Uri.TryCreate(rawValue, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps
                || uri.Scheme == Uri.UriSchemeFile))
        {
            imageSource = ImageSource.FromUri(uri);
            return true;
        }

        if (Path.IsPathRooted(rawValue) && File.Exists(rawValue))
        {
            imageSource = ImageSource.FromFile(rawValue);
            return true;
        }

        return false;
    }

    private static string? BuildDataUrlFromPhotoPath(string localPath)
    {
        try
        {
            if (!File.Exists(localPath))
            {
                return null;
            }

            var mimeType = GetMimeType(localPath);
            if (mimeType is null)
            {
                return null;
            }

            var bytes = File.ReadAllBytes(localPath);
            if (bytes.Length == 0)
            {
                return null;
            }

            return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return null;
        }
    }

    private static string? GetMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => null
        };
    }

    private static async Task<string?> UploadAvatarAsync(HttpClient http, string usernameKey, string localPath)
    {
        try
        {
            if (!File.Exists(localPath))
            {
                return null;
            }

            var extension = Path.GetExtension(localPath).ToLowerInvariant();
            var mimeType = GetMimeType(localPath);
            if (mimeType is null)
            {
                return null;
            }

            var objectPath = $"{SanitizePathPart(usernameKey)}/avatar{extension}";
            var requestUri = new Uri(http.BaseAddress!, $"/storage/v1/object/{AvatarBucketName}/{objectPath}");

            await using var fileStream = File.OpenRead(localPath);
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
            request.Headers.Add("x-upsert", "true");
            request.Content = new StreamContent(fileStream);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);

            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return BuildPublicAvatarUrl(objectPath);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPublicAvatarUrl(string objectPath)
    {
        return $"{SupabaseConfig.ProjectUrl}/storage/v1/object/public/{AvatarBucketName}/{Uri.EscapeDataString(objectPath).Replace("%2F", "/")}";
    }

    private static string SanitizePathPart(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return "guest";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = trimmed
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray();
        return new string(chars);
    }

    private static bool TryDecodeDataUrl(string rawValue, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!rawValue.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separatorIndex = rawValue.IndexOf(',');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var metadata = rawValue[..separatorIndex];
        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(rawValue[(separatorIndex + 1)..]);
            return bytes.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
