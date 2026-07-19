using Beep.KocAiCommunity.Domain.Connectors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class ConnectorInstanceConfiguration : IEntityTypeConfiguration<ConnectorInstance>
{
    public void Configure(EntityTypeBuilder<ConnectorInstance> b)
    {
        b.ToTable("ConnectorInstances", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.ConnectorCode).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Endpoint).HasMaxLength(1024).IsRequired();
        b.Property(x => x.AuthMode).HasMaxLength(48).IsRequired();
        b.HasIndex(x => x.ConnectorCode);
    }
}

public sealed class CredentialVaultEntryConfiguration : IEntityTypeConfiguration<CredentialVaultEntry>
{
    public void Configure(EntityTypeBuilder<CredentialVaultEntry> b)
    {
        b.ToTable("CredentialVaultEntries", "platform");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.EncryptedValue).IsRequired();
        b.Property(x => x.ProtectionDescriptor).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.ConnectorInstanceId, x.Key }).IsUnique();
        b.HasOne<ConnectorInstance>().WithMany().HasForeignKey(x => x.ConnectorInstanceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConnectorHealthSnapshotConfiguration : IEntityTypeConfiguration<ConnectorHealthSnapshot>
{
    public void Configure(EntityTypeBuilder<ConnectorHealthSnapshot> b)
    {
        b.ToTable("ConnectorHealthSnapshots", "platform");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(1024);
        b.HasIndex(x => new { x.ConnectorInstanceId, x.MeasuredUtc });
        b.HasOne<ConnectorInstance>().WithMany().HasForeignKey(x => x.ConnectorInstanceId).OnDelete(DeleteBehavior.Cascade);
    }
}
