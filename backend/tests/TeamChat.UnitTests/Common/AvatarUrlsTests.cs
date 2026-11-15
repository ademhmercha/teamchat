using TeamChat.Application.Common;

namespace TeamChat.UnitTests.Common;

public sealed class AvatarUrlsTests
{
    private static readonly Guid Id = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void ForUser_builds_a_relative_path()
        => Assert.Equal($"/api/users/{Id}/avatar", AvatarUrls.ForUser(Id));

    [Theory]
    [InlineData("http://ec2-16-170-120-215.eu-north-1.compute.amazonaws.com")]
    [InlineData("https://app.example.com")]
    [InlineData("HTTP://LOCALHOST:5285")]
    public void Normalize_strips_the_host_of_legacy_absolute_urls(string host)
        => Assert.Equal($"/api/users/{Id}/avatar", AvatarUrls.Normalize($"{host}/api/users/{Id}/avatar"));

    [Fact]
    public void Normalize_drops_the_cache_busting_query()
        => Assert.Equal($"/api/users/{Id}/avatar", AvatarUrls.Normalize($"http://h/api/users/{Id}/avatar?t=1700000000"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/api/users/11111111-2222-3333-4444-555555555555/avatar")]
    [InlineData("https://lh3.googleusercontent.com/a/photo")]
    [InlineData("https://cdn.example.com/api/users/not-a-guid/avatar")]
    public void Normalize_leaves_other_values_untouched(string? value)
        => Assert.Equal(value, AvatarUrls.Normalize(value));
}
