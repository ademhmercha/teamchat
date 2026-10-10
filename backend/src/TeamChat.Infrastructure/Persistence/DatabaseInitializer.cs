using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TeamChat.Infrastructure.Persistence;

/// <summary>Applique les migrations et le seed au demarrage de l'application.</summary>
public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;

    public DatabaseInitializer(IServiceProvider services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => DbSeeder.InitializeAsync(_services, _configuration);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
