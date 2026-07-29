using Beep.KocAiCommunity.Domain.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> b)
    {
        b.ToTable("ContentTranslations", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        b.Property(x => x.EntityKey).HasMaxLength(128).IsRequired();
        b.Property(x => x.Field).HasMaxLength(64).IsRequired();
        b.Property(x => x.Language).HasMaxLength(8).IsRequired();
        b.Property(x => x.Text).HasMaxLength(2048).IsRequired();

        // One translation per field per language. Unique so a re-run of the seeder cannot quietly
        // stack duplicates that then resolve arbitrarily.
        b.HasIndex(x => new { x.EntityType, x.EntityKey, x.Field, x.Language }).IsUnique();
    }
}
