namespace TeamChat.Application.Common;

public sealed class TurnOptions
{
    public const string SectionName = "Turn";

    public string StunUrls { get; set; } = "stun:stun.l.google.com:19302";

    public string Urls { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;

    public int CredentialTtlSeconds { get; set; } = 3600;
}
