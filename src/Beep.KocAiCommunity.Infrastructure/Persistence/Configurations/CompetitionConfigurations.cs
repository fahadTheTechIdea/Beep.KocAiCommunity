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
        b.Property(x => x.CategoryCode).HasMaxLength(64);
        b.HasIndex(x => new { x.Status, x.VisibilityScope });
        b.HasIndex(x => x.CategoryCode);
    }
}

public sealed class CompetitionCategoryConfiguration : IEntityTypeConfiguration<CompetitionCategory>
{
    public void Configure(EntityTypeBuilder<CompetitionCategory> b)
    {
        b.ToTable("CompetitionCategories", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(128).IsRequired();
        b.Property(x => x.Description).HasMaxLength(512);
        b.Property(x => x.Icon).HasMaxLength(128);

        // Competitions reference the code, so two categories may not share one.
        b.HasIndex(x => x.Code).IsUnique();
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
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.HasIndex(x => new { x.CompetitionId, x.SubmitterUserId, x.SubmittedUtc });

        // Unique per competition and submitter. It has to exclude nulls — the ordinary online path sends
        // no key, and a unique index over nulls would allow exactly one keyless submission per user.
        // The filter's quoting differs between providers, so KocDbContext sets it where it knows which
        // one it is talking to.
        b.HasIndex(x => new { x.CompetitionId, x.SubmitterUserId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("IX_Submissions_Idempotency");

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
        // Optimistic-concurrency token: the rank recompute reads every entry and rewrites ranks, so two
        // submissions to the same competition can race. The token makes a lost update surface as a
        // DbUpdateConcurrencyException (retried in UpdateLeaderboardAsync) rather than silently overwriting.
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
