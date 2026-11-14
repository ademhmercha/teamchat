namespace TeamChat.Application.Common;

/// <summary>Configuration de l'envoi d'emails (section "Email").</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Dev" (journalise seulement) ou "Ses" (Amazon SES).</summary>
    public string Provider { get; set; } = "Dev";

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "TeamChat";

    /// <summary>Region SES (ex. eu-north-1).</summary>
    public string Region { get; set; } = "eu-north-1";
}

/// <summary>Configuration generale de l'application (section "App").</summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>URL publique du frontend, sans slash final (liens dans les emails).</summary>
    public string PublicUrl { get; set; } = string.Empty;
}
