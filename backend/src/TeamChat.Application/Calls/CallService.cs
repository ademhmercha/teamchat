using TeamChat.Application.Abstractions;
using TeamChat.Application.Calls.Dtos;
using TeamChat.Application.Common;
using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Calls;

public sealed class CallService : ICallService
{
    private readonly ICallQueries _calls;
    private readonly IConversationService _conversations;
    private readonly IConversationQueries _conversationQueries;
    private readonly IUserProfileCache _profiles;
    private readonly IPresenceTracker _presence;
    private readonly IIceServerProvider _ice;
    private readonly TimeProvider _time;

    public CallService(
        ICallQueries calls,
        IConversationService conversations,
        IConversationQueries conversationQueries,
        IUserProfileCache profiles,
        IPresenceTracker presence,
        IIceServerProvider ice,
        TimeProvider time)
    {
        _calls = calls;
        _conversations = conversations;
        _conversationQueries = conversationQueries;
        _profiles = profiles;
        _presence = presence;
        _ice = ice;
        _time = time;
    }

    public async Task<CallSessionDto> StartCallAsync(
        Guid callerId,
        Guid calleeId,
        CallType type,
        CancellationToken cancellationToken = default)
    {
        if (callerId == calleeId)
        {
            throw new BadRequestException("Impossible de s'appeler soi-meme.");
        }

        var callee = await _profiles.GetAsync(calleeId, cancellationToken)
            ?? throw new BadRequestException("Destinataire introuvable.");
        if (!callee.IsActive)
        {
            throw new BadRequestException("Le destinataire est desactive.");
        }

        if (!_presence.GetOnlineUserIds().Contains(calleeId))
        {
            throw new BadRequestException("Le destinataire est hors ligne.");
        }

        if (await _calls.FindActiveCallForUserAsync(callerId, cancellationToken) is not null)
        {
            throw new ConflictException("Vous etes deja en appel.");
        }

        if (await _calls.FindActiveCallForUserAsync(calleeId, cancellationToken) is not null)
        {
            throw new ConflictException("Le destinataire est deja en appel.");
        }

        var caller = await _profiles.GetAsync(callerId, cancellationToken)
            ?? throw new ForbiddenException("Profil introuvable.");

        var now = _time.GetUtcNow();
        var callId = await _calls.CreateAsync(callerId, calleeId, type, now, cancellationToken);

        // La conversation directe sert de support au message systeme de fin d'appel.
        var conversation = await _conversations.GetOrCreateDirectAsync(callerId, calleeId, cancellationToken);

        return new CallSessionDto(
            callId,
            callerId,
            caller.DisplayName,
            calleeId,
            callee.DisplayName,
            type,
            CallStatus.Ringing,
            conversation.Id,
            now);
    }

    public async Task<CallSessionDto> AcceptCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        if (call.CalleeId != userId)
        {
            throw new ForbiddenException("Seul le destinataire peut accepter cet appel.");
        }

        if (call.Status != CallStatus.Ringing)
        {
            throw new ConflictException("Cet appel n'est plus en sonnerie.");
        }

