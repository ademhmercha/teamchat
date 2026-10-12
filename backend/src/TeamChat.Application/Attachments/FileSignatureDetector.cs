using System.Text;

namespace TeamChat.Application.Attachments;

public enum AttachmentKind
{
    Image,
    Voice,
    File,
}

/// <summary>Type reel d'un fichier detecte a partir de sa signature binaire.</summary>
public sealed record DetectedFileType(string ContentType, AttachmentKind Kind);

/// <summary>
/// Detection du type reel d'un fichier a partir de sa signature (magic bytes).
/// On ne se fie jamais a l'extension ni au Content-Type declare par le client
/// (section 8 : « analyse du type reel, pas seulement l'extension »).
/// </summary>
public static class FileSignatureDetector
{
    public static DetectedFileType? Detect(
        ReadOnlySpan<byte> data,
        string? declaredContentType,
        string fileName,
        bool isVoice)
    {
        // Executables et bibliotheques : rejects explicitement.
        if (StartsWith(data, 0x4D, 0x5A))
        {
            return null;
        }

        if (StartsWith(data, 0x7F, 0x45, 0x4C, 0x46))
        {
            return null;
        }

        // Images
        if (StartsWith(data, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
        {
            return new DetectedFileType("image/png", AttachmentKind.Image);
        }

        if (StartsWith(data, 0xFF, 0xD8, 0xFF))
        {
            return new DetectedFileType("image/jpeg", AttachmentKind.Image);
        }

        if (AsciiEquals(data, 0, "GIF87a") || AsciiEquals(data, 0, "GIF89a"))
        {
            return new DetectedFileType("image/gif", AttachmentKind.Image);
        }

        if (AsciiEquals(data, 0, "RIFF") && AsciiEquals(data, 8, "WEBP"))
        {
            return new DetectedFileType("image/webp", AttachmentKind.Image);
        }

        // PDF
        if (AsciiEquals(data, 0, "%PDF"))
        {
            return new DetectedFileType("application/pdf", AttachmentKind.File);
        }

        // Archives ZIP et formats Office Open XML (memes magic bytes).
        if (StartsWith(data, 0x50, 0x4B, 0x03, 0x04))
        {
            var officeType = Normalize(declaredContentType);
            if (officeType is "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                or "application/vnd.openxmlformats-officedocument.presentationml.presentation")
            {
                return new DetectedFileType(officeType, AttachmentKind.File);
            }

            return new DetectedFileType("application/zip", AttachmentKind.File);
        }

        // Audio (messages vocaux et fichiers audio)
        if (StartsWith(data, 0x1A, 0x45, 0xDF, 0xA3))
        {
            var isVideo = Normalize(declaredContentType)?.StartsWith("video/", StringComparison.Ordinal) == true;
            return new DetectedFileType(isVideo ? "video/webm" : "audio/webm", AttachmentKind.Voice);
        }

        if (AsciiEquals(data, 0, "OggS"))
        {
            return new DetectedFileType("audio/ogg", AttachmentKind.Voice);
        }

        if (AsciiEquals(data, 4, "ftyp"))
        {
            var declared = Normalize(declaredContentType);
            var isVideo = declared?.StartsWith("video/", StringComparison.Ordinal) == true;
            return new DetectedFileType(isVideo ? "video/mp4" : "audio/mp4", AttachmentKind.Voice);
        }

        if (AsciiEquals(data, 0, "ID3") || StartsWith(data, 0xFF, 0xFB) || StartsWith(data, 0xFF, 0xF3) || StartsWith(data, 0xFF, 0xF2))
        {
            return new DetectedFileType("audio/mpeg", AttachmentKind.Voice);
        }

        // Texte brut : pas de signature fiable, on se fie au type declare.
        if (Normalize(declaredContentType) == "text/plain")
        {
            return new DetectedFileType("text/plain", AttachmentKind.File);
        }

        // Un upload vocal sans signature audio connue est refuse.
        _ = isVoice;
        _ = fileName;

        return null;
    }

    private static string? Normalize(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var separator = contentType.IndexOf(';');
        var value = (separator >= 0 ? contentType[..separator] : contentType).Trim().ToLowerInvariant();
        return value.Length == 0 ? null : value;
    }

    private static bool StartsWith(ReadOnlySpan<byte> data, params byte[] signature)
        => data.Length >= signature.Length && data[..signature.Length].SequenceEqual(signature);

    private static bool AsciiEquals(ReadOnlySpan<byte> data, int offset, string value)
    {
        if (data.Length < offset + value.Length)
        {
            return false;
        }

        return data.Slice(offset, value.Length).SequenceEqual(Encoding.ASCII.GetBytes(value));
    }
}
