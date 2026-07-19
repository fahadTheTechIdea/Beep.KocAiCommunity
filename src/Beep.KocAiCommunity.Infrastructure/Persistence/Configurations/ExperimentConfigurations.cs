using Beep.KocAiCommunity.Domain.Experiments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class ExperimentConfiguration : IEntityTypeConfiguration<Experiment>
{
    public void Configure(EntityTypeBuilder<Experiment> b)
    {
        b.ToTable("Experiments", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2048).IsRequired();
        b.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.Tags).HasMaxLength(512);
        b.HasIndex(x => new { x.OwnerUserId, x.CreatedUtc });
    }
}

public sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> b)
    {
        b.ToTable("ExperimentRuns", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.RunByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.FailureStage).HasMaxLength(64);
        b.Property(x => x.Task).HasMaxLength(48).IsRequired();
        b.Property(x => x.Algorithm).HasMaxLength(128);
        b.Property(x => x.PrimaryMetric).HasMaxLength(48);
        b.Property(x => x.SecondaryMetric).HasMaxLength(48);
        b.Property(x => x.DatasetSnapshotHash).HasMaxLength(128);
        b.Property(x => x.Tags).HasMaxLength(512);
        b.HasIndex(x => new { x.ExperimentId, x.CreatedUtc });
        b.HasOne<Experiment>().WithMany().HasForeignKey(x => x.ExperimentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RunMetricConfiguration : IEntityTypeConfiguration<RunMetric>
{
    public void Configure(EntityTypeBuilder<RunMetric> b)
    {
        b.ToTable("RunMetrics", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(64).IsRequired();
        b.Property(x => x.Dataset).HasMaxLength(24);
        b.Property(x => x.Phase).HasMaxLength(24);
        b.HasIndex(x => new { x.RunId, x.Step });
        b.HasOne<Run>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RunParameterConfiguration : IEntityTypeConfiguration<RunParameter>
{
    public void Configure(EntityTypeBuilder<RunParameter> b)
    {
        b.ToTable("RunParameters", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.ValueJson).HasMaxLength(2048).IsRequired();
        b.HasIndex(x => new { x.RunId, x.Name }).IsUnique();
        b.HasOne<Run>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}
