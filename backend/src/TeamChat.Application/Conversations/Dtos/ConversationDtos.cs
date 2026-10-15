using TeamChat.Domain.Entities;

namespace TeamChat.Application.Conversations.Dtos;

public sealed record ConversationParticipantDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl);

public sealed record ConversationDto(
    Guid Id,
    ConversationType Type,
    string? Name,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ConversationParticipantDto> Participants);

public sealed record ConversationListItemDto(
    Guid Id,
    ConversationType Type,
    string? Name,
    Guid? OtherUserId,
    string? OtherDisplayName,
    string? OtherAvatarUrl,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    int ParticipantCount,
    int UnreadCount,
    bool Archived = false);

/// <summary>Detail d'une conversation (en-tete et panneau Details).</summary>
public sealed record ConversationDetailDto(
    Guid Id,
    ConversationType Type,
    string? Name,
    DateTimeOffset CreatedAt,
    bool Archived,
    IReadOnlyList<ConversationParticipantDto> Participants);

/// <summary>Fichier partage dans une conversation (onglet « Fichiers » du panneau Details).</summary>
public sealed record SharedAttachmentDto(
    Guid MessageId,
    long Sequence,
    Guid AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    int? DurationSec,
    Guid SenderId,
    string SenderDisplayName,
    MessageType Type,
    DateTimeOffset CreatedAt);

/// <summary>Requete d'archivage cote utilisateur.</summary>
public sealed record UpdateConversationRequest(bool Archived);

/// <summary>Resultat d'un depart de groupe : liste des participants restants.</summary>
public sealed record LeaveConversationResult(
    Guid ConversationId,
    IReadOnlyList<Guid> RemainingMemberIds);

public sealed record MessageAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    int? DurationSec);

public sealed record MessageDto(
    long Sequence,
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderDisplayName,
    string? SenderAvatarUrl,
    MessageType Type,
    string? Content,
    Guid? AttachmentId,
    DateTimeOffset CreatedAt,
    bool Edited,
    bool Deleted,
    MessageAttachmentDto? Attachment = null);

/// <summary>Curseur de pagination : ordre global des messages (Sequence).</summary>
public sealed record MessageCursor(long Sequence);

public sealed record MessageHistoryResult(
    IReadOnlyList<MessageDto> Items,
    MessageCursor? NextCursor,
    bool HasMore);

public sealed record CreateDirectConversationRequest(Guid UserId);

public sealed record SendMessageRequest(string Content);

/// <summary>Marque la conversation comme lue jusqu'a ce message (null = dernier message).</summary>
public sealed record MarkAsReadRequest(Guid? MessageId);

public sealed record CreateGroupRequest(string Name, IReadOnlyList<Guid> MemberIds);

public sealed record RenameGroupRequest(string Name);

public sealed record UpdateGroupMembersRequest(IReadOnlyList<Guid> MemberIds);

public sealed record GroupMemberDto(Guid UserId, string DisplayName, string? AvatarUrl);

public sealed record GroupDto(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    bool IsArchived,
    IReadOnlyList<GroupMemberDto> Members);

/// <summary>Resultat d'une mutation de groupe : sert a synchroniser les groupes SignalR.</summary>
public sealed record GroupOperationResult(
    Guid ConversationId,
    string GroupName,
    IReadOnlyList<Guid> AddedUserIds,
    IReadOnlyList<Guid> RemovedUserIds,
    IReadOnlyList<Guid> CurrentMemberIds);