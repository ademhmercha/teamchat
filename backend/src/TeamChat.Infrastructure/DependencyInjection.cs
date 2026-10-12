using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Infrastructure.Caching;
using TeamChat.Infrastructure.Calls;
using TeamChat.Infrastructure.Email;
using TeamChat.Infrastructure.Identity;
using TeamChat.Infrastructure.Persistence;
using TeamChat.Infrastructure.Realtime;
using TeamChat.Infrastructure.Security;
using TeamChat.Infrastructure.Storage;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<CallOptions>(configuration.GetSection(CallOptions.SectionName));
        services.Configure<TurnOptions>(configuration.GetSection(TurnOptions.SectionName));

        AddCache(services, configuration);
        AddDatabase(services, configuration);
        AddStorage(services, configuration);
        AddIdentity(services);
        AddServices(services);

        return services;
    }

    private static void AddCache(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
        }

        services.AddHybridCache();
        services.AddSingleton<ICacheService, HybridCacheService>();
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TeamChatDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("La chaine de connexion 'Default' est requise.");
            options.UseNpgsql(connectionString);
        });
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"];
        if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, S3FileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }

        services.AddScoped<IAttachmentQueries, AttachmentQueries>();
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<TeamChatDbContext>()
            .AddDefaultTokenProviders();
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IUserProfileCache, UserProfileCache>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddScoped<IEmailSender, DevEmailSender>();
        services.AddScoped<IConversationQueries, ConversationQueries>();
        services.AddScoped<IAdminGroupQueries, AdminGroupQueries>();
        services.AddScoped<IMembershipService, MembershipService>();
        services.AddScoped<ICallQueries, CallQueries>();
        services.AddScoped<IIceServerProvider, TurnIceServerProvider>();
        services.AddSingleton<IPresenceTracker, PresenceTracker>();
        services.AddHostedService<DatabaseInitializer>();
    }
}
