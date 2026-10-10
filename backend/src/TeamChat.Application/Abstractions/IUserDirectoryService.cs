using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Application.Abstractions;

public interface IUserDirectoryService
{
    Task<PagedResult<UserListItemDto>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto> GetAsync(Guid id, CancellationToken cancellationToken = default);
}
