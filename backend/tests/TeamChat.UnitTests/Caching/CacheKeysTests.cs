using TeamChat.Application.Caching;

namespace TeamChat.UnitTests.Caching;

public sealed class CacheKeysTests
{
    [Fact]
    public void User_key_is_prefixed_versioned_and_contains_identifier()
    {
        var id = Guid.NewGuid();

        var key = CacheKeys.User(id);

        Assert.Equal($"tc:v1:user:{id}", key);
    }

    [Fact]
    public void Membership_key_contains_conversation_and_user()
    {
        var conversationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var key = CacheKeys.Membership(conversationId, userId);

        Assert.Equal($"tc:v1:membership:{conversationId}:{userId}", key);
        Assert.DoesNotContain(userId.ToString(), CacheKeys.MembershipTag(conversationId));
    }

    [Fact]
    public void Directory_key_normalizes_search_term()
    {
        var key = CacheKeys.Directory(1, 20, "  Ada Lovelace ");

        Assert.Equal("tc:v1:directory:1:20:ada_lovelace", key);
    }

    [Fact]
    public void Directory_key_uses_placeholder_for_empty_search()
    {
        Assert.Equal("tc:v1:directory:2:50:_", CacheKeys.Directory(2, 50, "   "));
        Assert.Equal("tc:v1:directory:2:50:_", CacheKeys.Directory(2, 50, null));
    }
}
