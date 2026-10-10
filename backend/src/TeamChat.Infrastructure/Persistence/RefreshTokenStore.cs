using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly TeamChatDbContext _db;
    private readonly TimeProvider _clock;

    public RefreshTokenStore(TeamChatDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task UpdateAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        _db.RefreshTokens.Update(token);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }
}
