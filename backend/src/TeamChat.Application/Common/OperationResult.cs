namespace TeamChat.Application.Common;

/// <summary>Resultat d'une operation d'identite (sans exposer IdentityResult dans l'Application).</summary>
public sealed record OperationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static OperationResult Ok() => new(true, Array.Empty<string>());

    public static OperationResult Fail(params string[] errors) => new(false, errors);
}
