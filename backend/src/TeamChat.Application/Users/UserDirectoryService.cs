using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Users.Dtos;

namespace TeamChat.Application.Users;

public sealed class UserDirectoryService : IUserDirectoryService
{
    private readonly IUserQueries _queries;

    public UserDirectoryService(IUserQueries queries)
    {
        _queries = queries;
    }

    public Task<PagedResult<UserListItemDto>> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => _queries.SearchAsync(search, page, pageSize, cancellationToken);

    public async Task<UserProfileDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await _queries.GetProfileAsync(id, cancellationToken)
           ?? throw new NotFoundException("Utilisateur introuvable.");
}
