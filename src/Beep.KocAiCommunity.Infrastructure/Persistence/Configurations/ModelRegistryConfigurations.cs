using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class RegisteredModelConfiguration : IEntityTypeConfiguration<RegisteredModel>
{
    public void Configure(EntityTypeBuilder<RegisteredModel> b)
    {
        b.ToTable("RegisteredModels", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class ModelVersionConfiguration : IEntityTypeConfiguration<ModelVersion>
{
    public void Configure(EntityTypeBuilder<ModelVersion> b)
    {
        b.ToTable("ModelVersions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.SemVer).HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.MetricName).HasMaxLength(64).IsRequired();
        b.Property(x => x.RegisteredByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.ModelId, x.SemVer }).IsUnique();
        b.HasOne<RegisteredModel>().WithMany().HasForeignKey(x => x.ModelId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ModelApprovalConfiguration : IEntityTypeConfiguration<ModelApproval>
{
    public void Configure(EntityTypeBuilder<ModelApproval> b)
    {
        b.ToTable("ModelApprovals", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.ApproverUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Decision).HasMaxLength(16).IsRequired();
        b.HasIndex(x => new { x.ModelVersionId, x.ApproverUserId }).IsUnique();
        b.HasOne<ModelVersion>().WithMany().HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ModelDeploymentConfiguration : IEntityTypeConfiguration<ModelDeployment>
{
    public void Configure(EntityTypeBuilder<ModelDeployment> b)
    {
        b.ToTable("ModelDeployments", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Environment).HasMaxLength(32).IsRequired();
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.DeployedByUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.Status, x.Environment });
        b.HasOne<ModelVersion>().WithMany().HasForeignKey(x => x.ModelVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}
