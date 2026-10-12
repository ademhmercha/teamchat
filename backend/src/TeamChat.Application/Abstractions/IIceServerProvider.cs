using TeamChat.Application.Calls.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Fournit la configuration ICE (STUN/TURN) pour un utilisateur donne.</summary>
public interface IIceServerProvider
{
    Task<IReadOnlyList<IceServerDto>> GetIceServersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
