using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;
using TeamChat.Api.Hubs;
using TeamChat.Api.Infrastructure;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Attachments.Dtos;
using TeamChat.Application.Conversations.Dtos;

namespace TeamChat.Api.Controllers;

[ApiController]
[Authorize]
public sealed class AttachmentsController : ControllerBase
{
    private readonly IAttachmentService _attachments;
    private readonly IHubContext<ChatHub, IChatClient> _hub;

    public AttachmentsController(IAttachmentService attachments, IHubContext<ChatHub, IChatClient> hub)
    {
        _attachments = attachments;
        _hub = hub;
    }

    [HttpPost("api/conversations/{conversationId:guid}/attachments")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<MessageDto>> Upload(
        Guid conversationId,
        [FromForm] UploadAttachmentForm form,
        CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Fichier manquant.", Detail = "Aucun fichier n'a ete fourni." });
        }

        await using var stream = form.File.OpenReadStream();
        var upload = new AttachmentUpload(
            form.File.FileName,
            form.File.ContentType,
            form.File.Length,
            stream,
            form.IsVoice,
            form.DurationSec);

        var message = await _attachments.SaveAsync(User.GetUserId(), conversationId, upload, cancellationToken);

        await _hub.Clients
            .Group(ChatGroups.Conversation(conversationId))
            .ReceiveMessage(message);

        return Ok(message);
    }

    [HttpGet("api/attachments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var download = await _attachments.OpenAsync(User.GetUserId(), id, cancellationToken);

        var inline = download.ContentType.StartsWith("image/", StringComparison.Ordinal)
            || download.ContentType.StartsWith("audio/", StringComparison.Ordinal);

        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(download.FileName);

        Response.Headers.CacheControl = "private, max-age=300";
        Response.Headers.ETag = $"\"att-{id:N}\"";
        Response.Headers.ContentDisposition = disposition.ToString();

        return new FileStreamResult(download.Content, download.ContentType)
        {
            EnableRangeProcessing = true,
        };
    }
}

public sealed class UploadAttachmentForm
{
    public IFormFile? File { get; set; }

    /// <summary>Vrai pour un message vocal enregistre via le navigateur.</summary>
    public bool IsVoice { get; set; }

    /// <summary>Duree du message vocal en secondes (facultatif).</summary>
    public int? DurationSec { get; set; }
}
