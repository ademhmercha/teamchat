namespace TeamChat.Domain.Entities;

public sealed class Attachment : ITenantScoped
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string StorageKey { get; set; } = null!;

    public string OriginalFileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public int? DurationSec { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
