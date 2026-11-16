namespace TeamChat.Application.Abstractions;

public interface IMembershipService
{
    Task<bool> IsMemberAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task InvalidateConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task InvalidateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}