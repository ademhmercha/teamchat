using System.Text.RegularExpressions;

namespace TeamChat.Application.Common;

public static partial class AvatarUrls
{
    public static string ForUser(Guid userId) => $"/api/users/{userId}/avatar";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var match = AbsoluteApiAvatar().Match(value);
        return match.Success ? match.Groups["path"].Value : value;
    }

    [GeneratedRegex(@"^https?://[^/]+(?<path>/api/users/[0-9a-fA-F-]{36}/avatar)(?:[?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex AbsoluteApiAvatar();
}
