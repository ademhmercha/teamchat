using TeamChat.Application.Calls.Dtos;
using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Api.Hubs;

public interface IChatClient
{
    Task ReceiveMessage(MessageDto message);

    Task MessageEdited(MessageDto message);

    Task MessageDeleted(Guid conversationId, Guid messageId);

    Task UserTyping(Guid conversationId, Guid userId, string displayName);

    Task UserStoppedTyping(Guid conversationId, Guid userId);

    Task PresenceChanged(Guid userId, bool isOnline);

    Task PresenceSnapshot(IReadOnlyList<Guid> onlineUserIds);

    Task AddedToGroup(Guid conversationId);

    Task ConversationUpdated(Guid conversationId);

    Task ForceLogout();

    Task IncomingCall(CallSessionDto call);

    Task CallAccepted(CallSessionDto call);

    Task CallHandled(Guid callId);

    Task CallEnded(Guid callId, CallStatus status);

    Task ReceiveOffer(Guid callId, string sdp);

    Task ReceiveAnswer(Guid callId, string sdp);

    Task ReceiveIceCandidate(Guid callId, string candidate, string? sdpMid, int? sdpMLineIndex);

    Task NotificationsUpdated();
}
