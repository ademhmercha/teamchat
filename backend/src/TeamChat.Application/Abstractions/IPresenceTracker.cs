namespace TeamChat.Application.Abstractions;

/// <summary>Changement d'etat de presence pour un utilisateur.</summary>
public sealed record PresenceChange(Guid UserId, bool IsNowOnline);

/// <summary>
/// Suivi de presence en memoire (par instance). Chaque connexion SignalR est
/// associee a un utilisateur ; un utilisateur est en ligne des qu'il a au moins
/// une connexion active (multi-onglets).
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Enregistre une connexion. <paramref name="IsNowOnline"/> vaut vrai
    /// uniquement pour la premiere connexion (transition hors-ligne → en ligne).</summary>
    Task<PresenceChange> ConnectAsync(
        Guid userId,
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Retire une connexion. Retourne un changement uniquement lors du
    /// passage en ligne → hors-ligne (plus aucune connexion active).</summary>
    Task<PresenceChange?> DisconnectAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    IReadOnlyList<Guid> GetOnlineUserIds();
}