namespace TeamChat.Application.Abstractions;

/// <summary>Email a envoyer : version texte obligatoire, version HTML optionnelle.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);

public interface IEmailSender
{
    /// <summary>
    /// Envoie un email. Les implementations de production journalisent les echecs
    /// au lieu de les propager : un probleme d'envoi ne doit pas faire echouer la requete.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
