using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<WorkflowEntity>
{
    public void Configure(EntityTypeBuilder<WorkflowEntity> b)
    {
        b.ToTable("Workflows", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2048);
        b.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => x.OwnerUserId);
    }
}

public sealed class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> b)
    {
        b.ToTable("WorkflowVersions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.DefinitionJson).IsRequired();
        b.Property(x => x.SnapshotHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1024);
        b.Property(x => x.PublishedByUserId).HasMaxLength(450);
        b.HasIndex(x => new { x.WorkflowId, x.VersionNumber }).IsUnique();
        b.HasOne<WorkflowEntity>().WithMany().HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplate> b)
    {
        b.ToTable("WorkflowTemplates", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1024);
        b.Property(x => x.Domain).HasMaxLength(64).IsRequired();
        b.Property(x => x.DefinitionJson).IsRequired();
        b.Property(x => x.SnapshotHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
    }
}
