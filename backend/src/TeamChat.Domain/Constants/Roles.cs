namespace TeamChat.Domain.Constants;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";

    public const string Owner = "Owner";

    public const string Admin = "Admin";

    public const string User = "User";

    public static readonly IReadOnlyList<string> All = new[] { SuperAdmin, Owner, Admin, User };

    public static readonly IReadOnlyList<string> TenantRoles = new[] { Owner, Admin, User };

    public const string CompanyAdmins = Owner + "," + Admin;
}
