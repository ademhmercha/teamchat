using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;

namespace TeamChat.Infrastructure.Storage;

/// <summary>
/// Stockage sur disque local (developpement). Les cles sont des identifiants
/// aleatoires generes cote serveur ; le nom d'origine n'est jamais utilise.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorage> _logger;

    public LocalFileStorage(IOptions<StorageOptions> options, ILogger<LocalFileStorage> logger)
    {
        _logger = logger;

        var configured = options.Value.LocalPath;
        _basePath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);

        Directory.CreateDirectory(_basePath);
    }

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        await using (var file = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Fichier introuvable.", path);
        }

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("Piece jointe supprimee du disque : {StorageKey}", storageKey);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)
            || storageKey.Contains("..", StringComparison.Ordinal)
            || storageKey.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || storageKey.Contains('/')
            || storageKey.Contains('\\'))
        {
            throw new ArgumentException("Cle de stockage invalide.", nameof(storageKey));
        }

        return Path.Combine(_basePath, storageKey);
    }
}
