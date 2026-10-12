namespace TeamChat.Application.Common;

/// <summary>Reglages du cycle de vie des appels.</summary>
public sealed class CallOptions
{
    public const string SectionName = "Calls";

    /// <summary>Duree de sonnerie avant passage automatique en "manque" (section 7 : ~30 s).</summary>
    public int RingingTimeoutSeconds { get; set; } = 30;

    /// <summary>Intervalle de balayage des appels en sonnerie expires.</summary>
    public int SweepSeconds { get; set; } = 5;
}
