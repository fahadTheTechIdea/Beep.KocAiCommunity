using Beep.KocAiCommunity.Domain.Datasets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class DatasetConfiguration : IEntityTypeConfiguration<Dataset>
{
    public void Configure(EntityTypeBuilder<Dataset> b)
    {
        b.ToTable("Datasets", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2048).IsRequired();
        b.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Domain).HasMaxLength(32).IsRequired();
        b.Property(x => x.Tags).HasMaxLength(512);
        b.HasIndex(x => new { x.VisibilityScope, x.VisibilityOrgUnitId });
        b.HasIndex(x => x.OwnerUserId);
    }
}
