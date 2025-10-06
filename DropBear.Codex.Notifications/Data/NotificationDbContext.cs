using DropBear.Codex.Notifications.Entities;
using DropBear.Codex.Notifications.Models;
using Microsoft.EntityFrameworkCore;

namespace DropBear.Codex.Notifications.Data
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
            : base(options)
        {
        }

        public DbSet<NotificationRecord> Notifications { get; set; } = null!;
        public DbSet<NotificationPreferences> NotificationPreferences { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure NotificationRecord
            modelBuilder.Entity<NotificationRecord>(entity =>
            {
                entity.ToTable("Notifications");

                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.ReadAt);
                entity.HasIndex(e => e.DismissedAt);

                entity.Property(e => e.Message)
                    .IsRequired()
                    .HasMaxLength(4000);

                entity.Property(e => e.Title)
                    .HasMaxLength(255);

                entity.Property(e => e.SerializedData)
                    .HasMaxLength(8000);
            });

            // Configure NotificationPreference
            modelBuilder.Entity<NotificationPreferences>(entity =>
            {
                entity.ToTable("NotificationPreferences");

                entity.HasKey(e => e.UserId);

                entity.Property(e => e.SerializedTypePreferences)
                    .HasMaxLength(4000);
            });
        }
    }
}
