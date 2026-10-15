using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>
/// Cas d'usage de gestion des groupes (reserves aux administrateurs).
/// L'archivage et la modification des membres invalident le cache d'appartenance.
/// </summary>
public interface IAdminGroupService
{
    Task<IReadOnlyList<GroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default);

    Task<GroupDto> GetGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<GroupDto> CreateAsync(
        Guid creatorId,
        CreateGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<GroupDto> RenameAsync(
        Guid groupId,
        RenameGroupRequest request,
        CancellationToken cancellationToken = default);

    Task<GroupOperationResult> UpdateMembersAsync(
        Guid groupId,
        UpdateGroupMembersRequest request,
        CancellationToken cancellationToken = default);

    Task<GroupOperationResult> ArchiveAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<GroupOperationResult> UnarchiveAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<GroupOperationResult> DeleteAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}
