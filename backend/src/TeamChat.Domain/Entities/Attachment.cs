namespace TeamChat.Domain.Entities;

/// <summary>
/// Metadonnees d'une piece jointe (fichier, image ou message vocal).
/// Le binaire est stocke via <c>IFileStorage</c> sous une cle aleatoire ;
/// le nom d'origine n'est jamais utilise comme chemin de stockage.
/// </summary>
public sealed class Attachment
{
    public Guid Id { get; set; }

    /// <summary>Cle de stockage aleatoire (jamais le nom d'origine).</summary>
    public string StorageKey { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    /// <summary>Type MIME reel detecte cote serveur.</summary>
    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    /// <summary>Duree en secondes pour un message vocal (null sinon).</summary>
    public int? DurationSec { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
