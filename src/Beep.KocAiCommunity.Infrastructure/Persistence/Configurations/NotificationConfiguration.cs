using Beep.KocAiCommunity.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("Notifications", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Type).HasMaxLength(64).IsRequired();
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Message).HasMaxLength(1024).IsRequired();
        b.Property(x => x.LinkUrl).HasMaxLength(256);
        b.HasIndex(x => new { x.UserId, x.ReadUtc, x.CreatedUtc });
    }
}
