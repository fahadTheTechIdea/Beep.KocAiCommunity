using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class ModelRunConfiguration : IEntityTypeConfiguration<ModelRun>
{
    public void Configure(EntityTypeBuilder<ModelRun> b)
    {
        b.ToTable("ModelRuns", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.DatasetName).HasMaxLength(256).IsRequired();
        b.Property(x => x.LabelColumn).HasMaxLength(128).IsRequired();
        b.Property(x => x.Task).HasMaxLength(64).IsRequired();
        b.Property(x => x.Algorithm).HasMaxLength(128).IsRequired();
        b.Property(x => x.PrimaryMetric).HasMaxLength(64).IsRequired();
        b.Property(x => x.SecondaryMetric).HasMaxLength(64).IsRequired();
        b.Property(x => x.RunByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.RunByUserId, x.CompletedUtc });
    }
}
