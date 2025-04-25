#region

using DropBear.Codex.Notifications.Entities;
using Microsoft.EntityFrameworkCore;

#endregion

namespace DropBear.Codex.Notifications.Configuration;

public static class NotificationEntityConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationRecord>(entity =>
        {
            entity.ToTable("Notifications");
            // ...rest of configuration
        });

        // Configure other entities
    }
}
