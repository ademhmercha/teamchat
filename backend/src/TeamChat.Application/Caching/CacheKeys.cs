namespace TeamChat.Application.Caching;

/// <summary>
/// Fabrique centralisee des cles et tags de cache.
/// Regle : cles prefixees et versionnees, incluant toujours l'identifiant concerne (section 8 bis).
/// </summary>
public static class CacheKeys
{
    public const string Version = "v1";

    private const string Prefix = "tc";

    private static readonly string Root = $"{Prefix}:{Version}";

    public static string User(Guid userId) => $"{Root}:user:{userId}";

    public static string UserTag(Guid userId) => $"{Root}:user:{userId}";

    public static string Membership(Guid conversationId, Guid userId) =>
        $"{Root}:membership:{conversationId}:{userId}";

    public static string MembershipTag(Guid conversationId) =>
        $"{Root}:membership:{conversationId}";

    public static string UserMembershipTag(Guid userId) =>
        $"{Root}:membership-user:{userId}";

    public static string Directory(int page, int pageSize, string? search) =>
        $"{Root}:directory:{page}:{pageSize}:{Sanitize(search)}";

    public static string DirectoryTag() => $"{Root}:directory";

    public static string AdminStats() => $"{Root}:admin:stats";

    public static string StunTurn() => $"{Root}:stun-turn";

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "_";
        }

        var trimmed = value.Trim().ToLowerInvariant();
        var buffer = new char[trimmed.Length];

        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            buffer[i] = char.IsLetterOrDigit(c) ? c : '_';
        }

        return new string(buffer);
    }
}
