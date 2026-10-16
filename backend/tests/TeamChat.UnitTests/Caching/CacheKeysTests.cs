using TeamChat.Application.Caching;

namespace TeamChat.UnitTests.Caching;

public sealed class CacheKeysTests
{
    [Fact]
    public void User_key_is_tenant_scoped_versioned_and_contains_identifier()
    {
        var tenantId = Guid.NewGuid();
        var id = Guid.NewGuid();

        var key = CacheKeys.User(tenantId, id);

        Assert.Equal($"tc:v1:t:{tenantId}:user:{id}", key);
    }

    [Fact]
    public void User_key_uses_placeholder_when_tenant_is_absent()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"tc:v1:t:_:user:{id}", CacheKeys.User(null, id));
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
        var tenantId = Guid.NewGuid();
        var key = CacheKeys.Directory(tenantId, 1, 20, "  Ada Lovelace ");

        Assert.Equal($"tc:v1:t:{tenantId}:directory:1:20:ada_lovelace", key);
    }

    [Fact]
    public void Directory_key_uses_placeholder_for_empty_search()
    {
        var tenantId = Guid.NewGuid();
        Assert.Equal($"tc:v1:t:{tenantId}:directory:2:50:_", CacheKeys.Directory(tenantId, 2, 50, "   "));
        Assert.Equal($"tc:v1:t:{tenantId}:directory:2:50:_", CacheKeys.Directory(tenantId, 2, 50, null));
    }

    [Fact]
    public void Directory_key_is_isolated_per_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Assert.NotEqual(
            CacheKeys.DirectoryTag(tenantA),
            CacheKeys.DirectoryTag(tenantB));
    }
}
