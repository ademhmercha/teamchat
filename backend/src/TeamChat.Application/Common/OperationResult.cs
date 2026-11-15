namespace TeamChat.Application.Common;

public sealed record OperationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    public static OperationResult Ok() => new(true, Array.Empty<string>());

    public static OperationResult Fail(params string[] errors) => new(false, errors);
}
