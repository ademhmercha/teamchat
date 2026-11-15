using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TeamChat.Application.Abstractions;

namespace TeamChat.Infrastructure.Security;

public sealed class HttpContextUserContext : ICurrentUser, ICurrentTenant, ICurrentTenantSetter
{
    private readonly IHttpContextAccessor _accessor;
    private bool _tenantResolved;
    private Guid? _tenantValue;

    public HttpContextUserContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public Guid? TenantId => _tenantResolved ? _tenantValue : TenantFromPrincipal();

    public bool FilterDisabled => !_tenantResolved && !IsAuthenticated;

    public void SetTenant(Guid? tenantId)
    {
        _tenantValue = tenantId;
        _tenantResolved = true;
    }

    private Guid? TenantFromPrincipal()
        => Guid.TryParse(Principal?.FindFirst("tenant_id")?.Value, out var id) ? id : null;
}
