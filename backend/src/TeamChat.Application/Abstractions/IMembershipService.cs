namespace TeamChat.Application.Abstractions;

/// <summary>
/// Verification d'appartenance a une conversation, avec cache (5 min) et
/// invalidation par conversation. Toujours invalider apres une modification
/// des membres (section 8 bis).
/// </summary>
public interface IMembershipService
{
    Task<bool> IsMemberAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task InvalidateConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);
}