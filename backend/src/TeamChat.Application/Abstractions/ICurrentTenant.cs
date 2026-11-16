namespace TeamChat.Application.Abstractions;

public interface ICurrentTenant
{
    Guid? TenantId { get; }

    bool FilterDisabled { get; }
}

public interface ICurrentTenantSetter
{
    void SetTenant(Guid? tenantId);
}
