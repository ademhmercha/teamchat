using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeamChat.Domain.Constants;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

public static class DbSeeder
{
    /// <summary>
    /// Applique les migrations, cree les roles, la societe par defaut, le compte
    /// proprietaire (Owner) de cette societe et le super-admin de la plateforme.
    /// Les identifiants proviennent de la configuration, jamais en dur.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<TeamChatDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = provider.GetRequiredService<UserManager<AppUser>>();

        var defaultTenant = await EnsureDefaultTenantAsync(db, configuration);
        await EnsureTenantOwnerAsync(userManager, configuration, defaultTenant.Id);
        await EnsureSuperAdminAsync(userManager, configuration);
    }

    private static async Task<Tenant> EnsureDefaultTenantAsync(TeamChatDbContext db, IConfiguration configuration)
    {
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TenantDefaults.DefaultTenantId);
        if (existing is not null)
        {
            return existing;
        }

        var tenant = new Tenant
        {
            Id = TenantDefaults.DefaultTenantId,
            Name = configuration["Tenant:DefaultName"] ?? TenantDefaults.DefaultTenantName,
            Slug = TenantDefaults.DefaultTenantSlug,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static async Task EnsureTenantOwnerAsync(
        UserManager<AppUser> userManager,
        IConfiguration configuration,
        Guid tenantId)
    {
        var email = configuration["Admin:SeedEmail"];
        var password = configuration["Admin:SeedPassword"];
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            admin = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Administrateur",
                TenantId = tenantId,
                IsActive = true,
                EmailConfirmed = true,
            };

            var created = await userManager.CreateAsync(admin, password);
            if (created.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, Roles.Owner);
            }

            return;
        }

        // Compte existant (base deja en place) : on le rattache et on s'assure
        // qu'il possede le role Owner de sa societe.
        if (admin.DeletedAt is not null)
        {
            return;
        }

        var updated = false;
        if (admin.TenantId is null)
        {
            admin.TenantId = tenantId;
            updated = true;
        }

        if (updated)
        {
            await userManager.UpdateAsync(admin);
        }

        var roles = await userManager.GetRolesAsync(admin);
        if (!roles.Contains(Roles.Owner))
        {
            await userManager.AddToRoleAsync(admin, Roles.Owner);
        }
    }

    private static async Task EnsureSuperAdminAsync(UserManager<AppUser> userManager, IConfiguration configuration)
    {
        var email = configuration["SuperAdmin:SeedEmail"];
        var password = configuration["SuperAdmin:SeedPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var superAdmin = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Super Admin",
            TenantId = null,
            IsActive = true,
            EmailConfirmed = true,
        };

        var created = await userManager.CreateAsync(superAdmin, password);
        if (created.Succeeded)
        {
            await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
        }
    }
}
