namespace TeamChat.Application.Platform.Dtos;

/// <summary>Vue plateforme d'une societe avec ses compteurs (super-admin).</summary>
public sealed record PlatformTenantDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    DateTimeOffset CreatedAt,
    int UserCount,
    int ActiveUserCount,
    int GroupCount,
    int MessageCount);

/// <summary>Mise a jour d'une societe par le super-admin (champs optionnels).</summary>
public sealed record UpdateTenantRequest(string? Name, bool? IsActive);
