using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using TeamChat.Api.Hubs;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;

namespace TeamChat.Api.Infrastructure;

/// <summary>
/// Service d'arriere-plan qui bascule en "manque" les appels restes en sonnerie
/// au-dela du delai configure, puis notifie les deux participants et diffuse le
/// message systeme correspondant.
/// </summary>
public sealed class CallTimeoutService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<ChatHub, IChatClient> _hub;
    private readonly CallOptions _options;
    private readonly ILogger<CallTimeoutService> _logger;

    public CallTimeoutService(
        IServiceScopeFactory scopeFactory,
        IHubContext<ChatHub, IChatClient> hub,
        IOptions<CallOptions> options,
        ILogger<CallTimeoutService> logger)
    {
        _scopeFactory = scopeFactory;
        _hub = hub;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.SweepSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Erreur lors du balayage des appels expires");
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var calls = scope.ServiceProvider.GetRequiredService<ICallService>();

        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RingingTimeoutSeconds));
        var expired = await calls.ExpireStaleCallsAsync(timeout, cancellationToken);

        foreach (var call in expired)
        {
            await _hub.Clients.Group(ChatGroups.User(call.CallerId)).CallEnded(call.CallId, call.Status);
            await _hub.Clients.Group(ChatGroups.User(call.CalleeId)).CallEnded(call.CallId, call.Status);

            if (call.SystemMessage is not null)
            {
                await _hub.Clients.Group(ChatGroups.Conversation(call.SystemMessage.ConversationId))
                    .ReceiveMessage(call.SystemMessage);
            }
        }
    }
}
