using TeamChat.Application.Calls.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IIceServerProvider
{
    Task<IReadOnlyList<IceServerDto>> GetIceServersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
