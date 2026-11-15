namespace TeamChat.Application.Attachments.Dtos;

public sealed record AttachmentUpload(
    string FileName,
    string? DeclaredContentType,
    long Length,
    Stream Content,
    bool IsVoice,
    int? DurationSec);

public sealed record AttachmentDownload(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);
