using TeamChat.Application.Calls.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Abstractions;

/// <summary>
/// Cycle de vie des appels 1-a-1. Toutes les regles de controle (destinataire
/// actif et en ligne, pas de double appel, autorisation de relayage) sont
/// verifiees cote serveur ; le hub ne fait que relayer.
/// </summary>
public interface ICallService
{
    Task<CallSessionDto> StartCallAsync(
        Guid callerId,
        Guid calleeId,
        CallType type,
        CancellationToken cancellationToken = default);

    Task<CallSessionDto> AcceptCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    Task<CallOutcomeDto> RejectCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    Task<CallOutcomeDto> EndCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Termine l'eventuel appel en cours lorsqu'un participant se deconnecte
    /// (derniere connexion fermee). Renvoie null si aucun appel en cours.
    /// </summary>
    Task<CallOutcomeDto?> EndCallForDisconnectedUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifie que l'utilisateur est bien partie prenante d'un appel accepte et
    /// renvoie le pair vers qui relayer la signalisation. Un tiers est refuse.
    /// </summary>
    Task<CallRelayDto> AuthorizeRelayAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default);

    /// <summary>Passe en "manque" les appels en sonnerie plus vieux que le delai indique.</summary>
    Task<IReadOnlyList<CallExpiredEvent>> ExpireStaleCallsAsync(
        TimeSpan ringingTimeout,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IceServerDto>> GetIceServersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CallHistoryItemDto>> GetHistoryAsync(
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
