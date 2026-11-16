using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;

namespace TeamChat.Infrastructure.Storage;

public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3FileStorage(IOptions<StorageOptions> options)
    {
        var s3 = options.Value.S3;

        var config = new AmazonS3Config { ForcePathStyle = s3.ForcePathStyle };

        if (!string.IsNullOrWhiteSpace(s3.Endpoint))
        {
            config.ServiceURL = s3.Endpoint;
            if (!string.IsNullOrWhiteSpace(s3.Region))
            {
                config.AuthenticationRegion = s3.Region;
            }
        }
        else if (!string.IsNullOrWhiteSpace(s3.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3.Region);
        }

        _client = string.IsNullOrWhiteSpace(s3.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), config);
        _bucket = s3.Bucket;
    }

    public async Task SaveAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = storageKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        };

        await _client.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, storageKey, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("Fichier introuvable.", storageKey);
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await _client.DeleteObjectAsync(_bucket, storageKey, cancellationToken);
    }
}
