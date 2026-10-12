namespace TeamChat.Application.Common;

/// <summary>Configuration du stockage des pieces jointes (section 10).</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";

    /// <summary>Repertoire de base du stockage disque (dev). Chemin relatif resolu depuis le repertoire de l'application.</summary>
    public string LocalPath { get; set; } = "uploads";

    public long MaxFileSizeBytes { get; set; } = 25 * 1024 * 1024;

    public long MaxVoiceSizeBytes { get; set; } = 10 * 1024 * 1024;

    public S3StorageOptions S3 { get; set; } = new();
}

public sealed class S3StorageOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public bool ForcePathStyle { get; set; } = true;
}
