using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;

namespace TeamChat.Api.Infrastructure;

/// <summary>
/// Renseigne la societe courante lors des invocations du hub SignalR. Hors
/// requete HTTP, <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/>
/// n'est pas disponible : on lit la societe depuis le principal authentifie
/// (claim tenant_id) et on l'impose au contexte scoped courant.
/// </summary>
public sealed class TenantHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ApplyTenant(invocationContext.ServiceProvider, invocationContext.Context.User);
        return await next(invocationContext);
    }

    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        ApplyTenant(context.ServiceProvider, context.Context.User);
        await next(context);
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        ApplyTenant(context.ServiceProvider, context.Context.User);
        await next(context, exception);
    }

    private static void ApplyTenant(IServiceProvider services, System.Security.Claims.ClaimsPrincipal? user)
    {
        var setter = services.GetRequiredService<ICurrentTenantSetter>();
        var claim = user?.FindFirst("tenant_id")?.Value;
        setter.SetTenant(Guid.TryParse(claim, out var tenantId) ? tenantId : null);
    }
}
