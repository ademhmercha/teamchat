using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Application.Conversations;

/// <summary>
/// Gestion des groupes (admin). Toute modification de membres ou archivage
/// invalide le cache d'appartenance de la conversation (section 8 bis).
/// </summary>
public sealed class AdminGroupService : IAdminGroupService
{
    private const int MaxNameLength = 100;

    private readonly IAdminGroupQueries _queries;
    private readonly IMembershipService _membership;

    public AdminGroupService(IAdminGroupQueries queries, IMembershipService membership)
    {
        _queries = queries;
        _membership = membership;
    }

    public Task<IReadOnlyList<GroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
        => _queries.GetGroupsAsync(cancellationToken);

    public async Task<GroupDto> CreateAsync(
        Guid creatorId,
        CreateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new BadRequestException("Le nom du groupe est obligatoire.");
        }

        if (name.Length > MaxNameLength)
        {
            throw new BadRequestException($"Le nom du groupe ne peut pas depasser {MaxNameLength} caracteres.");
        }

        var memberIds = Normalize(request.MemberIds);
        memberIds.Add(creatorId);

        if (memberIds.Count < 2)
        {
            throw new BadRequestException("Un groupe doit contenir au moins deux membres.");
        }

        await EnsureUsersExistAsync(memberIds, cancellationToken);

        var conversation = await _queries.CreateGroupAsync(creatorId, name, memberIds, DateTimeOffset.UtcNow, cancellationToken);

        return new GroupDto(
            conversation.Id,
            conversation.Name ?? name,
            conversation.CreatedAt,
            IsArchived: false,
            conversation.Participants.Select(p => new GroupMemberDto(p.UserId, p.DisplayName, p.AvatarUrl)).ToList());
    }

    public async Task<GroupOperationResult> UpdateMembersAsync(
        Guid groupId,
        UpdateGroupMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _queries.GroupExistsAsync(groupId, cancellationToken))
        {
            throw new NotFoundException("Groupe introuvable.");
        }

        var memberIds = Normalize(request.MemberIds);
        if (memberIds.Count == 0)
        {
            throw new BadRequestException("Un groupe doit contenir au moins un membre.");
        }

        await EnsureUsersExistAsync(memberIds, cancellationToken);

        var result = await _queries.SetMembersAsync(groupId, memberIds, DateTimeOffset.UtcNow, cancellationToken);
        await _membership.InvalidateConversationAsync(groupId, cancellationToken);

        return result;
    }

    public async Task<GroupOperationResult> ArchiveAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        if (!await _queries.GroupExistsAsync(groupId, cancellationToken))
        {
            throw new NotFoundException("Groupe introuvable.");
        }

        var members = await _queries.GetParticipantIdsAsync(groupId, cancellationToken);
        await _queries.ArchiveAsync(groupId, DateTimeOffset.UtcNow, cancellationToken);
        await _membership.InvalidateConversationAsync(groupId, cancellationToken);

        return new GroupOperationResult(groupId, Array.Empty<Guid>(), Array.Empty<Guid>(), members);
    }

    private async Task EnsureUsersExistAsync(List<Guid> userIds, CancellationToken cancellationToken)
    {
        var count = await _queries.CountExistingUsersAsync(userIds, cancellationToken);
        if (count != userIds.Count)
        {
            throw new BadRequestException("Un ou plusieurs utilisateurs sont introuvables.");
        }
    }

    private static List<Guid> Normalize(IReadOnlyList<Guid>? ids)
        => ids is null ? new List<Guid>() : ids.Distinct().ToList();
}
