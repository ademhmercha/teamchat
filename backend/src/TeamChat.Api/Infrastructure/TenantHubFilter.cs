using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;

namespace TeamChat.Api.Infrastructure;

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
