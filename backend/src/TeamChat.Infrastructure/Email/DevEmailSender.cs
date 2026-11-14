using Microsoft.Extensions.Logging;
using TeamChat.Application.Abstractions;

namespace TeamChat.Infrastructure.Email;

/// <summary>En dev, l'envoi d'email se contente de journaliser le contenu (dont les liens).</summary>
public sealed class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[DevEmailSender] Email vers {To} - {Subject}\n{Body}", message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}
