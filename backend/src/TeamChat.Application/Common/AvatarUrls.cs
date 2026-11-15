using System.Text.RegularExpressions;

namespace TeamChat.Application.Common;

/// <summary>
/// URL des avatars hebergees par l'API. Toujours un chemin relatif (/api/users/{id}/avatar) :
/// derriere CloudFront, Request.Scheme/Request.Host valent l'origine EC2 en http, et une URL absolue
/// construite dessus est bloquee par le navigateur (Mixed Content).
/// </summary>
public static partial class AvatarUrls
{
    public static string ForUser(Guid userId) => $"/api/users/{userId}/avatar";

    /// <summary>
    /// Ramene une ancienne URL absolue vers l'avatar de l'API (http://hote/api/users/{id}/avatar...)
    /// a son chemin relatif. Toute autre valeur (nulle, deja relative, URL externe) est renvoyee telle quelle.
    /// </summary>
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
