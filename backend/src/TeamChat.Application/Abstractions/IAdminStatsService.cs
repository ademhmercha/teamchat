using TeamChat.Application.Admin.Dtos;

namespace TeamChat.Application.Abstractions;

/// <summary>Calcul des statistiques du tableau de bord admin (resultat mis en cache 5 min).</summary>
public interface IAdminStatsService
{
    Task<AdminStatsDto> GetStatsAsync(CancellationToken cancellationToken = default);
}
