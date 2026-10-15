using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using TeamChat.Api.Infrastructure;
using TeamChat.Application;
using TeamChat.Application.Abstractions;
using TeamChat.Application.Common;
using TeamChat.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var maxUploadBytes = builder.Configuration.GetValue<long>("Storage:MaxFileSizeBytes", 25 * 1024 * 1024);
var maxRequestBodyBytes = maxUploadBytes + (1024 * 1024);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBodyBytes);

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBodyBytes);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var redisBackplane = builder.Configuration["Redis:ConnectionString"];
var signalRBuilder = builder.Services.AddSignalR(options => options.AddFilter<TenantHubFilter>());
if (!string.IsNullOrWhiteSpace(redisBackplane))
{
    signalRBuilder.AddStackExchangeRedis(redisBackplane);
}

builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IUserSessionTerminator, SignalRUserSessionTerminator>();
builder.Services.AddHostedService<CallTimeoutService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, configuration) =>
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirst("sub")?.Value;
                if (!Guid.TryParse(sub, out var userId))
                {
                    context.Fail("Token invalide.");
                    return;
                }

                var cache = context.HttpContext.RequestServices.GetRequiredService<IUserProfileCache>();
                var profile = await cache.GetAsync(userId, context.HttpContext.RequestAborted);
                if (profile is null || !profile.IsActive)
                {
                    context.Fail("Compte inexistant ou desactive.");
                }
            },
            OnMessageReceived = context =>
            {
                // SignalR ne peut pas envoyer de header d'autorisation en
                // WebSocket : le JWT passe en query string pour /hubs.
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:4200")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    options.AddPolicy("frontend", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var loginPermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = loginPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    var chatPermitLimit = builder.Configuration.GetValue("RateLimiting:ChatPermitLimit", 120);
    options.AddPolicy("chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = chatPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    // Reponses JSON privees par defaut (section 8 bis) : sauf en-tete explicite.
    context.Response.OnStarting(() =>
    {
        var contentType = context.Response.ContentType;
        if (contentType is not null
            && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            && !context.Response.Headers.ContainsKey("Cache-Control"))
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        return Task.CompletedTask;
    });

    await next();
});
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    // Resout la societe une seule fois par requete authentifiee et la conserve
    // dans le scope. Les fabriques de cache (HybridCache) s'executent sans
    // IHttpContextAccessor : sans cela le filtre global serait desactive.
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var setter = context.RequestServices.GetRequiredService<ICurrentTenantSetter>();
        var claim = context.User.FindFirst("tenant_id")?.Value;
        setter.SetTenant(Guid.TryParse(claim, out var tenantId) ? tenantId : null);
    }

    await next();
});
app.UseAuthorization();
app.UseWebSockets();

app.MapControllers();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.MapHub<TeamChat.Api.Hubs.ChatHub>("/hubs/chat")
    .RequireAuthorization()
    .RequireRateLimiting("chat");

app.Run();

public partial class Program;