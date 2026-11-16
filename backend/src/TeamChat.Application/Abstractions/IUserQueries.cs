using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IUserQueries
{
    Task<PagedResult<UserListItemDto>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<UserProfileDto>> AdminSearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);
}
