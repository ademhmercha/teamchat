namespace TeamChat.Application.Abstractions;

public interface IUserSessionTerminator
{
    Task TerminateAsync(Guid userId, CancellationToken cancellationToken = default);
}
