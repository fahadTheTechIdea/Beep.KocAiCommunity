using Beep.KocAiCommunity.Domain.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Beep.KocAiCommunity.Infrastructure.Persistence.Configurations;

public sealed class LearningTrackConfiguration : IEntityTypeConfiguration<LearningTrack>
{
    public void Configure(EntityTypeBuilder<LearningTrack> b)
    {
        b.ToTable("LearningTracks", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(1024).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.Domain).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.Status, x.OrderNo });
    }
}

public sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> b)
    {
        b.ToTable("Lessons", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(256).IsRequired();
        b.Property(x => x.ContentRef).HasMaxLength(512).IsRequired();
        b.HasIndex(x => new { x.TrackId, x.OrderNo });
        b.HasOne<LearningTrack>().WithMany().HasForeignKey(x => x.TrackId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TrackEnrollmentConfiguration : IEntityTypeConfiguration<TrackEnrollment>
{
    public void Configure(EntityTypeBuilder<TrackEnrollment> b)
    {
        b.ToTable("TrackEnrollments", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.TrackId, x.UserId }).IsUnique();
    }
}

public sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> b)
    {
        b.ToTable("LessonProgress", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();
    }
}

public sealed class TrackCompletionConfiguration : IEntityTypeConfiguration<TrackCompletion>
{
    public void Configure(EntityTypeBuilder<TrackCompletion> b)
    {
        b.ToTable("TrackCompletions", "koc");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        b.HasIndex(x => new { x.UserId, x.CompletedUtc });
        b.HasIndex(x => new { x.TrackId, x.UserId }).IsUnique();
    }
}
