using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.LabelColumn).HasMaxLength(128).IsRequired();
        b.Property(x => x.TaskType).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.OwnerUserId, x.CreatedUtc });
    }
}
