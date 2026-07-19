using Beep.KocAiCommunity.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> b)
    {
        b.ToTable("Jobs", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasMaxLength(64).IsRequired();
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        b.Property(x => x.OwnerUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.LeaseOwnerId).HasMaxLength(128);
        b.Property(x => x.LastError).HasMaxLength(2048);

        // The claim query filters on status + due time; the owner index backs the run list.
        b.HasIndex(x => new { x.Status, x.NextAttemptUtc, x.LeaseExpiresUtc });
        b.HasIndex(x => new { x.OwnerUserId, x.CreatedUtc });
    }
}

public sealed class JobAttemptConfiguration : IEntityTypeConfiguration<JobAttempt>
{
    public void Configure(EntityTypeBuilder<JobAttempt> b)
    {
        b.ToTable("JobAttempts", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(24).IsRequired();
        b.Property(x => x.WorkerId).HasMaxLength(128);
        b.Property(x => x.Error).HasMaxLength(2048);
        b.HasIndex(x => new { x.JobId, x.AttemptNumber });
        b.HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class JobLogConfiguration : IEntityTypeConfiguration<JobLog>
{
    public void Configure(EntityTypeBuilder<JobLog> b)
    {
        b.ToTable("JobLogs", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Severity).HasMaxLength(16).IsRequired();
        b.Property(x => x.Message).HasMaxLength(2048).IsRequired();
        b.HasIndex(x => new { x.JobId, x.LoggedUtc });
        b.HasOne<Job>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
    }
}
