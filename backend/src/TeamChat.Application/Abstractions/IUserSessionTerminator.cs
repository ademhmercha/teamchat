namespace TeamChat.Application.Abstractions;

/// <summary>
/// Met fin aux sessions temps reel d'un utilisateur (connexions SignalR et
/// presence). Utilise notamment lors de la desactivation d'un compte par un
/// administrateur : les clients recus recoivent un ordre de deconnexion et la
/// presence est mise a jour immediatement.
/// </summary>
public interface IUserSessionTerminator
{
    Task TerminateAsync(Guid userId, CancellationToken cancellationToken = default);
}
