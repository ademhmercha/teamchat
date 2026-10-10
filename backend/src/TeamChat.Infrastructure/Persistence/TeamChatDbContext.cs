using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

public sealed class TeamChatDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public TeamChatDbContext(DbContextOptions<TeamChatDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.AvatarUrl).HasMaxLength(2048);
            entity.HasIndex(u => u.DisplayName);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.HasIndex(t => t.UserId);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Ignore(t => t.IsActive);
        });
    }
}
