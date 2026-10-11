using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Conversations;

public sealed class ConversationService : IConversationService
{
    private const int MaxContentLength = 2000;

    private readonly IConversationQueries _queries;
    private readonly IMembershipService _membership;

    public ConversationService(IConversationQueries queries, IMembershipService membership)
    {
        _queries = queries;
        _membership = membership;
    }

    public async Task<ConversationDto> GetOrCreateDirectAsync(
        Guid userId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (userId == otherUserId)
        {
            throw new BadRequestException("Impossible de creer une conversation avec soi-meme.");
        }

        var existing = await _queries.FindDirectConversationAsync(userId, otherUserId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        return await _queries.CreateDirectAsync(userId, otherUserId, cancellationToken);
    }

    public async Task<MessageDto> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ValidateContent(content);

        if (!await _membership.IsMemberAsync(conversationId, userId, cancellationToken))
        {
            throw new ForbiddenException("Vous ne faites pas partie de cette conversation.");
        }

        return await _queries.AddMessageAsync(conversationId, userId, content.Trim(), DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<MessageHistoryResult> GetHistoryAsync(
        Guid userId,
        Guid conversationId,
        long? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!await _membership.IsMemberAsync(conversationId, userId, cancellationToken))
        {
            throw new ForbiddenException("Vous ne faites pas partie de cette conversation.");
        }

        return await _queries.GetMessagesAsync(conversationId, before, limit, cancellationToken);
    }

    public Task<IReadOnlyList<ConversationListItemDto>> GetMyConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => _queries.GetConversationsForUserAsync(userId, cancellationToken);

    public Task<IReadOnlyList<Guid>> GetMemberConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => _queries.GetMemberConversationIdsAsync(userId, cancellationToken);

    public async Task<MessageDto> EditMessageAsync(
        Guid userId,
        Guid messageId,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        ValidateContent(newContent);

        var message = await _queries.FindMessageAsync(messageId, cancellationToken)
            ?? throw new NotFoundException("Message introuvable.");

        await EnsureMemberAsync(message.ConversationId, userId, cancellationToken);
        EnsureAuthor(message, userId);

        if (message.Deleted)
        {
            throw new BadRequestException("Impossible de modifier un message supprime.");
        }

        return await _queries.UpdateMessageAsync(messageId, newContent.Trim(), DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task<MessageDto> DeleteMessageAsync(
        Guid userId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _queries.FindMessageAsync(messageId, cancellationToken)
            ?? throw new NotFoundException("Message introuvable.");

        await EnsureMemberAsync(message.ConversationId, userId, cancellationToken);
        EnsureAuthor(message, userId);

        if (message.Deleted)
        {
            return message;
        }

        return await _queries.SoftDeleteMessageAsync(messageId, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task MarkAsReadAsync(
        Guid userId,
        Guid conversationId,
        Guid? messageId,
        CancellationToken cancellationToken = default)
    {
        if (!await _membership.IsMemberAsync(conversationId, userId, cancellationToken))
        {
            throw new ForbiddenException("Vous ne faites pas partie de cette conversation.");
        }

        var target = messageId ?? await _queries.GetLastMessageIdAsync(conversationId, cancellationToken);
        if (target is null)
        {
            return;
        }

        await _queries.SetLastReadAsync(conversationId, userId, target, cancellationToken);
    }

    private async Task EnsureMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await _membership.IsMemberAsync(conversationId, userId, cancellationToken))
        {
            throw new ForbiddenException("Vous ne faites pas partie de cette conversation.");
        }
    }

    private void EnsureAuthor(MessageDto message, Guid userId)
    {
        if (message.SenderId != userId)
        {
            throw new ForbiddenException("Seul l'auteur peut modifier ou supprimer ce message.");
        }
    }

    private static void ValidateContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BadRequestException("Le contenu du message est vide.");
        }

        if (content.Length > MaxContentLength)
        {
            throw new BadRequestException($"Le message ne peut pas depasser {MaxContentLength} caracteres.");
        }
    }
}