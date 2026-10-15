namespace TeamChat.Application.Tenants.Dtos;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt);
