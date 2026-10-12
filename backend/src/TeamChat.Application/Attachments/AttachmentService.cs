using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Attachments.Dtos;
using TeamChat.Application.Common;
using TeamChat.Application.Conversations.Dtos;
using TeamChat.Domain.Entities;

namespace TeamChat.Application.Attachments;

public sealed class AttachmentService : IAttachmentService
{
    private const int MaxFileNameLength = 260;
    private const int MaxDurationSec = 3600;

    private readonly IAttachmentQueries _queries;
    private readonly IMembershipService _membership;
    private readonly IFileStorage _storage;
    private readonly StorageOptions _options;

    public AttachmentService(
        IAttachmentQueries queries,
        IMembershipService membership,
        IFileStorage storage,
        IOptions<StorageOptions> options)
    {
        _queries = queries;
        _membership = membership;
        _storage = storage;
        _options = options.Value;
    }

    public async Task<MessageDto> SaveAsync(
        Guid userId,
        Guid conversationId,
        AttachmentUpload upload,
        CancellationToken cancellationToken = default)
    {
        if (!await _membership.IsMemberAsync(conversationId, userId, cancellationToken))
        {
            throw new ForbiddenException("Vous ne faites pas partie de cette conversation.");
        }

        var maxBytes = upload.IsVoice ? _options.MaxVoiceSizeBytes : _options.MaxFileSizeBytes;
        if (upload.Length <= 0)
        {
            throw new BadRequestException("Le fichier est vide.");
        }

        if (upload.Length > maxBytes)
        {
            throw new BadRequestException($"Le fichier ne peut pas depasser {maxBytes / (1024 * 1024)} Mo.");
        }

        var data = await ReadAllBytesAsync(upload.Content, maxBytes, cancellationToken);

        var detected = FileSignatureDetector.Detect(data, upload.DeclaredContentType, upload.FileName, upload.IsVoice)
            ?? throw new BadRequestException("Type de fichier non autorise.");

        if (upload.IsVoice && detected.Kind != AttachmentKind.Voice)
        {
            throw new BadRequestException("Un message vocal doit etre un fichier audio.");
        }

        var type = detected.Kind switch
        {
            AttachmentKind.Image => MessageType.Image,
            AttachmentKind.Voice => MessageType.Voice,
            _ => MessageType.File,
        };

        var storageKey = Guid.NewGuid().ToString("N");
        var fileName = SanitizeFileName(upload.FileName);

        await _storage.SaveAsync(storageKey, new MemoryStream(data, writable: false), detected.ContentType, cancellationToken);

        try
        {
            return await _queries.PersistAttachmentMessageAsync(
                conversationId,
                userId,
                storageKey,
                fileName,
                detected.ContentType,
                data.LongLength,
                type == MessageType.Voice ? NormalizeDuration(upload.DurationSec) : null,
                type,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
        catch
        {
            await SafeDeleteAsync(storageKey, cancellationToken);
            throw;
        }
    }

    public async Task<AttachmentDownload> OpenAsync(
        Guid userId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var info = await _queries.FindForDownloadAsync(attachmentId, cancellationToken)
            ?? throw new NotFoundException("Piece jointe introuvable.");

        if (!await _membership.IsMemberAsync(info.ConversationId, userId, cancellationToken))
        {
            throw new ForbiddenException("Vous ne faites pas partie de cette conversation.");
        }

        Stream stream;
        try
        {
            stream = await _storage.OpenReadAsync(info.StorageKey, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Fichier introuvable.");
        }

        return new AttachmentDownload(stream, info.FileName, info.ContentType, info.SizeBytes);
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;

        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new BadRequestException($"Le fichier ne peut pas depasser {maxBytes / (1024 * 1024)} Mo.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private async Task SafeDeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(storageKey, cancellationToken);
        }
        catch
        {
            // La compensation best-effort ne doit pas masquer l'erreur initiale.
        }
    }

    private static int? NormalizeDuration(int? durationSec)
    {
        if (durationSec is null)
        {
            return null;
        }

        return Math.Clamp(durationSec.Value, 0, MaxDurationSec);
    }

    private static string SanitizeFileName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "fichier";
        }

        return name.Length <= MaxFileNameLength ? name : name[^MaxFileNameLength..];
    }
}
