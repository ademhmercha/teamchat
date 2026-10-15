using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TeamChat.Application.Abstractions;
using TeamChat.Domain.Entities;

namespace TeamChat.Infrastructure.Persistence;

public sealed class TeamChatDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentTenant _tenant;

    public TeamChatDbContext(DbContextOptions<TeamChatDbContext> options, ICurrentTenant tenant)
        : base(options)
    {
        _tenant = tenant;
    }

    /// <summary>Societe courante (evaluee a chaque requete a partir du jeton).</summary>
    public Guid? CurrentTenantId => _tenant.TenantId;

    /// <summary>Vrai hors contexte authentifie (login, refresh, inscription, seed) : le filtre est ignore.</summary>
    public bool TenantFilterBypassed => _tenant.FilterDisabled;

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<CallLog> Calls => Set<CallLog>();

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Slug).HasMaxLength(120).IsRequired();
            entity.HasIndex(t => t.Slug).IsUnique();
        });

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.AvatarUrl).HasMaxLength(2048);
            entity.HasIndex(u => u.DisplayName);
            entity.HasIndex(u => u.TenantId);
            entity.HasQueryFilter(u => TenantFilterBypassed || u.TenantId == CurrentTenantId);
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

        builder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.Property(c => c.DirectKey).HasMaxLength(80);
            entity.HasIndex(c => c.DirectKey).IsUnique();
            entity.HasIndex(c => c.TenantId);
            entity.HasQueryFilter(c => TenantFilterBypassed || c.TenantId == CurrentTenantId);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(c => c.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ConversationParticipant>(entity =>
        {
            entity.ToTable("ConversationParticipants");
            entity.HasKey(p => new { p.ConversationId, p.UserId });
            entity.HasOne<Conversation>()
                .WithMany()
                .HasForeignKey(p => p.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => p.UserId);
        });

        builder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Sequence).UseIdentityColumn();
            entity.Property(m => m.Content).HasMaxLength(2000);
            entity.HasIndex(m => new { m.ConversationId, m.Sequence });
            entity.HasIndex(m => m.TenantId);
            entity.HasQueryFilter(m => TenantFilterBypassed || m.TenantId == CurrentTenantId);
            entity.HasOne<Conversation>()
                .WithMany()
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Attachment>()
                .WithMany()
                .HasForeignKey(m => m.AttachmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Attachment>(entity =>
        {
            entity.ToTable("Attachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.StorageKey).HasMaxLength(400).IsRequired();
            entity.Property(a => a.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(a => a.ContentType).HasMaxLength(128).IsRequired();
            entity.HasIndex(a => a.StorageKey).IsUnique();
            entity.HasIndex(a => a.TenantId);
            entity.HasQueryFilter(a => TenantFilterBypassed || a.TenantId == CurrentTenantId);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(a => a.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CallLog>(entity =>
        {
            entity.ToTable("CallLogs");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.TenantId);
            entity.HasQueryFilter(c => TenantFilterBypassed || c.TenantId == CurrentTenantId);
            entity.HasIndex(c => new { c.CallerId, c.StartedAt });
            entity.HasIndex(c => new { c.CalleeId, c.StartedAt });
            entity.HasIndex(c => new { c.Status, c.StartedAt });
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(c => c.CallerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(c => c.CalleeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).HasMaxLength(100).IsRequired();
            entity.Property(n => n.Body).HasMaxLength(300).IsRequired();
            entity.HasIndex(n => n.TenantId);
            entity.HasQueryFilter(n => TenantFilterBypassed || n.TenantId == CurrentTenantId);
            entity.HasIndex(n => new { n.UserId, n.CreatedAt });
            entity.HasIndex(n => new { n.UserId, n.ReadAt });
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<Conversation>()
                .WithMany()
                .HasForeignKey(n => n.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    public override int SaveChanges()
    {
        StampTenant();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Renseigne la societe courante sur les entites tenant-scoped creees.</summary>
    private void StampTenant()
    {
        if (CurrentTenantId is not { } tenantId)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ITenantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = tenantId;
            }
        }
    }
}
