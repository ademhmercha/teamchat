namespace TeamChat.Application.Abstractions;

/// <summary>Utilisateur authentifie de la requete courante (issu du JWT).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
