using Beep.KocAiCommunity.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class SettingValueConfiguration : IEntityTypeConfiguration<SettingValue>
{
    public void Configure(EntityTypeBuilder<SettingValue> b)
    {
        b.ToTable("SettingValues", "platform");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(128).IsRequired();
        b.Property(x => x.Value).IsRequired();
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
    }
}

public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> b)
    {
        b.ToTable("FeatureFlags", "platform");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(128).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1024);
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
    }
}
