namespace TeamChat.Application.Abstractions;

/// <summary>
/// Abstraction du stockage binaire des pieces jointes.
/// Disque local en developpement, stockage objet S3-compatible en production
/// (selection par configuration). Le code metier ignore l'implementation.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
