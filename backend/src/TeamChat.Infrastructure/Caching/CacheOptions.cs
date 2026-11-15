namespace TeamChat.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public int DefaultTtlSeconds { get; set; } = 60;
}
