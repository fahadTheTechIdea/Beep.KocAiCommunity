using Beep.KocAiCommunity.Domain.Datasets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class DatasetVersionConfiguration : IEntityTypeConfiguration<DatasetVersion>
{
    public void Configure(EntityTypeBuilder<DatasetVersion> b)
    {
        b.ToTable("DatasetVersions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(16).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(1024);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.Property(x => x.PublishedByUserId).HasMaxLength(450);
        b.HasIndex(x => new { x.DatasetId, x.VersionNumber }).IsUnique();
        b.HasOne<Dataset>().WithMany().HasForeignKey(x => x.DatasetId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DatasetFileConfiguration : IEntityTypeConfiguration<DatasetFile>
{
    public void Configure(EntityTypeBuilder<DatasetFile> b)
    {
        b.ToTable("DatasetFiles", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.LogicalPath).HasMaxLength(512).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.DatasetVersionId);
        b.HasOne<DatasetVersion>().WithMany().HasForeignKey(x => x.DatasetVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DatasetSchemaColumnConfiguration : IEntityTypeConfiguration<DatasetSchemaColumn>
{
    public void Configure(EntityTypeBuilder<DatasetSchemaColumn> b)
    {
        b.ToTable("DatasetSchemaColumns", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.ColumnName).HasMaxLength(256).IsRequired();
        b.Property(x => x.DataType).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.DatasetVersionId);
        b.HasOne<DatasetVersion>().WithMany().HasForeignKey(x => x.DatasetVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DatasetProfileConfiguration : IEntityTypeConfiguration<DatasetProfile>
{
    public void Configure(EntityTypeBuilder<DatasetProfile> b)
    {
        b.ToTable("DatasetProfiles", "koc");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.DatasetVersionId);
        b.HasOne<DatasetVersion>().WithMany().HasForeignKey(x => x.DatasetVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DatasetProfileColumnConfiguration : IEntityTypeConfiguration<DatasetProfileColumn>
{
    public void Configure(EntityTypeBuilder<DatasetProfileColumn> b)
    {
        b.ToTable("DatasetProfileColumns", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.ColumnName).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.DatasetProfileId);
        b.HasOne<DatasetProfile>().WithMany().HasForeignKey(x => x.DatasetProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
