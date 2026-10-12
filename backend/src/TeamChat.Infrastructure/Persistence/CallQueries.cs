using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Acces aux donnees des appels.</summary>
public sealed class CallQueries : ICallQueries
{
    private static readonly Expression<Func<CallLog, CallRecord>> Project = call => new CallRecord(
        call.Id,
        call.CallerId,
        call.CalleeId,
        call.Type,
        call.Status,
        call.StartedAt,
        call.AnsweredAt,
        call.EndedAt);

    private readonly TeamChatDbContext _db;

    public CallQueries(TeamChatDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateAsync(
        Guid callerId,
        Guid calleeId,
        CallType type,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var call = new CallLog
        {
            Id = Guid.NewGuid(),
            CallerId = callerId,
            CalleeId = calleeId,
            Type = type,
            Status = CallStatus.Ringing,
            StartedAt = now,
        };

        _db.Calls.Add(call);
        await _db.SaveChangesAsync(cancellationToken);
        return call.Id;
    }

    public async Task<CallRecord?> FindAsync(Guid callId, CancellationToken cancellationToken = default)
        => await _db.Calls.AsNoTracking()
            .Where(call => call.Id == callId)
            .Select(Project)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CallRecord?> FindActiveCallForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await _db.Calls.AsNoTracking()
            .Where(call => (call.CallerId == userId || call.CalleeId == userId)
                && (call.Status == CallStatus.Ringing || call.Status == CallStatus.Accepted))
            .OrderByDescending(call => call.StartedAt)
            .Select(Project)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpdateStatusAsync(
        Guid callId,
        CallStatus status,
        DateTimeOffset? answeredAt,
        DateTimeOffset? endedAt,
        CancellationToken cancellationToken = default)
    {
        var call = await _db.Calls.FirstOrDefaultAsync(c => c.Id == callId, cancellationToken);
        if (call is null)
        {
            return;
        }

        call.Status = status;
        if (answeredAt is not null)
        {
            call.AnsweredAt = answeredAt;
        }

        if (endedAt is not null)
        {
            call.EndedAt = endedAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CallRecord>> FindStaleRingingAsync(
        DateTimeOffset startedBefore,
        CancellationToken cancellationToken = default)
        => await _db.Calls.AsNoTracking()
            .Where(call => call.Status == CallStatus.Ringing && call.StartedAt < startedBefore)
            .OrderBy(call => call.StartedAt)
            .Select(Project)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CallRecord>> GetHistoryForUserAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
        => await _db.Calls.AsNoTracking()
            .Where(call => call.CallerId == userId || call.CalleeId == userId)
            .OrderByDescending(call => call.StartedAt)
            .Take(limit)
            .Select(Project)
            .ToListAsync(cancellationToken);
}
