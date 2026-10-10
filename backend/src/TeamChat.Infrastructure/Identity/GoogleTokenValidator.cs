using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;

namespace TeamChat.Infrastructure.Identity;

public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;

    /// <summary>Optionnel : restreint la connexion a un domaine de societe (ex. "exemple.com").</summary>
    public string? AllowedDomain { get; set; }
}

public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleOptions _options;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IOptions<GoogleOptions> options, ILogger<GoogleTokenValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            _logger.LogWarning("Google:ClientId non configure : la connexion Google est desactivee.");
            return null;
        }

        try
        {
            var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new Google.Apis.Auth.GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _options.ClientId },
                });

            if (!string.IsNullOrWhiteSpace(_options.AllowedDomain) &&
                !string.Equals(payload.HostedDomain, _options.AllowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Connexion Google refusee : domaine non autorise.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            return new GoogleUserInfo(payload.Email, payload.Name, payload.Picture);
        }
        catch (Google.Apis.Auth.InvalidJwtException ex)
        {
            _logger.LogInformation(ex, "ID token Google invalide.");
            return null;
        }
    }
}
