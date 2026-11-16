using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TeamChat.Infrastructure.Persistence;

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
