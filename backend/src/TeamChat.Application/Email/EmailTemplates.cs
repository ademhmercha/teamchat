using System.Net;
using TeamChat.Application.Abstractions;

namespace TeamChat.Application.Email;

/// <summary>Gabarits d'emails (HTML + texte). Aucune URL en dur : le lien de base vient de <c>App:PublicUrl</c>.</summary>
public static class EmailTemplates
{
    public static EmailMessage Invitation(
        string to, string displayName, string companyName, string baseUrl, Guid userId, string token)
    {
        var link = $"{Trim(baseUrl)}/accept-invite?userId={userId}&token={Uri.EscapeDataString(token)}";
        return Build(
            to,
            $"Invitation a rejoindre {companyName} sur TeamChat",
            $"Bonjour {displayName},",
            $"Vous avez ete invite(e) a rejoindre {companyName} sur TeamChat. Definissez votre mot de passe pour activer votre compte.",
            "Definir mon mot de passe",
            link);
    }

    public static EmailMessage PasswordReset(string to, string displayName, string baseUrl, Guid userId, string token)
    {
        var link = $"{Trim(baseUrl)}/reset-password?userId={userId}&token={Uri.EscapeDataString(token)}";
        return Build(
            to,
            "Reinitialisation de votre mot de passe TeamChat",
            $"Bonjour {displayName},",
            "Une reinitialisation de votre mot de passe a ete demandee. Si ce n'est pas vous, ignorez cet email.",
            "Choisir un nouveau mot de passe",
            link);
    }

    public static EmailMessage Welcome(string to, string displayName, string companyName, string baseUrl) =>
        Build(
            to,
            $"Bienvenue sur TeamChat : {companyName} est prete",
            $"Bonjour {displayName},",
            $"Votre espace {companyName} est cree et vous en etes le proprietaire. Invitez vos collegues et commencez a discuter.",
            "Ouvrir TeamChat",
            $"{Trim(baseUrl)}/login");

    private static string Trim(string baseUrl) => baseUrl.TrimEnd('/');

    private static EmailMessage Build(
        string to, string subject, string greeting, string message, string action, string link)
    {
        var text = $"{greeting}\n\n{message}\n\n{action} : {link}\n\n-- L'equipe TeamChat";

        var html =
            "<!doctype html><html lang=\"fr\"><body style=\"margin:0;padding:24px;background:#f4f5f7;font-family:Arial,Helvetica,sans-serif;color:#1f2937\">"
            + "<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr><td align=\"center\">"
            + "<table role=\"presentation\" width=\"520\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:520px;background:#ffffff;border-radius:8px;padding:32px\"><tr><td>"
            + $"<p style=\"margin:0 0 16px;font-size:16px;font-weight:bold\">{H(greeting)}</p>"
            + $"<p style=\"margin:0 0 24px;font-size:15px;line-height:1.5\">{H(message)}</p>"
            + $"<p style=\"margin:0 0 24px\"><a href=\"{H(link)}\" style=\"display:inline-block;background:#2563eb;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:6px;font-size:15px\">{H(action)}</a></p>"
            + $"<p style=\"margin:0 0 8px;font-size:12px;color:#6b7280\">Si le bouton ne fonctionne pas, copiez ce lien dans votre navigateur :<br>{H(link)}</p>"
            + "<p style=\"margin:16px 0 0;font-size:12px;color:#6b7280\">L'equipe TeamChat</p>"
            + "</td></tr></table></td></tr></table></body></html>";

        return new EmailMessage(to, subject, text, html);
    }

    private static string H(string value) => WebUtility.HtmlEncode(value);
}
