using Beep.KocAiCommunity.Domain.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class ArtifactReferenceConfiguration : IEntityTypeConfiguration<ArtifactReference>
{
    public void Configure(EntityTypeBuilder<ArtifactReference> b)
    {
        b.ToTable("ArtifactReferences", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.StorageKey).HasMaxLength(256).IsRequired();
        b.Property(x => x.LogicalPath).HasMaxLength(1024).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
        b.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Sha256).IsUnique();
        b.HasIndex(x => x.StorageKey);
    }
}
