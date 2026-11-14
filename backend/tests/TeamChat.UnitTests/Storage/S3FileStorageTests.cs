using System.Reflection;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using TeamChat.Application.Common;
using TeamChat.Infrastructure.Storage;

namespace TeamChat.UnitTests.Storage;

/// <summary>Construction du client S3 : cles explicites, chaine par defaut (role IAM), endpoint ou region.</summary>
public sealed class S3FileStorageTests : IDisposable
{
    private static readonly string[] EnvVars = { "AWS_ACCESS_KEY_ID", "AWS_SECRET_ACCESS_KEY", "AWS_SESSION_TOKEN" };

    private readonly Dictionary<string, string?> _saved = new();

    public S3FileStorageTests()
    {
        // Identifiants factices dans l'environnement : la chaine par defaut les trouve,
        // le test ne depend ni du profil de la machine ni du reseau (IMDS).
        foreach (var name in EnvVars)
        {
            _saved[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "ENVKEYID");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "envsecret");
        Environment.SetEnvironmentVariable("AWS_SESSION_TOKEN", null);
    }

    public void Dispose()
    {
        foreach (var (name, value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    [Fact]
    public void Without_access_key_uses_default_credential_chain()
    {
        var client = CreateClient(new S3StorageOptions { Bucket = "b", Region = "eu-west-1", Endpoint = "" });

        var credentials = GetCredentials(client);
        // Chaine par defaut : le SDK resout les identifiants a la premiere requete, donc rien
        // de type BasicAWSCredentials (cles explicites) n'est construit par S3FileStorage.
        Assert.IsNotType<BasicAWSCredentials>(credentials);
        if (credentials is not null)
        {
            Assert.Equal("ENVKEYID", credentials.GetCredentials().AccessKey);
        }
    }

    [Fact]
    public void With_access_key_uses_explicit_credentials()
    {
        var client = CreateClient(new S3StorageOptions
        {
            Bucket = "b",
            Region = "eu-west-1",
            AccessKey = "EXPLICITKEY",
            SecretKey = "explicitsecret",
        });

        var credentials = GetCredentials(client);
        Assert.IsType<BasicAWSCredentials>(credentials);
        Assert.Equal("EXPLICITKEY", credentials.GetCredentials().AccessKey);
    }

    [Fact]
    public void With_endpoint_uses_service_url()
    {
        var client = CreateClient(new S3StorageOptions
        {
            Bucket = "b",
            Endpoint = "http://localhost:9000",
            Region = "us-east-1",
            AccessKey = "k",
            SecretKey = "s",
        });

        Assert.Equal("http://localhost:9000", client.Config.ServiceURL?.TrimEnd('/'));
        Assert.Null(client.Config.RegionEndpoint);
        Assert.Equal("us-east-1", client.Config.AuthenticationRegion);
    }

    [Fact]
    public void Without_endpoint_uses_region_endpoint()
    {
        var client = CreateClient(new S3StorageOptions { Bucket = "b", Region = "eu-west-1", Endpoint = "" });

        Assert.Equal("eu-west-1", client.Config.RegionEndpoint?.SystemName);
        Assert.True(string.IsNullOrEmpty(client.Config.ServiceURL));
    }

    private static IAmazonS3 CreateClient(S3StorageOptions s3)
    {
        var storage = new S3FileStorage(Options.Create(new StorageOptions { Provider = "S3", S3 = s3 }));
        var field = typeof(S3FileStorage).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IAmazonS3)field.GetValue(storage)!;
    }

    private static AWSCredentials GetCredentials(IAmazonS3 client)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
        for (var type = client.GetType(); type is not null; type = type.BaseType)
        {
            var property = type.GetProperty("Credentials", flags);
            if (property is not null)
            {
                return (AWSCredentials)property.GetValue(client)!;
            }

            var field = type.GetFields(flags).FirstOrDefault(f => typeof(AWSCredentials).IsAssignableFrom(f.FieldType));
            if (field is not null)
            {
                return (AWSCredentials)field.GetValue(client)!;
            }
        }

        throw new InvalidOperationException("Credentials introuvables sur le client S3.");
    }
}
