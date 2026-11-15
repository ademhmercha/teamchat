namespace TeamChat.Application.Abstractions;

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
