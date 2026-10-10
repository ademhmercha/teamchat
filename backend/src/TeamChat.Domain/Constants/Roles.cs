namespace TeamChat.Domain.Constants;

/// <summary>Noms des roles applicatifs.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = new[] { Admin, User };
}
