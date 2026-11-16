using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Abstractions;

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
