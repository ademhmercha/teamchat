namespace TeamChat.Application.Auth.Dtos;

/// <summary>Inscription publique d'un proprietaire de societe (cree la societe + le compte Owner).</summary>
public sealed record RegisterCompanyRequest(
    string CompanyName,
    string DisplayName,
    string Email,
    string Password);
