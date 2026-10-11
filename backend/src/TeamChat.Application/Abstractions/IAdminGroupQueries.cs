using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Acces aux donnees des groupes (cote requetes).</summary>
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

    /// <summary>Remplace l'ensemble des membres. Renvoie les ajouts et retraits.</summary>
    Task<GroupOperationResult> SetMembersAsync(
        Guid groupId,
        IReadOnlyList<Guid> memberIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        Guid groupId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetParticipantIdsAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<bool> GroupExistsAsync(Guid groupId, CancellationToken cancellationToken = default);

    /// <summary>Nombre d'identifiants correspondant a des utilisateurs existants.</summary>
    Task<int> CountExistingUsersAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);
}
