using Microsoft.Extensions.Logging;
using TeamChat.Application.Abstractions;

namespace TeamChat.Infrastructure.Email;

/// <summary>
/// En dev, l'envoi d'email se contente de journaliser le contenu (dont le lien
/// d'invitation). A remplacer par un vrai fournisseur SMTP en production.
/// </summary>
public sealed class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[DevEmailSender] Email vers {To} - {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
