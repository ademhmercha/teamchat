namespace TeamChat.Domain.Constants;

/// <summary>Noms des roles applicatifs.</summary>
public static class Roles
{
    /// <summary>Administrateur de la plateforme (hors societe, voit toutes les societes).</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Proprietaire d'une societe : controle total sur sa societe et ses comptes.</summary>
    public const string Owner = "Owner";

    /// <summary>Administrateur d'une societe (gere les membres de sa societe).</summary>
    public const string Admin = "Admin";

    /// <summary>Membre d'une societe.</summary>
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = new[] { SuperAdmin, Owner, Admin, User };

    /// <summary>Roles attribuables a un compte d'une societe (jamais SuperAdmin par un admin client).</summary>
    public static readonly IReadOnlyList<string> TenantRoles = new[] { Owner, Admin, User };

    /// <summary>Roles autorises a administrer une societe.</summary>
    public const string CompanyAdmins = Owner + "," + Admin;
}
