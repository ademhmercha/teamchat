using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;

namespace TeamChat.Infrastructure.Email;

/// <summary>Envoi reel via Amazon SES (identifiants : chaine par defaut du SDK, role IAM en production).</summary>
public sealed class SesEmailSender : IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly EmailOptions _options;
    private readonly ILogger<SesEmailSender> _logger;

    public SesEmailSender(
        IAmazonSimpleEmailServiceV2 ses,
        IOptions<EmailOptions> options,
        ILogger<SesEmailSender> logger)
    {
        _ses = ses;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = new Body { Text = new Content { Data = message.TextBody, Charset = "UTF-8" } };
            if (!string.IsNullOrEmpty(message.HtmlBody))
            {
                body.Html = new Content { Data = message.HtmlBody, Charset = "UTF-8" };
            }

            var request = new SendEmailRequest
            {
                FromEmailAddress = FormatFrom(),
                Destination = new Destination { ToAddresses = new List<string> { message.To } },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = message.Subject, Charset = "UTF-8" },
                        Body = body,
                    },
                },
            };

            await _ses.SendEmailAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ni adresse ni contenu dans le journal (donnees personnelles, jetons).
            _logger.LogError(ex, "Echec d'envoi d'email via SES (sujet : {Subject}).", message.Subject);
        }
    }

    private string FormatFrom() =>
        string.IsNullOrWhiteSpace(_options.FromName)
            ? _options.FromAddress
            : $"\"{_options.FromName.Replace("\"", string.Empty)}\" <{_options.FromAddress}>";
}
