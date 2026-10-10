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
    /// Applique les migrations puis cree les roles et le compte admin initial
    /// (email + mot de passe fournis par la configuration, jamais en dur).
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

        var email = configuration["Admin:SeedEmail"];
        var password = configuration["Admin:SeedPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = provider.GetRequiredService<UserManager<AppUser>>();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = "Administrateur",
            IsActive = true,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }
    }
}
