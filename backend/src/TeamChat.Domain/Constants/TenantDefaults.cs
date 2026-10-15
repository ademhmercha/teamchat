namespace TeamChat.Domain.Constants;

/// <summary>Valeurs stables de la societe par defaut (backfill et seed).</summary>
public static class TenantDefaults
{
    public static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-000000000001");

    public const string DefaultTenantName = "TeamChat";

    public const string DefaultTenantSlug = "teamchat";
}
