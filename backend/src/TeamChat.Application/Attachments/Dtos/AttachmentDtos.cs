namespace TeamChat.Application.Attachments.Dtos;

/// <summary>Contenu brut d'un upload, transmis par la couche Api.</summary>
public sealed record AttachmentUpload(
    string FileName,
    string? DeclaredContentType,
    long Length,
    Stream Content,
    bool IsVoice,
    int? DurationSec);

/// <summary>Contenu d'une piece jointe pret a etre renvoye au client.</summary>
public sealed record AttachmentDownload(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);
