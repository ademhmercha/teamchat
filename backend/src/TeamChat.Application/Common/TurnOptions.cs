namespace TeamChat.Application.Common;

/// <summary>
/// Configuration des serveurs ICE utilises par WebRTC (section 10).
/// Les identifiants TURN temporaires sont derives du secret partage
/// (mecanisme "TURN REST API" de coturn) et ne sont jamais partages entre utilisateurs.
/// </summary>
public sealed class TurnOptions
{
    public const string SectionName = "Turn";

    /// <summary>URLs STUN, separees par des virgules.</summary>
    public string StunUrls { get; set; } = "stun:stun.l.google.com:19302";

    /// <summary>URLs TURN, separees par des virgules (vides = pas de TURN).</summary>
    public string Urls { get; set; } = string.Empty;

    /// <summary>Secret partage avec coturn (option use-auth-secret). Jamais committe.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Duree de validite des identifiants TURN temporaires, en secondes.</summary>
    public int CredentialTtlSeconds { get; set; } = 3600;
}
