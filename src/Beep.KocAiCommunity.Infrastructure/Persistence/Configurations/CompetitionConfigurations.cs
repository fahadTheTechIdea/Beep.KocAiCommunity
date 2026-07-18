using Beep.KocAiCommunity.Domain.Competitions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> b)
    {
        b.ToTable("Competitions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2048).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.ScorerCode).HasMaxLength(64).IsRequired();
        b.HasIndex(x => new { x.Status, x.VisibilityScope });
    }
}

public sealed class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        b.ToTable("Submissions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.SubmitterUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.CompetitionId, x.SubmitterUserId, x.SubmittedUtc });
        b.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LeaderboardEntryConfiguration : IEntityTypeConfiguration<LeaderboardEntry>
{
    public void Configure(EntityTypeBuilder<LeaderboardEntry> b)
    {
        b.ToTable("LeaderboardEntries", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.SubmitterUserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.CompetitionId, x.SubmitterUserId }).IsUnique();
        b.HasIndex(x => new { x.CompetitionId, x.Rank });
        b.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
