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

    /// <summary>L'utilisateur vient d'etre ajoute a une conversation de groupe.</summary>
    Task AddedToGroup(Guid conversationId);

    /// <summary>La liste des conversations a change (membres, archivage, nouveau message).</summary>
    Task ConversationUpdated(Guid conversationId);

    /// <summary>Le compte a ete desactive par un administrateur : le client doit
    /// fermer la connexion et revenir a l'ecran de connexion.</summary>
    Task ForceLogout();

    /// <summary>Un appel entrant sonne pour l'utilisateur.</summary>
    Task IncomingCall(CallSessionDto call);

    /// <summary>Le destinataire a accepte : l'appel passe en cours.</summary>
    Task CallAccepted(CallSessionDto call);

    /// <summary>L'appel est termine (raccrochage, refus, manque ou echec).</summary>
    Task CallEnded(Guid callId, CallStatus status);

    /// <summary>Signalisation WebRTC : offre SDP relayee au pair.</summary>
    Task ReceiveOffer(Guid callId, string sdp);

    /// <summary>Signalisation WebRTC : reponse SDP relayee au pair.</summary>
    Task ReceiveAnswer(Guid callId, string sdp);

    /// <summary>Signalisation WebRTC : candidat ICE relaye au pair.</summary>
    Task ReceiveIceCandidate(Guid callId, string candidate, string? sdpMid, int? sdpMLineIndex);
}
