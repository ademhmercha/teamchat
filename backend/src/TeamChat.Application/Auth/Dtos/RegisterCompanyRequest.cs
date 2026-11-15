namespace TeamChat.Application.Auth.Dtos;

public sealed record RegisterCompanyRequest(
    string CompanyName,
    string DisplayName,
    string Email,
    string Password);
