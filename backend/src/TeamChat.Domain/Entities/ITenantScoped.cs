namespace TeamChat.Domain.Entities;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
