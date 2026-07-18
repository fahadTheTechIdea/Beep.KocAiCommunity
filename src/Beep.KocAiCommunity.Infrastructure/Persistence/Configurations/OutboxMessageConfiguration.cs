using Beep.KocAiCommunity.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("OutboxMessages", "platform");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(128).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        // Dispatcher scans unprocessed messages oldest-first.
        b.HasIndex(x => new { x.ProcessedUtc, x.CreatedUtc });
    }
}
