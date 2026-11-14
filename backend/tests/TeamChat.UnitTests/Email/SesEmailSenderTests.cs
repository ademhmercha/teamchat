using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Application.Email;
using TeamChat.Infrastructure.Email;

namespace TeamChat.UnitTests.Email;

public sealed class SesEmailSenderTests
{
    private static SesEmailSender Create(Mock<IAmazonSimpleEmailServiceV2> ses, string fromName = "TeamChat") =>
        new(
            ses.Object,
            Options.Create(new EmailOptions { Provider = "Ses", FromAddress = "no-reply@example.com", FromName = fromName }),
            NullLogger<SesEmailSender>.Instance);

    [Fact]
    public async Task Sends_text_and_html_to_recipient_with_configured_sender()
    {
        var ses = new Mock<IAmazonSimpleEmailServiceV2>();
        SendEmailRequest? captured = null;
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new SendEmailResponse());

        await Create(ses).SendAsync(new EmailMessage("bob@example.com", "Sujet", "texte", "<p>html</p>"));

        Assert.NotNull(captured);
        Assert.Equal("\"TeamChat\" <no-reply@example.com>", captured!.FromEmailAddress);
        Assert.Equal(new[] { "bob@example.com" }, captured.Destination.ToAddresses);
        Assert.Equal("Sujet", captured.Content.Simple.Subject.Data);
        Assert.Equal("texte", captured.Content.Simple.Body.Text.Data);
        Assert.Equal("<p>html</p>", captured.Content.Simple.Body.Html.Data);
    }

    [Fact]
    public async Task Omits_html_part_when_not_provided_and_uses_bare_address_without_name()
    {
        var ses = new Mock<IAmazonSimpleEmailServiceV2>();
        SendEmailRequest? captured = null;
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((r, _) => captured = r)
            .ReturnsAsync(new SendEmailResponse());

        await Create(ses, fromName: "").SendAsync(new EmailMessage("bob@example.com", "S", "t"));

        Assert.Equal("no-reply@example.com", captured!.FromEmailAddress);
        Assert.Null(captured.Content.Simple.Body.Html);
    }

    [Fact]
    public async Task Send_failure_is_swallowed_so_the_request_does_not_fail()
    {
        var ses = new Mock<IAmazonSimpleEmailServiceV2>();
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MessageRejectedException("sandbox : adresse non verifiee"));

        var exception = await Record.ExceptionAsync(() =>
            Create(ses).SendAsync(new EmailMessage("bob@example.com", "S", "t")));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        var ses = new Mock<IAmazonSimpleEmailServiceV2>();
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Create(ses).SendAsync(new EmailMessage("bob@example.com", "S", "t")));
    }
}

public sealed class EmailTemplatesTests
{
    [Fact]
    public void Password_reset_builds_link_from_public_url_and_escapes_token()
    {
        var id = Guid.NewGuid();

        var mail = EmailTemplates.PasswordReset("bob@example.com", "Bob", "https://app.example.com/", id, "a+b/c=");

        Assert.Contains($"https://app.example.com/reset-password?userId={id}&token=a%2Bb%2Fc%3D", mail.TextBody);
        Assert.NotNull(mail.HtmlBody);
        Assert.Contains("a%2Bb%2Fc%3D", mail.HtmlBody);
    }

    [Fact]
    public void Invitation_and_welcome_use_public_url()
    {
        var invite = EmailTemplates.Invitation("a@b.c", "Ana", "Acme", "https://app.example.com", Guid.NewGuid(), "t");
        var welcome = EmailTemplates.Welcome("a@b.c", "Ana", "Acme", "https://app.example.com");

        Assert.Contains("https://app.example.com/accept-invite?userId=", invite.TextBody);
        Assert.Contains("https://app.example.com/login", welcome.TextBody);
    }

    [Fact]
    public void Html_body_encodes_user_supplied_values()
    {
        var mail = EmailTemplates.Welcome("a@b.c", "<script>alert(1)</script>", "A&B", "https://app.example.com");

        Assert.DoesNotContain("<script>", mail.HtmlBody);
        Assert.Contains("&lt;script&gt;", mail.HtmlBody);
        Assert.Contains("A&amp;B", mail.HtmlBody);
    }
}
