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

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<CallLog> Calls => Set<CallLog>();

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

        builder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.Property(c => c.DirectKey).HasMaxLength(80);
            entity.HasIndex(c => c.DirectKey).IsUnique();            entity.HasOne<AppUser>()
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
            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(a => a.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CallLog>(entity =>
        {
            entity.ToTable("CallLogs");
            entity.HasKey(c => c.Id);
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
    }
}