        var now = _time.GetUtcNow();
        await _calls.UpdateStatusAsync(callId, CallStatus.Accepted, now, null, cancellationToken);
        return await BuildSessionAsync(call with { Status = CallStatus.Accepted }, cancellationToken);
    }

    public async Task<CallOutcomeDto> RejectCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        if (call.CalleeId != userId)
        {
            throw new ForbiddenException("Seul le destinataire peut refuser cet appel.");
        }

        if (call.Status != CallStatus.Ringing)
        {
            throw new ConflictException("Cet appel n'est plus en sonnerie.");
        }

        var now = _time.GetUtcNow();
        await _calls.UpdateStatusAsync(callId, CallStatus.Rejected, null, now, cancellationToken);
        var message = await PostSystemMessageAsync(call, "refuse", now, cancellationToken);
        return new CallOutcomeDto(call.Id, call.CallerId, call.CalleeId, call.Type, CallStatus.Rejected, message);
    }

    public async Task<CallOutcomeDto> EndCallAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await RequireCallAsync(callId, cancellationToken);
        if (call.CallerId != userId && call.CalleeId != userId)
        {
            throw new ForbiddenException("Vous ne faites pas partie de cet appel.");
        }

        if (call.Status is CallStatus.Ended or CallStatus.Missed or CallStatus.Rejected or CallStatus.Failed)
        {
            return new CallOutcomeDto(call.Id, call.CallerId, call.CalleeId, call.Type, call.Status, null);
        }

        var now = _time.GetUtcNow();
        var status = call.Status == CallStatus.Ringing ? CallStatus.Missed : CallStatus.Ended;
        await _calls.UpdateStatusAsync(callId, status, null, now, cancellationToken);
        var content = status == CallStatus.Missed ? "manque" : BuildEndedContent(call, now);
        var message = await PostSystemMessageAsync(call, content, now, cancellationToken);
        return new CallOutcomeDto(call.Id, call.CallerId, call.CalleeId, call.Type, status, message);
    }

    public async Task<CallOutcomeDto?> EndCallForDisconnectedUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var call = await _calls.FindActiveCallForUserAsync(userId, cancellationToken);
        if (call is null)
        {
            return null;
        }

        var now = _time.GetUtcNow();
        var status = call.Status == CallStatus.Accepted ? CallStatus.Ended : CallStatus.Missed;
        await _calls.UpdateStatusAsync(call.Id, status, null, now, cancellationToken);
        var content = status == CallStatus.Missed ? "manque" : BuildEndedContent(call, now);
        var message = await PostSystemMessageAsync(call, content, now, cancellationToken);
        return new CallOutcomeDto(call.Id, call.CallerId, call.CalleeId, call.Type, status, message);
    }

    public async Task<CallRelayDto> AuthorizeRelayAsync(
        Guid userId,
        Guid callId,
        CancellationToken cancellationToken = default)
    {
        var call = await RequireCallAsync(callId, cancellationToken);

        Guid peer;
        if (call.CallerId == userId)
        {
            peer = call.CalleeId;
        }
        else if (call.CalleeId == userId)
        {
            peer = call.CallerId;
        }
        else
        {
            throw new ForbiddenException("Vous ne faites pas partie de cet appel.");
        }

        if (call.Status != CallStatus.Accepted)
        {
            throw new ConflictException("L'appel n'est pas en cours.");
        }

        return new CallRelayDto(call.Id, peer, call.Type);
    }

    public async Task<IReadOnlyList<CallExpiredEvent>> ExpireStaleCallsAsync(
        TimeSpan ringingTimeout,
        CancellationToken cancellationToken = default)
    {
        var threshold = _time.GetUtcNow() - ringingTimeout;
        var stale = await _calls.FindStaleRingingAsync(threshold, cancellationToken);
        if (stale.Count == 0)
        {
            return Array.Empty<CallExpiredEvent>();
        }

        var expired = new List<CallExpiredEvent>(stale.Count);
        foreach (var call in stale)
        {
            var now = _time.GetUtcNow();
            await _calls.UpdateStatusAsync(call.Id, CallStatus.Missed, null, now, cancellationToken);
            var message = await PostSystemMessageAsync(call, "manque", now, cancellationToken);
            expired.Add(new CallExpiredEvent(call.Id, call.CallerId, call.CalleeId, call.Type, CallStatus.Missed, message));
        }

        return expired;
    }

    public Task<IReadOnlyList<IceServerDto>> GetIceServersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => _ice.GetIceServersAsync(userId, cancellationToken);

    public async Task<IReadOnlyList<CallHistoryItemDto>> GetHistoryAsync(
        Guid userId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var calls = await _calls.GetHistoryForUserAsync(userId, limit, cancellationToken);
        if (calls.Count == 0)
        {
            return Array.Empty<CallHistoryItemDto>();
        }

        var otherIds = calls
            .Select(c => c.CallerId == userId ? c.CalleeId : c.CallerId)
            .Distinct()
            .ToList();

        var names = new Dictionary<Guid, string>();
        foreach (var id in otherIds)
        {
            var profile = await _profiles.GetAsync(id, cancellationToken);
            names[id] = profile?.DisplayName ?? string.Empty;
        }

        return calls
            .Select(call =>
            {
                var otherId = call.CallerId == userId ? call.CalleeId : call.CallerId;
                var duration = call.AnsweredAt is { } answered && call.EndedAt is { } ended
                    ? (int)Math.Max(0, (ended - answered).TotalSeconds)
                    : (int?)null;

                return new CallHistoryItemDto(
                    call.Id,
                    otherId,
                    names.GetValueOrDefault(otherId, string.Empty),
                    call.Type,
                    call.Status,
                    call.StartedAt,
                    duration,
                    Outgoing: call.CallerId == userId);
            })
            .ToList();
    }

    private async Task<CallRecord> RequireCallAsync(Guid callId, CancellationToken cancellationToken)
        => await _calls.FindAsync(callId, cancellationToken)
           ?? throw new NotFoundException("Appel introuvable.");

    private async Task<CallSessionDto> BuildSessionAsync(CallRecord call, CancellationToken cancellationToken)
    {
        var caller = await _profiles.GetAsync(call.CallerId, cancellationToken);
        var callee = await _profiles.GetAsync(call.CalleeId, cancellationToken);
        var conversation = await _conversations.GetOrCreateDirectAsync(call.CallerId, call.CalleeId, cancellationToken);

        return new CallSessionDto(
            call.Id,
            call.CallerId,
            caller?.DisplayName ?? string.Empty,
            call.CalleeId,
            callee?.DisplayName ?? string.Empty,
            call.Type,
            call.Status,
            conversation.Id,
            call.StartedAt);
    }

    private async Task<MessageDto?> PostSystemMessageAsync(
        CallRecord call,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetOrCreateDirectAsync(call.CallerId, call.CalleeId, cancellationToken);
        var text = $"{TypeLabel(call.Type)} {content}";
        return await _conversationQueries.AddSystemMessageAsync(
            conversation.Id,
            call.CallerId,
            text,
            now,
            cancellationToken);
    }

    private static string BuildEndedContent(CallRecord call, DateTimeOffset now)
    {
        if (call.AnsweredAt is { } answered)
        {
            var seconds = Math.Max(0, (int)(now - answered).TotalSeconds);
            return $"termine (duree {FormatDuration(seconds)})";
        }

        return "termine";
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds < 60)
        {
            return $"{totalSeconds} s";
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return seconds == 0 ? $"{minutes} min" : $"{minutes} min {seconds} s";
    }

    private static string TypeLabel(CallType type)
        => type == CallType.Video ? "Appel video" : "Appel audio";
}
