namespace TeamChat.Domain.Entities;

/// <summary>Nature d'une notification affichee dans la cloche de l'application.</summary>
public enum NotificationType
{
    /// <summary>L'utilisateur a ete ajoute a une conversation de groupe.</summary>
    GroupAdded = 0,

    /// <summary>Un appel entrant n'a pas ete pris.</summary>
    MissedCall = 1,
}
