using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IAdminGroupQueries
{
    Task<IReadOnlyList<GroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<GroupDto?> FindGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<ConversationDto> CreateGroupAsync(
        Guid creatorId,
        string name,
        IReadOnlyList<Guid> memberIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<GroupOperationResult> SetMembersAsync(
        Guid groupId,
        IReadOnlyList<Guid> memberIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        Guid groupId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task RenameAsync(
        Guid groupId,
        string name,
        CancellationToken cancellationToken = default);

    Task UnarchiveAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetParticipantIdsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<bool> GroupExistsAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<int> CountExistingUsersAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);
}
